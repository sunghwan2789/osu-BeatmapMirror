using Microsoft.Extensions.Hosting;
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
    internal class Server : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://" + Settings.Prefix);
            Listener.Prefixes.Add("https://" + Settings.Prefix);
            Listener.Start();

            Listen();
            return Task.CompletedTask;
        }

        private HttpListener Listener;

        private async void Listen()
        {
            while (Listener.IsListening)
            {
                var context = await Listener.GetContextAsync();
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

        public Task StopAsync(CancellationToken cancellationToken)
        {
            using (Listener)
            {
                Listener.Close();
                return Task.CompletedTask;
            }
        }
    }
}