using System.IO.Hashing;
using System.Text.RegularExpressions;
using NetTopologySuite.Geometries;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using Point = NetTopologySuite.Geometries.Point;

namespace tbm.ImagePipeline.Consumer;

public partial class MetadataConsumer : IConsumer<ImageWithBytes>
{
    private readonly ILogger<MetadataConsumer> _logger;
    private readonly FailedImageHandler _failedImageHandler;
    private readonly (IEnumerable<byte[]> Exif, IEnumerable<byte[]> Icc, IEnumerable<byte[]> Iptc, IEnumerable<byte[]> Xmp)
        _commonEmbeddedMetadataXxHash3ToIgnore;

    static MetadataConsumer() => NetTopologySuite.NtsGeometryServices.Instance = new(
        coordinateSequenceFactory: NetTopologySuite.Geometries.Implementation.CoordinateArraySequenceFactory.Instance,
        precisionModel: new(1000d),
        srid: 4326, // WGS84
        geometryOverlay: GeometryOverlay.NG,
        coordinateEqualityComparer: new());

    public MetadataConsumer
        (ILogger<MetadataConsumer> logger, IConfiguration config, FailedImageHandler failedImageHandler)
    {
        (_logger, _failedImageHandler) = (logger, failedImageHandler);
        var section = config.GetSection("MetadataConsumer").GetSection("CommonEmbeddedMetadataXxHash3ToIgnore");
        IEnumerable<byte[]> GetCommonXxHash3ToIgnore(string key) =>
            (section.GetSection(key).Get<string[]>() ?? []).Select(Convert.FromHexString);
        _commonEmbeddedMetadataXxHash3ToIgnore = (
            Exif: GetCommonXxHash3ToIgnore("Exif"),
            Icc: GetCommonXxHash3ToIgnore("Icc"),
            Iptc: GetCommonXxHash3ToIgnore("Iptc"),
            Xmp: GetCommonXxHash3ToIgnore("Xmp")
        );
    }

    public (IEnumerable<ImageId> Failed, IEnumerable<ImageId> Consumed) Consume(
        ImagePipelineDbContext db,
        IReadOnlyCollection<ImageWithBytes> imageKeysWithT,
        CancellationToken stoppingToken = default)
    {
        var metadataEithers = _failedImageHandler
            .TrySelect(imageKeysWithT,
                imageWithBytes => imageWithBytes.ImageInReply.ImageId,
                GetImageMetaData(stoppingToken))
            .ToList();
        db.ImageMetadata.AddRange(metadataEithers.Rights());
        var failed = metadataEithers.Lefts().ToList();
        return (failed, imageKeysWithT.Select(i => i.ImageInReply.ImageId).Except(failed));
    }

    private Func<ImageWithBytes, ImageMetadata> GetImageMetaData
        (CancellationToken stoppingToken = default) => imageWithBytes =>
    {
        stoppingToken.ThrowIfCancellationRequested();
        var (image, imageBytes) = imageWithBytes;
        var info = Image.Identify(imageBytes);
        var meta = info.Metadata;
        if (meta.DecodedImageFormat is not (JpegFormat or PngFormat or GifFormat or BmpFormat))
            ThrowHelper.ThrowNotSupportedException($"Not supported image format {meta.DecodedImageFormat?.Name}.");

        return new()
        {
            ImageId = image.ImageId,
            Format = meta.DecodedImageFormat?.Name,
            Width = (ushort)info.Width,
            Height = (ushort)info.Height,
            BitsPerPixel = (ushort)info.PixelType.BitsPerPixel,
            FrameCount = (uint)info.FrameMetadataCollection.Count,
            XxHash3 = XxHash3.Hash(imageBytes),
            DownloadedByteSize = image.ExpectedByteSize == imageBytes.Length
                ? null
                : new() {DownloadedByteSize = (uint)imageBytes.Length},
            EmbeddedExif = CreateEmbeddedExifFromProfile(meta.ExifProfile),
            EmbeddedIcc = CreateEmbeddedFromProfile<IccProfile, ImageMetadata.Icc>
                (_commonEmbeddedMetadataXxHash3ToIgnore.Icc, meta.IccProfile, i => i.ToByteArray()),
            EmbeddedIptc = CreateEmbeddedFromProfile<IptcProfile, ImageMetadata.Iptc>
                (_commonEmbeddedMetadataXxHash3ToIgnore.Iptc, meta.IptcProfile, i => i.Data),
            EmbeddedXmp = CreateEmbeddedFromProfile<XmpProfile, ImageMetadata.Xmp>
                (_commonEmbeddedMetadataXxHash3ToIgnore.Xmp, meta.XmpProfile, i => i.ToByteArray()),
            JpgMetadata = ImageMetadata.Jpg.FromImageSharpMetadata(meta),
            PngMetadata = ImageMetadata.Png.FromImageSharpMetadata(meta),
            GifMetadata = ImageMetadata.Gif.FromImageSharpMetadata(meta),
            BmpMetadata = ImageMetadata.Bmp.FromImageSharpMetadata(meta)
        };
    };

    private TEmbeddedMetadata? CreateEmbeddedFromProfile<TImageSharpProfile, TEmbeddedMetadata>(
        IEnumerable<byte[]> commonXxHash3ToIgnore,
        TImageSharpProfile? profile,
        Func<TImageSharpProfile, byte[]?> rawBytesSelector)
        where TImageSharpProfile : class
        where TEmbeddedMetadata : ImageMetadata.Embedded, new()
    {
        if (profile == null) return null;
        var rawBytes = rawBytesSelector(profile); // will be null when param profile is null
        if (rawBytes == null || rawBytes.Length == 0) return null;
        if (rawBytes.Length > 65535)
            _logger.LogWarning("Embedded {} in image contains {} bytes",
                typeof(TEmbeddedMetadata).Name.ToUpperInvariant(), rawBytes.Length);
        var xxHash3 = XxHash3.Hash(rawBytes);
        return new()
        {
            XxHash3 = xxHash3,
            RawBytes = commonXxHash3ToIgnore.Contains(xxHash3, ByteArrayEqualityComparer.Instance) ? null : rawBytes
        };
    }

    private ImageMetadata.Exif? CreateEmbeddedExifFromProfile(ExifProfile? exif)
    {
        T? GetExifTagValueOrNull<T>(ExifTag<T> tag) where T : class =>
            exif.TryGetValue(tag, out var value) ? value.Value : null;
        T? GetExifTagValueOrNull2<T>(ExifTag<T> tag) where T : struct =>
            exif.TryGetValue(tag, out var value) ? value.Value : null;

        var ret = CreateEmbeddedFromProfile<ExifProfile, ImageMetadata.Exif>
            (_commonEmbeddedMetadataXxHash3ToIgnore.Exif, exif, i => i.ToByteArray());
        if (ret == null || exif == null) return ret;

        // https://exiftool.org/TagNames/EXIF.html, https://exiv2.org/tags.html
        ret.Orientation = exif.TryGetValue(ExifTag.Orientation, out var orientation)
            ? Enum.GetName((ImageMetadata.Exif.ExifOrientation)orientation.Value)
            : null;
        ret.ImageDescription = GetExifTagValueOrNull(ExifTag.ImageDescription).NullIfEmpty();
        ret.UserComment = GetExifTagValueOrNull2(ExifTag.UserComment).ToString().NullIfEmpty();
        ret.Artist = GetExifTagValueOrNull(ExifTag.Artist).NullIfEmpty();
        ret.XpAuthor = GetExifTagValueOrNull(ExifTag.XPAuthor).NullIfEmpty();
        ret.Copyright = GetExifTagValueOrNull(ExifTag.Copyright).NullIfEmpty();
        ret.ImageUniqueId = GetExifTagValueOrNull(ExifTag.ImageUniqueID).NullIfEmpty();
        ret.BodySerialNumber = GetExifTagValueOrNull(ExifTag.SerialNumber).NullIfEmpty();
        ret.Make = GetExifTagValueOrNull(ExifTag.Make).NullIfEmpty();
        ret.Model = GetExifTagValueOrNull(ExifTag.Model).NullIfEmpty();
        ret.Software = GetExifTagValueOrNull(ExifTag.Software).NullIfEmpty();
        ret.CustomRendered = GetExifTagValueOrNull2(ExifTag.CustomRendered);

        var parsedDateTime = ExifDateTimeTagValuesParser.ParseExifDateTimeOrNull(
            GetExifTagValueOrNull(ExifTag.DateTime), GetExifTagValueOrNull(ExifTag.SubsecTime));
        ret.DateTime = parsedDateTime?.DateTime;
        ret.DateTimeOffset = parsedDateTime?.Offset;

        var parsedDateTimeDigitized = ExifDateTimeTagValuesParser.ParseExifDateTimeOrNull(
            GetExifTagValueOrNull(ExifTag.DateTimeDigitized), GetExifTagValueOrNull(ExifTag.SubsecTimeDigitized));
        ret.DateTimeDigitized = parsedDateTimeDigitized?.DateTime;
        ret.DateTimeDigitizedOffset = parsedDateTimeDigitized?.Offset;

        var parsedDateTimeOriginal = ExifDateTimeTagValuesParser.ParseExifDateTimeOrNull(
            GetExifTagValueOrNull(ExifTag.DateTimeOriginal), GetExifTagValueOrNull(ExifTag.SubsecTimeOriginal));
        ret.DateTimeOriginal = parsedDateTimeOriginal?.DateTime;
        ret.DateTimeOriginalOffset = parsedDateTimeOriginal?.Offset;

        ret.OffsetTime = GetExifTagValueOrNull(ExifTag.OffsetTime).NullIfEmpty();
        ret.OffsetTimeDigitized = GetExifTagValueOrNull(ExifTag.OffsetTimeDigitized).NullIfEmpty();
        ret.OffsetTimeOriginal = GetExifTagValueOrNull(ExifTag.OffsetTimeOriginal).NullIfEmpty();

        ret.GpsDateTime = ExifGpsTagValuesParser.ParseGpsDateTimeOrNull(
            GetExifTagValueOrNull(ExifTag.GPSTimestamp),
            GetExifTagValueOrNull(ExifTag.GPSDateStamp));
        ret.GpsCoordinate = ExifGpsTagValuesParser.ParseGpsCoordinateOrNull(exif.Values,
            GetExifTagValueOrNull(ExifTag.GPSLatitude),
            GetExifTagValueOrNull(ExifTag.GPSLatitudeRef),
            GetExifTagValueOrNull(ExifTag.GPSLongitude),
            GetExifTagValueOrNull(ExifTag.GPSLongitudeRef));
        ret.GpsImgDirection = GetExifTagValueOrNull2(ExifTag.GPSImgDirection)?.ToSingle().NanToNull();
        ret.GpsImgDirectionRef = GetExifTagValueOrNull(ExifTag.GPSImgDirectionRef).NullIfEmpty();

        ret.TagNames = exif.Values.Select(i => new ImageMetadata.Exif.TagName {Name = i.Tag.ToString()})

            // tags might be duplicated in EXIF with same or different values
            .DistinctBy(tagName => tagName.Name).ToList();
        return ret;
    }

    private static class ExifGpsTagValuesParser
    {
        public static DateTime? ParseGpsDateTimeOrNull(Rational[]? timeStamp, string? dateStamp)
        {
            if (timeStamp == null || dateStamp.NullIfEmpty() == null) return null;

#pragma warning disable S3220 // Method calls should not resolve ambiguously to overloads with "params"
            var dateParts = dateStamp!.Split(':', '-').Select(int.Parse).ToList();
#pragma warning restore S3220 // Method calls should not resolve ambiguously to overloads with "params"
            if (dateParts is not [var year, var month, var day])
                throw new ArgumentOutOfRangeException(nameof(dateStamp), dateStamp,
                $"Unexpected \"{dateStamp}\", expecting three parts separated by ':' or '-'.");
            if (year == 0 || month == 0 || day == 0) return null;

            var timeParts = timeStamp.Select(i => (int)i.ToSingle().NanToZero()).ToList();
            return timeParts is [var hour, var minute, _] // discard matching seconds
                ? new DateTime(year, month: month, day: day, hour, minute, second: 0, DateTimeKind.Unspecified)

                    // possible fractional seconds such as rational 5510/100
                    .AddSeconds(timeStamp[2].ToSingle().NanToZero())
                : throw new ArgumentOutOfRangeException(nameof(timeStamp), timeStamp,
                    $"Unexpected \"{timeStamp}\", expecting three rationals.");
        }

        public static Point? ParseGpsCoordinateOrNull(
            IEnumerable<IExifValue> allTagValues,
            IEnumerable<Rational>? latitude,
#pragma warning disable AV1568 // Parameter value should not be overwritten in method body
            string? latitudeRef,
#pragma warning restore AV1568 // Parameter value should not be overwritten in method body
            IEnumerable<Rational>? longitude,
            string? longitudeRef)
        {
            // https://issues.apache.org/jira/browse/IMAGING-24
            string? GetOtherLatitudeRefTagValueOrNull()
            {
                var tags = allTagValues.Where(value => value.Tag == ExifTag.GPSLatitudeRef);
                return tags.Select(value => (value.GetValue() as IExifValue<string>)?.Value)
                    .FirstOrDefault(value => value is "N" or "S");
            }
            if (latitudeRef is not ("N" or "S")) latitudeRef = GetOtherLatitudeRefTagValueOrNull();

            var latitudeDms = latitude?.Select(i => i.ToDouble()).ToList();
            var longitudeDms = longitude?.Select(i => i.ToDouble()).ToList();
            if (latitudeDms == null || latitudeRef == null || longitudeDms == null || longitudeRef == null)
                return null;

            latitudeDms[0] = latitudeRef switch
            {
                "N" => latitudeDms[0] > 0 ? latitudeDms[0] : -latitudeDms[0],
                "S" => latitudeDms[0] > 0 ? -latitudeDms[0] : latitudeDms[0],
                _ => throw new ArgumentOutOfRangeException(nameof(latitudeRef), latitudeRef,
                    $"Unexpected \"{latitudeRef}\", expecting \"N\" or \"S\".")
            };
            longitudeDms[0] = longitudeRef switch
            {
                "E" => longitudeDms[0] > 0 ? longitudeDms[0] : -longitudeDms[0],
                "W" => longitudeDms[0] > 0 ? -longitudeDms[0] : longitudeDms[0],
                _ => throw new ArgumentOutOfRangeException(nameof(longitudeRef), longitudeRef,
                    $"Unexpected \"{longitudeRef}\", expecting \"E\" or \"W\".")
            };

            var coordinate = new Coordinate(ConvertDmsToDd(longitudeDms), ConvertDmsToDd(latitudeDms));
            return coordinate.IsValid
                ? NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory().CreatePoint(coordinate)
                : null;
        }

        private static double ConvertDmsToDd(List<double> dms)
        { // https://stackoverflow.com/questions/3249700/convert-degrees-minutes-seconds-to-decimal-coordinates
            if (dms.Count != 3) throw new ArgumentException(
                "Unexpected length for DMS, expecting three doubles.", nameof(dms));
            var degrees = dms[0];
            var minutes = dms[1];
            var seconds = dms[2];
            return degrees > 0
                ? degrees + (minutes / 60) + (seconds / 3600)
                : degrees - (minutes / 60) - (seconds / 3600);
        }
    }

    private static partial class ExifDateTimeTagValuesParser
    {
#pragma warning disable AV1568 // Parameter value should not be overwritten in method body
        public static DateTimeAndOffset? ParseExifDateTimeOrNull(string? exifDateTime, string? exifFractionalSeconds)
#pragma warning restore AV1568 // Parameter value should not be overwritten in method body
        { // https://gist.github.com/thanatos/eee17100476a336a711e
            // tested inputs with valid results:
            // "2019:02:07 21:238"         => "2019/2/7 21:23:08"  , ""
            // "2019:04:26 20:08:02"       => "2019/4/26 20:08:02" , ""
            // "2019:04:26 24:08:02"       => "2019/4/27 0:08:02"  , ""
            // "2019:04:30 11:04:95", "10" => "2019/4/30 11:05:35.1", ""
            // "2018:09:12 21:20:08下午"    => "2018/9/12 21:20:08" , ""
            // "2018:09:12 09:20:08上午"    => "2018/9/12 9:20:08"  , ""
            // "2018:09:12 09:20:08下午"    => "2018/9/12 21:20:08" , ""
            // "2018:09:12 9:20:08下午"     => "2018/9/12 21:20:08" , ""
            // "2013: 1: 1  8:10:25"       => "2013/1/1 8:10:25"   , ""
            // "2019-04-09T02:14:04-12:00" => "2019/4/9 2:14:04"   , "-12:00"
            // "2019-04-09T02:14:04+09:00" => "2019/4/9 2:14:04"   , "+09:00"
            // "2017/05/22 00:04:31"       => "2017/5/22 0:04:31"  , ""
            // "2017-05-22 00:04:31"       => "2017/5/22 0:04:31"  , ""
            // "Sat Mar 03 10:05:45 2007"  => "2007/3/3 10:05:45"  , ""
            // "1556068385188"             => "2019/4/24 1:13:05"  , "+00:00"
            // "1373363130", "33483"       => "2013/7/9 9:45:30.33483", "+00:00"
            // "lead2018:07:20 01:51:11trail", "123" => "2018/7/20 1:51:11.123", ""
            if (exifDateTime?.NullIfEmpty() == null || exifDateTime == "0000:00:00 00:00:00") return null;

            if (ExtractCommonExifDateTimeWithLeadingOrTrailingCharsRegex().Match(exifDateTime) is {Success: true} m)
                exifDateTime = m.Groups["dateTime"].Value;

            // https://stackoverflow.com/questions/4483886/how-can-i-get-a-count-of-the-total-number-of-digits-in-a-number/51099524#51099524
            [SuppressMessage("Major Code Smell", "S3358:Ternary operators should not be nested")]
            static int CountDigits(int n) => n == 0 ? 1 : (n > 0 ? 1 : 2) + (int)Math.Log10(Math.Abs((double)n));
            var hasFractionalSeconds = int.TryParse(exifFractionalSeconds, CultureInfo.InvariantCulture, out var fractionalSeconds);
            fractionalSeconds = hasFractionalSeconds ? fractionalSeconds : 0;

            if (exifDateTime == "00000" && fractionalSeconds == 0) return null;
            var ret = ParseWithoutOffset(exifDateTime)
                      ?? ParseWithOffset(exifDateTime)
                      ?? ParseWithOverflowedTimeParts(exifDateTime)
                      ?? ParseAsUnixTimestamp(exifDateTime)
                      ?? throw new ArgumentException(
                          $"Failed to parse provided EXIF date time \"{exifDateTime}\""
                          + $" with fractional seconds {fractionalSeconds}.");
            return fractionalSeconds == 0 ? ret : ret with
            {
                DateTime = ret.DateTime.AddSeconds(fractionalSeconds / Math.Pow(10, CountDigits(fractionalSeconds)))
            };
        }

        [GeneratedRegex(
            "^.*(?<dateTime>[0-9]{4}:[0-9]{2}:[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}(?!(上|下)午)?).*$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
        private static partial Regex ExtractCommonExifDateTimeWithLeadingOrTrailingCharsRegex();

        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1025:Code should not contain multiple whitespace in a row")]
        private static DateTimeAndOffset? ParseWithoutOffset(string exifDateTime)
        {
            var culture = (DateTimeFormatInfo)CultureInfo.InvariantCulture.DateTimeFormat.Clone();
            culture.AMDesignator = "上午";
            culture.PMDesignator = "下午";
            var dateTime = DateTime.TryParseExact(exifDateTime,
            [
                "yyyy':'MM':'dd HH':'mm':'ss",   // 2019:04:26 20:08:02
                "yyyy':'MM':'dd HH':'mm':'sstt", // 2018:09:12 21:20:08下午, 2018:09:12 09:20:08上午
                "yyyy':'MM':'dd h':'mm':'sstt",  // 2018:09:12 09:20:08下午, 2018:09:12 9:20:08下午
                "yyyy':'MM':'dd HH':'mms",       // 2019:02:07 21:238
                "yyyy':'M':'d H':'m':'s",        // 2013: 1: 1  8:10:25 with DateTimeStyles.AllowWhiteSpaces
                "yyyy'/'MM'/'dd HH':'mm':'ss",   // 2017/05/22 00:04:31
                "yyyy-MM-dd HH':'mm':'ss",       // 2017-05-22 00:04:31
                "ddd MMM dd HH':'mm':'ss yyyy"   // Sat Mar 03 10:05:45 2007
            ], culture, DateTimeStyles.AllowWhiteSpaces, out var dt)
                ? dt
                : default;
            return dateTime == default ? null : new(dateTime, Offset: null);
        }

        private static DateTimeAndOffset? ParseWithOffset(string exifDateTime)
        {
            var dateTimeOffset = DateTimeOffset.TryParseExact(exifDateTime,
                "yyyy-MM-ddTHH':'mm':'sszzz", // 2019-04-09T02:14:04-12:00, 2019-04-09T02:14:04+09:00
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dto)
                ? dto
                : default;
            return dateTimeOffset == default
                ? null
                : new(dateTimeOffset.DateTime, dateTimeOffset.ToString("zzz", CultureInfo.InvariantCulture));
        }

        [GeneratedRegex(
            "^(?<year>[0-9]{4}):(?<month>[0-9]{2}):(?<day>[0-9]{2}) (?<hour>[0-9]{2}):(?<minute>[0-9]{2}):(?<second>[0-9]{2})$",
            RegexOptions.Compiled, matchTimeoutMilliseconds: 100)]
        private static partial Regex ExtractExifDateTimePartsRegex();

        // https://stackoverflow.com/questions/5208607/parsing-times-above-24-hours-in-c-sharp/76483705#76483705
        // https://en.wikipedia.org/wiki/Date_and_time_notation_in_Japan#Time
        // https://ja.wikipedia.org/wiki/30%E6%99%82%E9%96%93%E5%88%B6
        // e.g. 2019:04:26 24:08:02 or malformed 2019:04:30 11:04:95
        private static DateTimeAndOffset? ParseWithOverflowedTimeParts(string exifDateTime) =>
            ExtractExifDateTimePartsRegex().Match(exifDateTime) is not {Success: true} m
                ? null
                : new(new DateTime(
                        int.Parse(m.Groups["year"].ValueSpan, CultureInfo.InvariantCulture),
                        int.Parse(m.Groups["month"].ValueSpan, CultureInfo.InvariantCulture),
                        int.Parse(m.Groups["day"].ValueSpan, CultureInfo.InvariantCulture),
                        hour: 0, minute: 0, second: 0, DateTimeKind.Unspecified)
                    .AddHours(int.Parse(m.Groups["hour"].ValueSpan, CultureInfo.InvariantCulture))
                    .AddMinutes(int.Parse(m.Groups["minute"].ValueSpan, CultureInfo.InvariantCulture))
                    .AddSeconds(int.Parse(m.Groups["second"].ValueSpan, CultureInfo.InvariantCulture)),
                    Offset: null);

        private static DateTimeAndOffset? ParseAsUnixTimestamp(string exifDateTime) =>
            long.TryParse(exifDateTime, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsedUnixTimestamp)
                ? exifDateTime.Length switch
                {
                    // accepting from 1973-03-03 17:46:40.000 to 2286-11-21 01:46:39.999
                    // when the input contains no leading zeros, e.g. 1556068385188
                    >= 12 and <= 13 =>
                        new(DateTimeOffset.FromUnixTimeMilliseconds(parsedUnixTimestamp).DateTime, "+00:00"),

                    // accepting from 1973-03-03 17:46:40 to 2286-11-21 01:46:39
                    // when the input contains no leading zeros, e.g. 1373363130
                    >= 9 and <= 10 =>
                        new(DateTimeOffset.FromUnixTimeSeconds(parsedUnixTimestamp).DateTime, "+00:00"),
                    _ => null
                }
                : null;

        public sealed record DateTimeAndOffset(DateTime DateTime, string? Offset);
    }
}
