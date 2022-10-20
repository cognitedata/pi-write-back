using Cognite.Extractor.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Cognite.PiWriteBack.Test.Integration
{
    [Collection("IntegrationTests")]
    public class FullTest : ConsoleWrapper
    {
        private readonly IntegrationTestFixture _tester;
        public FullTest(IntegrationTestFixture tester, ITestOutputHelper output) : base(output)
        {
            _tester = tester;
        }

        private string ReformatJson(string raw)
        {
            return JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(raw), new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        [Fact(Timeout = 10000)]
        public async Task RunExtractor()
        {
            using (var extractor = new WriteBack(_tester.Config, _tester.Provider,
                _tester.Provider.GetRequiredService<CogniteDestination>(), null))
            {
                await Utils.RunWithTimeout(() => extractor.Start(CancellationToken.None), 5);
            }
            Assert.True(_tester.Handler.TimeSeries.ContainsKey("TimeSeries-External-ID-1"));
            var ts = _tester.Handler.TimeSeries["TimeSeries-External-ID-1"];

            Assert.Equal(5, ts.Count);

            var et1 = ts["test"];
            var str = Utils.JsonElementToString(et1);

            var reference = ReformatJson(File.ReadAllText("TimeSeriesTest1.json"));
            Assert.Equal(reference, str);
        }
    }
}
