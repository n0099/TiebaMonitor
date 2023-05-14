using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

        var imageRequesterConfig = context.Configuration.GetSection("ImageRequester");
        service.AddHttpClient("tbImage", client =>
            {
                client.BaseAddress = new("https://imgsrc.baidu.com/forum/pic/item/");
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .SetHandlerLifetime(TimeSpan.FromSeconds(imageRequesterConfig.GetValue("HandlerLifetimeSec", 600))) // 10 mins
            .AddPolicyHandler((provider, request) => HttpPolicyExtensions.HandleTransientHttpError()
                .RetryForeverAsync((outcome, tryCount, _) =>
                {
                    var failReason = outcome.Exception == null ? "HTTP " + outcome.Result.StatusCode : "exception" + outcome.Exception;
                    provider.GetRequiredService<ILogger<ImageRequester>>().LogWarning(
                        "Fetch for {} failed due to {} after {} retries, still trying until reach the configured HttpClient.TimeoutMs.",
                        request.RequestUri, failReason, tryCount);
                }))
            // https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory/abbe6d767681098c957ee6b6bee656197b7d03b4#use-case-applying-timeouts
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(imageRequesterConfig.GetValue("TimeoutMs", 3000)));

        // https://stackoverflow.com/questions/52889827/remove-http-client-logging-handler-in-asp-net-core/52970073#52970073
        service.RemoveAll<IHttpMessageHandlerBuilderFilter>();
    }

    protected override void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterType<ImagePipelineDbContext>();
        builder.RegisterType<PaddleOcrRecognizerAndDetector>();
        builder.RegisterType<TesseractRecognizer>();
        builder.RegisterType<JoinedRecognizer>();
        builder.RegisterType<ImageOcrConsumer>();
        builder.RegisterType<ImageHashConsumer>();
        builder.RegisterType<ImageRequester>();
    }
}
