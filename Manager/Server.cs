using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace Manager
{
    internal class Server : BackgroundService
    {
        public Server(IHostApplicationLifetime applicationLifetime, ILogger<Server> logger)
        {
            ApplicationLifetime = applicationLifetime;
            Logger = logger;
        }

        private IHostApplicationLifetime ApplicationLifetime { get; }
        private ILogger Logger { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            var listener = new HttpListener
            {
                IgnoreWriteExceptions = true,
            };
            foreach (var prefix in Settings.Prefix.Split(','))
            {
                listener.Prefixes.Add(prefix);
            }

            stoppingToken.Register(() => listener?.Stop());

            Logger.LogInformation("Start listening.");
            listener.Start();
            try
            {
                while (listener.IsListening && !stoppingToken.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync();
                    if (!context.Request.IsWebSocketRequest ||
                        (Settings.TLSOnly && !context.Request.IsSecureConnection))
                    {
                        if (string.IsNullOrEmpty(Settings.Fallback))
                        {
                            context.Response.StatusCode = 400;
                        }
                        else
                        {
                            context.Response.Redirect(Settings.Fallback);
                        }
                        context.Response.Close();
                        continue;
                    }

                    try
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null);
                        //Log.Write(wsContext.WebSocket.GetHashCode() + " AcceptWebSocketAsync");
                        new Client(wsContext.WebSocket).Listen();
                    }
                    catch { }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occured while listening.");
            }
            finally
            {
                Logger.LogInformation("Stopping listening.");
                listener.Close();
                listener = null;
                ApplicationLifetime.StopApplication();
            }
        }
    }
}