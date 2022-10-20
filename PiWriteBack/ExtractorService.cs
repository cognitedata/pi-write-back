using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.EventLog;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Cognite.PiWriteBack
{
    public class ExtractorService : ServiceBase
    {
        private const string _eventLogSource = "PiWriteBackService";
        private readonly EventLogLoggerProvider _loggerProvider;
        private CancellationTokenSource _source;

        private Task _runTask;
        private readonly ExtractorParams _setup;
        private Timer _timer;

        public ExtractorService(ExtractorParams setup)
        {
            var settings = new EventLogSettings
            {
                LogName = "Application",
                SourceName = _eventLogSource
            };
            _loggerProvider = new EventLogLoggerProvider(settings);
            _setup = setup;
            ServiceName = "PiWriteBackService";
        }

        protected override void OnStart(string[] args)
        {
            _source = new CancellationTokenSource();

            var eventLog = _loggerProvider.CreateLogger("PiWriteBackService");

            _runTask = ExtractorStarter.Run(
                _setup,
                _source.Token,
                eventLog,
                new ServiceCollection());

            _timer = new Timer(state =>
            {
                if (_runTask != null && _runTask.IsCompleted)
                {
                    if (_runTask.Exception != null)
                    {
                        eventLog.LogError("Extractor crashed unexpectedly: {Message}", _runTask.Exception.Message);
                    }
                    else
                    {
                        eventLog.LogInformation("Extractor stopped on its own");
                    }
                    Stop();
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        protected override void OnStop()
        {
            _source?.Cancel();
            _runTask?.Wait();
            _runTask = null;
            _timer?.Dispose();
            _timer = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source?.Cancel();
                _source?.Dispose();
                _source = null;
            }
            base.Dispose(disposing);
        }
    }
}