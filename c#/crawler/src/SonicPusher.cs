namespace tbm.Crawler;

public sealed class SonicPusher : IDisposable
{
    private static readonly ReaderWriterLockSlim SuspendPushingFileLock = new();
    private readonly ILogger<SonicPusher> _logger;
    private readonly IConfigurationSection _config;

    public SonicPusher(ILogger<SonicPusher> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config.GetSection("Sonic");
        Ingest = NSonicFactory.Ingest(
            _config.GetValue("Hostname", "localhost"),
            _config.GetValue("Port", 1491),
            _config.GetValue("Secret", "SecretPassword"));
        CollectionPrefix = _config.GetValue<string>("CollectionPrefix", "tbm_");
    }

    public ISonicIngestConnection Ingest { get; }
    public string CollectionPrefix { get; }

    public void Dispose() => Ingest.Dispose();

    public float PushPost(Fid fid, string type, PostId id, IEnumerable<Content>? content)
    {
        if (!_config.GetValue("Enabled", false)) return 0;
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        float GetElapsedMs() => (float)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1000;

        if (content == null) return GetElapsedMs();
        var contentTexts = string.Join(' ', content
                .Where(c => c.Type != 2) // filter out alt text of emoticons
                .Select(c => c.Text))
            .Trim()

            // https://github.com/spikensbror-dotnet/nsonic/pull/10
            .Replace("\\", @"\\").Replace("\n", "\\n").Replace("\"", "\\\"");
        if (contentTexts == "") return GetElapsedMs();

        try
        {
            foreach (var text in contentTexts.Chunk(30000)) // https://github.com/spikensbror-dotnet/nsonic/issues/11
                Ingest.Push($"{CollectionPrefix}{type}_content", $"f{fid}",
                    id.ToString(CultureInfo.InvariantCulture), text.ToString(), "cmn");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while pushing the content of post id {} for {} in fid {} into sonic, content={}",
                id, type, fid, contentTexts);
        }

        var ret = GetElapsedMs();
        if (ret > 1000) _logger.LogWarning(
            "Pushing a single post content with {} UTF-16 chars into sonic for {} in fid {} spending {:F0}ms, content={}",
            contentTexts.Length, type, fid, ret, contentTexts);
        return ret;
    }

    public void PushPostWithCancellationToken<T>(
        IReadOnlyCollection<T> posts, Fid fid, string postType,
        Func<T, PostId> postIdSelector,
        Func<T, IEnumerable<Content>?> postContentSelector,
        CancellationToken stoppingToken = default)
    {
        try
        {
            SuspendPushingFileLock.EnterWriteLock();
            foreach (var p in posts)
            {
                stoppingToken.ThrowIfCancellationRequested();
                _ = PushPost(fid, postType, postIdSelector(p), postContentSelector(p));
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
        {
            string GetBase64EncodedPostContent(T p) =>
                Convert.ToBase64String(Helper.WrapPostContent(postContentSelector(p))
                    ?.ToByteArray() ?? ReadOnlySpan<byte>.Empty);
            File.AppendAllLines(ResumeSuspendPostContentsPushingWorker.GetFilePath(postType),
                posts.Select(p => $"{fid},{postIdSelector(p)},{GetBase64EncodedPostContent(p)}"));
            throw;
        }
        finally
        {
            SuspendPushingFileLock.ExitWriteLock();
        }
    }
}
