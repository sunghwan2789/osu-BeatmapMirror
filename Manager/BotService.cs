﻿using Microsoft.Extensions.Hosting;
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
        private ILogger Logger { get; }

        public BotService(ILogger<BotService> logger)
        {
            Logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var scheduler = Task.Delay(Settings.SyncInterval, stoppingToken);

                    Logger.LogInformation("Start bot.");
                    var stopwatch = Stopwatch.StartNew();
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bot.exe"),
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    });
                    var output = new StringBuilder();

                    using (stoppingToken.Register(() => process.CloseMainWindow()))
                    {
                        try
                        {
                            await Task.WhenAll(
                                process.OutputReadToEndAsync(output),
                                process.WaitForExitAsync()
                            );
                        }
                        catch (OperationCanceledException) { }
                    }

                    stopwatch.Stop();

                    Logger.LogInformation($"Bot have run for {stopwatch.Elapsed}.{Environment.NewLine}{output}");

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