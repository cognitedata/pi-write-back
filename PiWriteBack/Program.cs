using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace Cognite.PiWriteBack
{
    public class ExtractorParams
    {
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ConfigFile { get; set; }
        public string LogDir { get; set; }
        public string LogLevel { get; set; }
        public bool Service { get; set; }
        public string WorkingDir { get; set; }
    }

    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            return await GetCommandLineOptions().InvokeAsync(args);
        }

        private static Parser GetCommandLineOptions()
        {
            var rootCommand = new RootCommand
            {
                Description = "Cognite PI Write Back"
            };

            var option = new Option<string>("--host", "PI server hostname or IP address");
            rootCommand.AddOption(option);

            option = new Option<string>("--username", "PI server windows username");
            rootCommand.AddOption(option);

            option = new Option<string>("--password", "PI server windows password");
            rootCommand.AddOption(option);

            var flag = new Option("--service", "Run extractor as service");
            rootCommand.AddOption(flag);

            option = new Option<string>("--config-file", "Set path to .yml configuration file");
            option.AddAlias("-f");
            rootCommand.AddOption(option);

            option = new Option<string>("--log-level", "Set the console log-level [fatal/error/warning/information/debug/verbose]");
            option.AddAlias("-l");
            rootCommand.AddOption(option);

            option = new Option<string>("--working-dir", "Set the working directory of the extractor. Defaults to current directory for standalone," +
                " or one level above for service version");
            option.AddAlias("-w");
            rootCommand.AddOption(option);

            option = new Option<string>("--log-dir", "Set path to log files, enables logging to file");
            rootCommand.AddOption(option);

            rootCommand.Handler = CommandHandler.Create<ExtractorParams>(async setup =>
            {
                if (setup.Service)
                {
                    using (var service = new ExtractorService(setup))
                    {
                        ServiceBase.Run(service);
                    }
                }
                else
                {
                    await ExtractorStarter.Run(setup, CancellationToken.None, null, new ServiceCollection());
                }
            });

            return new CommandLineBuilder(rootCommand)
                .UseVersionOption()
                .UseHelp()
                .Build();
        }
    }
}
