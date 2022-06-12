using System.Net;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace tbm.Crawler
{
    internal class Program
    {
        public static ILifetimeScope Autofac { get; private set; } = null!;
        public static readonly List<string> RegisteredCrawlerLocks = new() {"thread", "threadLate", "reply", "subReply"};

        private static void Main()
        {
            var logger = LogManager.GetCurrentClassLogger();
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                logger.Error((Exception)args.ExceptionObject, "AppDomain.UnhandledException:");
            TaskScheduler.UnobservedTaskException += (_, args) =>
                logger.Error(args.Exception, "TaskScheduler.UnobservedTaskException:");
            try
            {
#pragma warning disable IDE0058 // Expression value is never used
                var host = Host.CreateDefaultBuilder()
                    .ConfigureLogging((_, logging) =>
                    {
                        logging.ClearProviders();
                        logging.AddNLog(new NLogProviderOptions() {RemoveLoggerFactoryFilter = false});
                    })
                    .ConfigureServices((context, service) =>
                    {
                        service.AddHostedService<MainCrawlWorker>();
                        service.AddHostedService<RetryCrawlWorker>();
                        var httpConfig = context.Configuration.GetSection("ClientRequester");
                        service.AddHttpClient("tbClient", client =>
                            {
                                client.BaseAddress = new("http://c.tieba.baidu.com");
                                client.Timeout = TimeSpan.FromMilliseconds(httpConfig.GetValue("TimeoutMs", 3000));
                            })
                            .SetHandlerLifetime(TimeSpan.FromSeconds(httpConfig.GetValue("HandlerLifetimeSec", 600))) // 10 mins
                            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler {AutomaticDecompression = DecompressionMethods.GZip});
                        service.RemoveAll<IHttpMessageHandlerBuilderFilter>(); // https://stackoverflow.com/questions/52889827/remove-http-client-logging-handler-in-asp-net-core/52970073#52970073
                    })
                    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                    .ConfigureContainer((ContainerBuilder builder) =>
                    {
                        builder.RegisterType<TbmDbContext>();
                        builder.RegisterType<ClientRequester>();
                        builder.RegisterType<ClientRequesterTcs>().SingleInstance();
                        RegisteredCrawlerLocks.ForEach(l =>
                            builder.RegisterType<CrawlerLocks>().Keyed<CrawlerLocks>(l).SingleInstance());
                        builder.RegisterType<UserParserAndSaver>();
                        builder.RegisterType<ThreadLateCrawlerAndSaver>();

                        var baseClassOfClassesToBeRegister = new List<Type>
                        {
                            typeof(BaseCrawler<,>), typeof(BaseCrawlFacade<,,,>),
                            typeof(BaseParser<,>), typeof(BaseSaver<>)
                        };
                        builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly()).Where(t =>
                            baseClassOfClassesToBeRegister.Any(c => c.IsSubTypeOfRawGeneric(t))).AsSelf();
                    })
                    .Build();
                Autofac = host.Services.GetAutofacRoot();
                host.Run();
#pragma warning restore IDE0058 // Expression value is never used
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Exception");
            }
            finally
            {
                LogManager.Shutdown();
            }
        }
    }
}
