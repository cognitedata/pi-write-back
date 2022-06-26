using Cognite.Extractor.Common;
using Cognite.Extractor.Logging;
using Cognite.Extractor.StateStorage;
using Cognite.Extractor.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PiWriteBack
{
    public class Program
    {
        private static ILogger _logger;

        public static async Task Main(string[] args)
        {
            // Create a cancelation token and run the application
            using (var source = new CancellationTokenSource())
            {
                _logger = LoggingUtils.GetDefault();
                Console.CancelKeyPress += (sender, eArgs) =>
                {
                    _logger.LogWarning("Ctrl-C pressed: Cancelling tasks");
                    eArgs.Cancel = true;
                    source.Cancel();
                };
                await Run(source.Token).ConfigureAwait(false);
            }
        }

        public static async Task Run(CancellationToken token) {

            var services = new ServiceCollection();
            
            // Read the configuration file and configure the services
            // to be injected
            try
            {
                services.ConfigureServices("./config/config.yml");
            }
            catch (Exception e)
            {
                _logger.LogError("Could not load config file. Exiting: {Message}", e.Message);
                return;
            }
            using (var provider = services.BuildServiceProvider())
            {
                _logger = provider.GetRequiredService<ILogger<Program>>();

                _logger.LogInformation("Starting PI Write Back...");

                // Create a loop that restarts the scoped services in case
                // of any errors.
                while (!token.IsCancellationRequested)
                {
                    await ExtractionLoop(provider, token).ConfigureAwait(false);
                }
                _logger.LogInformation("Pi Write Back finished. Exiting...");
            }
        }

        public static async Task ExtractionLoop(
            ServiceProvider provider, 
            CancellationToken token)
        {
            using (var scope = provider.CreateScope())
            {
                try
                {
                    // Create the required services within the current scope
                    var piServer = scope.ServiceProvider.GetRequiredService<PiServer>();
                    var cdf = scope.ServiceProvider.GetRequiredService<Cdf>();
                    var extractor = scope.ServiceProvider.GetRequiredService<DataPointsExtractor>();

                    // Initialize connections to the PI server and to CDF
                    await piServer.Init();
                    await cdf.Init(token);

                    // Find or create the tags in the PI server
                    piServer.CreateTags();

                    // Run the extractor
                    await extractor.Run(token);
                }
                catch (Exception e)
                {
                    // TODO: Should handle specific exceptions here and decide to
                    // break the main loop or not.
                    _logger.LogError(e, "Extraction error");
                    if (!token.IsCancellationRequested)
                    {
                        // Always restart the application, unless cancelled
                        var delay = TimeSpan.FromSeconds(10);
                        _logger.LogWarning("Restarting in {time} seconds", delay.TotalSeconds);
                        await Task.Delay(delay, token);
                    }
                }
            }
        }
    }

    public static class ServicesExtensions
    {
        public static void ConfigureServices(this ServiceCollection services, string configPath)
        {
            var config = services.AddConfig<Config>(configPath, 1);
            services.AddSingleton(config.Pi);
            services.AddSingleton(config.TimeSeries);
            services.AddLogger();
            services.AddStateStore();
            services.AddCogniteClient(
                "PI Write Back",
                userAgent: "PiWriteBack/1.0.0 (Cognite)",
                setLogger: true,
                setMetrics: true);
            services.AddScoped<PiServer>();
            services.AddScoped<Cdf>();
            services.AddScoped<WriteBackState>();
            services.AddScoped<DataPointsExtractor>();
        }
    }
}
