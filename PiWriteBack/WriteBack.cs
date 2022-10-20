using Cognite.Extractor.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OSIsoft.AF.Time;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Cognite.PiWriteBack
{
    internal class WriteBack : BaseExtractor<FullConfig>
    {
        private readonly ILogger<WriteBack> _log;
        private PiServer _piServer;
        private Cdf _cdf;
        private DataPointsExtractor _extractor;

        public WriteBack(
            FullConfig config,
            IServiceProvider provider,
            CogniteDestination destination,
            ExtractionRun run)
            : base(config, provider, destination, run)
        {
            if (run != null)
            {
                run.Continuous = true;
            }
            _log = provider.GetRequiredService<ILogger<WriteBack>>();

            // Create the required services within the current scope
            _piServer = provider.GetRequiredService<PiServer>();
            _cdf = provider.GetRequiredService<Cdf>();
            _extractor = provider.GetRequiredService<DataPointsExtractor>();
        }

        protected override async Task Start()
        {
            _log.LogInformation("Starting PI Write Back");

            // Initialize connections to the PI server and to CDF
            await _piServer.Init();
            await _cdf.Init(Source.Token);

            // Find or create the tags in the PI server
            _piServer.CreateTags();

            // Run the extractor
            await _extractor.Run(Source.Token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //_source.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
