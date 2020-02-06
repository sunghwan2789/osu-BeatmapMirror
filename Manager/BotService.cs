using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utility;
using Utility.Extensions;

namespace Manager
{
    internal class BotService : BackgroundService
    {
        public BotService(ILogger<BotService> logger)
        {
            Logger = logger;
        }

        private ILogger Logger { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var scheduler = Task.Delay(Settings.SyncInterval, stoppingToken);

                    Logger.LogInformation("Start");
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bot.exe"),
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    });
                    var output = new StringBuilder();

                    stoppingToken.Register(() => process?.CloseMainWindow());
                    await Task.WhenAll(
                        process.WaitForExitAsync(stoppingToken),
                        process.OutputReadToEndAsync(output, stoppingToken)
                    );

                    process.Dispose();
                    process = null;
                    Logger.LogInformation($"Exited{Environment.NewLine}{output}");

                    await scheduler;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception occured while running service.");
            }
        }
    }
}