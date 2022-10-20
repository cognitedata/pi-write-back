using Cognite.Extractor.Configuration;
using Cognite.Extractor.Logging;
using Cognite.Extractor.Utils;
using CogniteSdk;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace Cognite.PiWriteBack.Test.Integration
{
    [CollectionDefinition("IntegrationTests")]
    public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture> { }

    public class IntegrationTestFixture
    {
        public FullConfig Config { get; }
        public ServiceProvider Provider { get; }
        public CDFMock Handler { get; }
        private readonly ServiceCollection _services = new ServiceCollection();
        public IntegrationTestFixture()
        {
            Config = _services.AddConfig<FullConfig>("config.integration-test.yml", 1);
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
