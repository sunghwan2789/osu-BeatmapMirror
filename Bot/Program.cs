using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Console;
using System.Threading.Tasks;
using Utility;

namespace Bot
{
    internal static class Program
    {
        private static Task Main(string[] args)
        {
            return CreateHostBuilder(args).Build().RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseConsoleLifetime()
                .ConfigureLogging(logging =>
                {
                    logging.Services.Configure<ConsoleLoggerOptions>(options =>
                    {
                        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(new CommandLineArgs { Args = args });
                    services.AddSingleton<OsuLegacyClient>();
                    services.AddSingleton<Requests>();
                    services.AddHostedService<App>();
                });
    }

    public class CommandLineArgs
    {
        public string[] Args { get; set; }
    }
}