using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Console;
using System.Threading.Tasks;

namespace Manager
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
                .UseWindowsService()
                .ConfigureLogging(logging =>
                {
                    logging.Services.Configure<ConsoleLoggerOptions>(options =>
                    {
                        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<BotService>();
                    services.AddHostedService<Server>();
                });
    }
}