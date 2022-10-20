using Cognite.Extractor.Configuration;
using Cognite.Extractor.Logging;
using Cognite.Extractor.Utils;
using CogniteSdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OSIsoft.AF.Asset;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Cognite.PiWriteBack.Test.Unit
{
    internal class UnitTester 
    {
        public FullConfig Config { get; }
        public ServiceProvider Provider { get; }
        public CDFMock Handler { get; }
        private readonly ServiceCollection _services = new ServiceCollection();

        public CancellationTokenSource Source { get; } = new CancellationTokenSource();

        public UnitTester()
        {
            Config = _services.AddConfig<FullConfig>("config.unit-test.yml", 1);
            _services.AddLogger();
            _services.AddSingleton<CDFMock>();
            _services.AddHttpClient<Client.Builder>()
                .ConfigurePrimaryHttpMessageHandler(provider => provider.GetRequiredService<CDFMock>().CreateHandler());
            _services.AddCogniteClient("appid", null, true, true, false);

            Provider = _services.BuildServiceProvider();
            Handler = Provider.GetRequiredService<CDFMock>();
        }
    }
}
