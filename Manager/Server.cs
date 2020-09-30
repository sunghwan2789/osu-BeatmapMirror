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
        private IHostApplicationLifetime ApplicationLifetime { get; }
        private ILogger Logger { get; }

        public Server(IHostApplicationLifetime applicationLifetime, ILogger<Server> logger)
        {
            ApplicationLifetime = applicationLifetime;
            Logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Release current synchronization context for next services.
            await Task.Yield();

            using var listener = new HttpListener
            {
                IgnoreWriteExceptions = true,
            };

            foreach (var prefix in Settings.Prefix.Split(','))
            {
                listener.Prefixes.Add($"http://{prefix}");
                listener.Prefixes.Add($"https://{prefix}");
            }

            Logger.LogInformation("Start listening.");
            listener.Start();

            try
            {
                using var _ = stoppingToken.Register(() => listener.Stop());

                while (listener.IsListening && !stoppingToken.IsCancellationRequested)
                {
                    await ProcessRequestAsync(await listener.GetContextAsync());
                }
            }
            // Catch cancellations and just log it.
            catch (Exception ex) when (
                (ex is HttpListenerException && stoppingToken.IsCancellationRequested)
                || (ex is OperationCanceledException))
            {
                Logger.LogInformation("Shutdown listener due to cancellation.");
            }
            finally
            {
                Logger.LogInformation("Stopping listening.");
                listener.Close();
                ApplicationLifetime.StopApplication();
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            if (!context.Request.IsWebSocketRequest
                || (Settings.TLSOnly && !context.Request.IsSecureConnection))
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
                return;
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
}