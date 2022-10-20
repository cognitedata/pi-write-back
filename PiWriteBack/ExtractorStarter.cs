using Cognite.Extractor.Common;
using Cognite.Extractor.Logging;
using Cognite.Extractor.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cognite.PiWriteBack
{
    public class ExtractorStarter
    {
        private static string VerifyConfig(ILogger log, FullConfig config)
        {
            if (string.IsNullOrEmpty(config.Pi.Host)) return "Missing PI host";
            if (config.Cognite == null) return "Missing cognite config";

            return null;
        }

        private static void VerifyAndBuildConfig(
            ILogger log,
            FullConfig config,
            ExtractorParams setup,
            ExtractorRunnerParams<FullConfig, WriteBack> options)
        {
            if (!string.IsNullOrEmpty(setup.Host)) config.Pi.Host = setup.Host;
            if (!string.IsNullOrEmpty(setup.Username)) config.Pi.Username = setup.Username;
            if (!string.IsNullOrEmpty(setup.Password)) config.Pi.Password = setup.Password;
            if (!string.IsNullOrEmpty(setup.LogLevel)) config.Logger.Console = new ConsoleConfig { Level = setup.LogLevel };
            if (!string.IsNullOrEmpty(setup.LogDir))
            {
                if (config.Logger.File == null)
                {
                    config.Logger.File = new FileConfig { Level = "information", Path = setup.LogDir };
                }
                else
                {
                    config.Logger.File.Path = setup.LogDir;
                }
            }

            if (options != null)
            {
                options.Restart = true;
            }

            string configResult = VerifyConfig(log, config);
            if (configResult != null)
            {
                throw new ConfigurationException($"Invalid config: {configResult}");
            }
        }

        private static void SetWorkingDir(ExtractorParams setup)
        {
            string path = null;
            if (setup.WorkingDir != null)
            {
                path = setup.WorkingDir;
            }
            else if (setup.Service)
            {
                path = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.FullName;
            }
            if (path != null)
            {
                if (!Directory.Exists(path))
                {
                    throw new ConfigurationException($"Target directory does not exist: {path}");
                }
                Directory.SetCurrentDirectory(path);
            }
        }


        private static readonly Gauge version =
            Metrics.CreateGauge("opcua_version",
                $"version: {Extractor.Metrics.Version.GetVersion(Assembly.GetExecutingAssembly())}"
                + $", status: {Extractor.Metrics.Version.GetDescription(Assembly.GetExecutingAssembly())}");

        public static async Task Run(ExtractorParams setup, CancellationToken token, ILogger log, ServiceCollection services)
        {
            SetWorkingDir(setup);

            if (log == null)
            {
                log = LoggingUtils.GetDefault();
            }

            version.Set(0);

            var ver = Extractor.Metrics.Version.GetVersion(Assembly.GetExecutingAssembly());

            log.LogInformation("Starting PI Write Back version {Ver}: {Desc}",
                ver,
                Extractor.Metrics.Version.GetDescription(Assembly.GetExecutingAssembly()));

            services.AddScoped<PiServer>();
            services.AddScoped<Cdf>();
            services.AddScoped<WriteBackState>();
            services.AddScoped<DataPointsExtractor>();

            var options = new ExtractorRunnerParams<FullConfig, WriteBack>
            {
                ConfigPath = setup.ConfigFile ?? "config/config.yml",
                AcceptedConfigVersions = new[] { 1 },
                AppId = $"PI-Write Back:{ver}",
                UserAgent = $"CognitePIWriteBack/{ver}",
                AddStateStore = true,
                AddLogger = true,
                AddMetrics = true,
                Restart = false,
                ExtServices = services,
                StartupLogger = log,
                ConfigCallback = (config, opt) => VerifyAndBuildConfig(log, config, setup, opt),
                RequireDestination = true
            };

            await ExtractorRunner.Run(options, token);
        }
    }
}
