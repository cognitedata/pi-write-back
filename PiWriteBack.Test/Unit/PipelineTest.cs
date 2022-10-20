using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Cognite.PiWriteBack.Test.Unit
{
    public class PipelineTest : ConsoleWrapper
    {
        private readonly UnitTester _tester;
        public PipelineTest(ITestOutputHelper output) : base(output)
        {
            _tester = new UnitTester();
        }
    }
}

