using System.Security.Cryptography;
using System.Text;

namespace tbm.Crawler.Tieba;

public class ClientRequester(
    ILogger<ClientRequester> logger,
    IConfiguration config,
    IHttpClientFactory httpFactory,
    ClientRequesterTcs requesterTcs)
{
    // https://github.com/Starry-OvO/aiotieba/issues/123#issuecomment-1563314122
    public const string LegacyClientApiDomain = "http://c.tieba.baidu.com";
    public const string ClientApiDomain = "http://tiebac.baidu.com";

    private static readonly Random Rand = new();
    private readonly IConfigurationSection _config = config.GetSection("ClientRequester");

    public async Task<JsonElement> RequestJson(
        string url,
        string clientVersion,
        IReadOnlyDictionary<string, string> postParamsKeyByName,
        CancellationToken stoppingToken = default) =>
        await Request(() => PostJson(url, postParamsKeyByName, clientVersion, stoppingToken), stream =>
        {
            stoppingToken.ThrowIfCancellationRequested();
            using var doc = JsonDocument.Parse(stream);
            return doc.RootElement.Clone();
        });

    public async Task<TResponse> RequestProtoBuf<TRequest, TResponse>(
        string url,
        string clientVersion,
        TRequest requestParam,
        Action<TRequest, Common> commonParamSetter,
        Func<TResponse> responseFactory,
        CancellationToken stoppingToken = default)
        where TRequest : class, IMessage<TRequest>
        where TResponse : class, IMessage<TResponse> =>
        await Request(() => PostProtoBuf(url, clientVersion, requestParam, commonParamSetter, stoppingToken), stream =>
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                return new MessageParser<TResponse>(responseFactory).ParseFrom(stream);
            }
            catch (InvalidProtocolBufferException e)
            {
                _ = stream.Seek(0, SeekOrigin.Begin);
                using var stream2 = new MemoryStream((int)stream.Length);
                stream.CopyTo(stream2);
                ObjectDisposedException.ThrowIf(!stream2.TryGetBuffer(out var buffer), stream2);
                var responseBody = Encoding.UTF8.GetString(buffer);

                // the invalid protoBuf bytes usually is just a plain html string
                if (responseBody.Contains("为了保护您的账号安全和最佳的浏览体验，当前业务已经不支持IE8以下浏览器"))
                    throw new TiebaException(shouldRetry: true, shouldSilent: true);
                throw new TiebaException($"Malformed protoBuf response from tieba. {responseBody}", e);
            }
        });

    private static async Task<T> Request<T>
        (Func<Task<HttpResponseMessage>> requester, Func<Stream, T> responseConsumer)
    {
        try
        {
            using var response = await requester();
#pragma warning disable IDISP001 // Dispose created
            var stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync();
#pragma warning restore IDISP001 // Dispose created
            return responseConsumer(stream);
        }
        catch (TaskCanceledException e) when (e.InnerException is TimeoutException)
        {
            throw new TiebaException("Tieba client request timeout.");
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == null)
                throw new TiebaException("Network error from tieba.", e);
            throw new TiebaException($"HTTP {(int)e.StatusCode} from tieba.");
        }
    }

    private async Task<HttpResponseMessage> PostJson(
        string url,
        IReadOnlyDictionary<string, string> postParamsKeyByName,
        string clientVersion,
        CancellationToken stoppingToken = default)
    {
        var postParamPairs = new Dictionary<string, string>
        {
            {"_client_id", $"wappc_{Rand.NextLong(1000000000000, 9999999999999)}_{Rand.Next(100, 999)}"},
            {"_client_type", "2"},
            {"_client_version", clientVersion}
        }.Concat(postParamsKeyByName).ToList();
        var signature = postParamPairs.Aggregate("", (acc, pair) =>
        {
            acc += pair.Key + '=' + pair.Value;
            return acc;
        }) + "tiebaclient!!!";
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
        var signatureMd5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(signature)));
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        postParamPairs.Add(KeyValuePair.Create("sign", signatureMd5));

        return await Post(async http =>
            {
                using var content = new FormUrlEncodedContent(postParamPairs);
                return await http.PostAsync(url, content, stoppingToken);
            },
            () => logger.LogTrace("POST {} {}", url, postParamsKeyByName), stoppingToken);
    }

    private async Task<HttpResponseMessage> PostProtoBuf<TRequest>(
        string url,
        string clientVersion,
        TRequest requestParam,
        Action<TRequest, Common> commonParamSetter,
        CancellationToken stoppingToken = default)
        where TRequest : class, IMessage<TRequest>
    {
        // https://github.com/Starry-OvO/aiotieba/issues/67#issuecomment-1376006123
        // https://github.com/MoeNetwork/wmzz_post/blob/80aba25de46f5b2cb1a15aa2a69b527a7374ffa9/wmzz_post_setting.php#L64
        commonParamSetter(requestParam, new() {ClientVersion = clientVersion, ClientType = 2});

        // https://github.com/dotnet/runtime/issues/22996 http://test.greenbytes.de/tech/tc2231
        using var protoBufFile = new ByteArrayContent(requestParam.ToByteArray());
        protoBufFile.Headers.Add("Content-Disposition", "form-data; name=\"data\"; filename=\"file\"");
#pragma warning disable IDE0028 // Simplify collection initialization
        using var content = new MultipartFormDataContent();
#pragma warning restore IDE0028 // Simplify collection initialization
        content.Add(protoBufFile);

        // https://stackoverflow.com/questions/30926645/httpcontent-boundary-double-quotes
        var boundary = content.Headers.ContentType?.Parameters.First(header => header.Name == "boundary");
        if (boundary != null) boundary.Value = boundary.Value?.Replace("\"", "");

#pragma warning disable CC0008 // Use object initializer
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
#pragma warning restore CC0008 // Use object initializer
        request.Content = content;
        _ = request.Headers.UserAgent.TryParseAdd($"bdtb for Android {clientVersion}");
        request.Headers.Add("x_bd_data_type", "protobuf");
        request.Headers.Accept.ParseAdd("*/*");
        request.Headers.Connection.Add("keep-alive");

        return await Post(http => http.SendAsync(request, stoppingToken),
            () => logger.LogTrace("POST {} {}", url, requestParam), stoppingToken);
    }

    private async Task<HttpResponseMessage> Post(
        Func<HttpClient, Task<HttpResponseMessage>> responseTaskFactory,
        Action logTraceAction,
        CancellationToken stoppingToken = default)
    {
#pragma warning disable IDISP001 // Dispose created
        var http = httpFactory.CreateClient("tbClient");
#pragma warning restore IDISP001 // Dispose created
        await requesterTcs.Wait(stoppingToken);
        if (_config.GetValue("LogTrace", false)) logTraceAction();
        var ret = responseTaskFactory(http);
#pragma warning disable AV2235 // Call to Task.ContinueWith should be replaced with an await expression
        _ = ret.ContinueWith(task =>
#pragma warning restore AV2235 // Call to Task.ContinueWith should be replaced with an await expression
        {
            // ReSharper disable once MergeIntoPattern
#pragma warning disable SS034 // Use await to get the result of an asynchronous operation
            if (task.IsCompletedSuccessfully && task.Result.IsSuccessStatusCode) requesterTcs.Increase();
#pragma warning restore SS034 // Use await to get the result of an asynchronous operation
            else requesterTcs.Decrease();
        }, stoppingToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return await ret;
    }
}
