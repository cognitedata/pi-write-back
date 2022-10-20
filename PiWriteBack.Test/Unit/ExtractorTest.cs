using Cognite.Extractor.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Cognite.PiWriteBack.Test.Unit
{
    public class ExtractorTest : ConsoleWrapper
    {
        private readonly UnitTester _tester;
        public ExtractorTest(ITestOutputHelper output) : base(output)
        {
            _tester = new UnitTester();
        }

        [Fact(Timeout = 5000)]
        public async Task SingleRun()
        {
            using (var extractor = new WriteBack(_tester.Config, _tester.Provider,
                _tester.Provider.GetRequiredService<CogniteDestination>(), null))
            {
                await Utils.RunWithTimeout(() => extractor.Start(_tester.Source.Token), 2);
            }
        }

        [Fact(Timeout = 10_000)]
        public async Task AutomaticUpdates()
        {
            using (var extractor = new WriteBack(_tester.Config, _tester.Provider,
                _tester.Provider.GetRequiredService<CogniteDestination>(), null))
            {
                var runTask = extractor.Start(_tester.Source.Token);
                await Utils.RunWithTimeout(runTask, 2);
            }
        }
    }
}
