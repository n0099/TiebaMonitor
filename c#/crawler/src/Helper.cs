using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace tbm.Crawler;

public abstract class Helper
{
    public static byte[]? SerializedProtoBufOrNullIfEmpty(IMessage? protoBuf) =>
        protoBuf == null || protoBuf.CalculateSize() == 0 ? null : protoBuf.ToByteArray();

    public static byte[]? SerializedProtoBufWrapperOrNullIfEmpty<T>
        (IEnumerable<T>? valuesToWrap, Func<IMessage?> wrapperFactory) where T : IMessage =>
        valuesToWrap?.Select(message => message.CalculateSize()).Sum() is 0 or null
            ? null
            : SerializedProtoBufOrNullIfEmpty(wrapperFactory());

    public static RepeatedField<Content>? ParseThenUnwrapPostContent(byte[]? serializedProtoBuf) =>
        serializedProtoBuf == null ? null : PostContentWrapper.Parser.ParseFrom(serializedProtoBuf).Value;

    public static PostContentWrapper? WrapPostContent(RepeatedField<Content>? contents) =>
        contents == null ? null : new() {Value = {contents}};

    private static readonly JsonSerializerOptions UnescapedSerializeOptions =
        new() {Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)};

    public static string UnescapedJsonSerialize<TValue>(TValue value) =>
        JsonSerializer.Serialize(value, UnescapedSerializeOptions);

    public static void GetNowTimestamp(out Time now) => now = GetNowTimestamp();
    public static Time GetNowTimestamp() => (Time)DateTimeOffset.Now.ToUnixTimeSeconds();
}