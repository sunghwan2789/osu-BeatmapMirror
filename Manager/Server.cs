using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Manager
{
    partial class Server : ServiceBase
    {
        public Server()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // 동기화 봇
            Task.Run(() => RunBot());

            // 웹소켓 서버
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://" + Settings.Prefix);
            Listener.Prefixes.Add("https://" + Settings.Prefix);
            Listener.Start();

            Listen();
        }

        private void RunBot()
        {
            while (true)
            {
                var scheduler = Task.Delay(Settings.SyncInterval);
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bot.exe"),
                        Arguments = "manage",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                process.Start();
                process.WaitForExit();
                process.Dispose();

                try
                {
                    var output = File.ReadAllText(Settings.LogPath + ".bot.log");
                    Log.Write("=========== BEATMAP SYNC PROCESS\r\n" + output);
                    Log.Write("BEATMAP SYNC PROCESS ===========");
                }
                catch {}
                scheduler.Wait();
                scheduler.Dispose();
            }
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

        protected override void OnStop()
        {
            Listener.Close();
        }
    }
}
