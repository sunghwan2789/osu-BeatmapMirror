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

                    Logger.LogInformation($"Start");
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bot.exe"),
                        Arguments = "manage",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    });
                    stoppingToken.Register(() => process?.CloseMainWindow());
                    process.WaitForExit();

                    try
                    {
                        var data = File.ReadAllText($"{Settings.LogPath}.bot.log");
                        File.Delete(data);
                        Logger.LogInformation(data);
                    }
                    catch { }

                    Logger.LogInformation($"Exited");

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