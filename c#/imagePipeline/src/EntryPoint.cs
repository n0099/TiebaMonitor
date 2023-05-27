using System.Net;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;

#pragma warning disable IDE0058

namespace tbm.ImagePipeline;

public class EntryPoint : BaseEntryPoint
{
    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection service)
    {
        service.AddHostedService<ImagePipelineWorker>();
        service.AddHostedService<ImageBatchProducingWorker>();

        var imageRequesterConfig = context.Configuration.GetSection("ImageRequester");
        service.AddHttpClient("tbImage", client =>
            {
                client.BaseAddress = new("https://imgsrc.baidu.com/forum/pic/item/");
                client.Timeout = Timeout.InfiniteTimeSpan; // overall timeout, will sum up after each retry by polly
                client.DefaultRequestVersion = HttpVersion.Version20;
            })
            .SetHandlerLifetime(TimeSpan.FromSeconds(imageRequesterConfig.GetValue("HandlerLifetimeSec", 600))); // 10 mins

        var registry = service.AddPolicyRegistry();
        // https://stackoverflow.com/questions/69749167/polly-handletransienthttperror-not-catching-httprequestexception/69750053#69750053
        void AddPolicyToRegistry<T>
            (PolicyBuilder<T> policyBuilder, Action<ILogger<ImageRequester>, string, DelegateResult<T>, int> logCallback) =>
            registry.Add($"tbImage<{typeof(T).Name}>", Policy.WrapAsync(
                policyBuilder.RetryForeverAsync((outcome, tryCount, context2) =>
                { // https://www.stevejgordon.co.uk/passing-an-ilogger-to-polly-policies
                    if (!(context2.TryGetValue("ILogger<ImageRequester>", out var o1)
                          && o1 is ILogger<ImageRequester> logger)) return;
                    if (!(context2.TryGetValue("imageUrlFilename", out var o2)
                          && o2 is string imageUrlFilename)) return;
                    logCallback(logger, imageUrlFilename, outcome, tryCount);
                }),
                // timeout for each retry, https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory/abbe6d767681098c957ee6b6bee656197b7d03b4#use-case-applying-timeouts
                Policy.TimeoutAsync<T>(imageRequesterConfig.GetValue("TimeoutMs", 3000))));
        AddPolicyToRegistry(HttpPolicyExtensions.HandleTransientHttpError(), (logger, imageUrlFilename, outcome, tryCount) =>
        {
            var failReason = outcome.Exception == null
                ? $"HTTP {outcome.Result.StatusCode}"
                : $"exception {outcome.Exception.GetType().FullName}";
            logger.LogWarning(outcome.Exception, "Fetch for image {} failed due to {} after {} retries, still trying...",
                imageUrlFilename, failReason, tryCount);
        });
        AddPolicyToRegistry(Policy<byte[]>.Handle<HttpRequestException>(), (logger, imageUrlFilename, outcome, tryCount) =>
            logger.LogWarning("Fetch for image {} failed after {} bytes downloaded due to {} after {} retries, still trying...",
                imageUrlFilename, outcome.Result.Length, $"exception {outcome.Exception.GetType().FullName}", tryCount));

        // https://stackoverflow.com/questions/52889827/remove-http-client-logging-handler-in-asp-net-core/52970073#52970073
        service.RemoveAll<IHttpMessageHandlerBuilderFilter>();
    }

    protected override void ConfigureContainer(HostBuilderContext context, ContainerBuilder builder)
    {
        builder.RegisterType<ImagePipelineDbContext>();
        builder.RegisterType<JoinedRecognizer>();
        builder.RegisterType<OcrConsumer>();
        builder.RegisterType<HashConsumer>();
        builder.RegisterType<MetadataConsumer>();
        builder.RegisterType<ImageRequester>();

        builder.Register(componentContext => Channel.CreateBounded<List<ImageWithBytes>>(
            new BoundedChannelOptions(
                componentContext.Resolve<IConfiguration>()
                    .GetSection("ImagePipeline")
                    .GetValue("MaxBufferedImageBatches", 8)
            ) {SingleReader = true, SingleWriter = true})
        ).SingleInstance();

        var config = context.Configuration.GetSection("OcrConsumer");
        var paddleOcr = builder.RegisterType<PaddleOcrRecognizerAndDetector>();
        if (!config.GetSection("PaddleOcr").GetValue("DisposeAfterEachBatch", false))
            paddleOcr.SingleInstance();
        var tesseract = builder.RegisterType<TesseractRecognizer>();
        if (!config.GetSection("Tesseract").GetValue("DisposeAfterEachBatch", false))
            tesseract.SingleInstance();
    }
}