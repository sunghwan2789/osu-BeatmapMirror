using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace Manager
{
    class Client
    {
        private WebSocket Socket;
        private int Id;
        private OsuLegacyClient Request;

        public Client(WebSocket socket)
        {
            Socket = socket;
            Id = Socket.GetHashCode();
            Request = new OsuLegacyClient();
        }

        public async Task Listen()
        {
            try
            {
                var buffer = new byte[1024];
                var first = true;
                while (Socket.State == WebSocketState.Open)
                {
                    var payload = await Socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (payload.MessageType != WebSocketMessageType.Text)
                    {
                        break;
                    }

                    if (first)
                    {
                        await Process(Encoding.UTF8.GetString(buffer, 0, payload.Count));
                    }
                    first = payload.EndOfMessage;
                }
            }
            catch (Exception e)
            {
                //Log.Write(Id + " " + e.GetBaseException() + ": " + e.Message);
            }
            //Log.Write(Id + " Dispose");
            Socket.Dispose();
        }

        private async Task Process(string s)
        {
            try
            {
                Log.Write(Id + " RECV " + s);
                await Process(JObject.Parse(s));
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException)
            {
                Log.Write(Id + " " + e.GetBaseException() + ": " + e.Message);
                Send("error", "wait");
            }
        }

        private async Task Process(JObject request)
        {
            switch (request["action"].Value<string>())
            {
                case "login":
                {
                    var data = request["data"].Value<JObject>();

                    if (data["sid"] == null)
                    {
                        await LoginAsync(data["id"].Value<string>(), data["pw"].Value<string>());
                    }
                    else
                    {
                        await LoginAsync(data["sid"].Value<string>());
                    }
                    break;
                }
                case "logout":
                {
                    await LogoutAsync();
                    break;
                }
                case "upload":
                {
                    var id = request["data"].Value<JObject>()["id"].Value<int>();
                    await UploadAsync(id);
                    break;
                }
            }
        }

        private async Task LoginAsync(string id, string pw)
        {
            await LoginAsync(Request.LoginAsync(id, pw));
        }

        private async Task LoginAsync(string sid)
        {
            await LoginAsync(Request.LoginAsync(sid));
        }

        private async Task LoginAsync(Task<bool> loginTask)
        {
            if (await loginTask)
            {
                Send("login", new Dictionary<string, string>
                {
                    { "id", Request.UserNumber },
                    { "name", Request.UserName },
                    { "sid", Request.Session },
                });
            }
            else
            {
                Send("login", new Dictionary<string, string>
                {
                    { "error", "true" }
                });
            }
        }

        private async Task LogoutAsync()
        {
            await Request.LogoutAsync();
        }

        private async Task UploadAsync(int id)
        {
            // 이미 있는 맵인지 확인
            using (var query = DB.Command)
            {
                query.CommandText = "SELECT 1 FROM gosu_beatmaps WHERE setId = @i";
                query.Parameters.AddWithValue("@i", id);
                if (query.ExecuteScalar() != null)
                {
                    Send("upload", new Dictionary<string, string>
                    {
                        { "state", "rejected" },
                        { "detail", "exists" }
                    });
                    return;
                }
            }

            Send("upload", new Dictionary<string, string>
            {
                { "state", "fetching" }
            });

            // 비트맵 존재 확인
            var beatmap = (await Request.GetBeatmapsAPIAsync($"s={id}")).FirstOrDefault();
            if (beatmap == null)
            {
                Send("upload", new Dictionary<string, string>
                {
                    { "state", "rejected" },
                    { "detail", "nomap" }
                });
                return;
            }

            // 품질 관리
            if (beatmap["favourite_count"].Value<int>() < Settings.FavoriteMinimum)
            {
                Send("upload", new Dictionary<string, string>
                {
                    { "state", "rejected" },
                    { "detail", "favorite" }
                });
                return;
            }

            // 랭크된 비트맵은 자동 동기화함
            //if (beatmap["approved"].Value<int>() > 0)
            //{
            //    Send("upload", new Dictionary<string, string>
            //    {
            //        { "state", "rejected" },
            //        { "detail", "will" }
            //    });
            //    break;
            //}

            // 비로그인 유저는 내가 짬날 때 업로드
            if (!Request.IsAuthorized)
            {
                using (var query = DB.Command)
                {
                    query.CommandText = "INSERT INTO osu_custom_list SET id = @id " +
                        "ON DUPLICATE KEY UPDATE id = @id";
                    query.Parameters.AddWithValue("@id", id);
                    query.ExecuteNonQuery();
                }
                Send("upload", new Dictionary<string, object>
                {
                    { "state", "reserved" }
                });
                return;
            }

            try
            {
                var pushed = DateTime.Now;
                await Request.DownloadAsync(id, new Progress<(int received, long total)>(tuple =>
                {
                    var (received, total) = tuple;
                    if (received == 0)
                    {
                        Send("upload", new Dictionary<string, object>
                        {
                            { "state", "download" },
                            { "total", total }
                        });
                        return;
                    }
                    if ((DateTime.Now - pushed).TotalSeconds >= 3)
                    {
                        pushed = DateTime.Now;
                        Send("upload", new Dictionary<string, object>
                        {
                            { "state", "downloading" },
                            { "got", received }
                        });
                    }
                }));
                Send("upload", new Dictionary<string, string>
                {
                    { "state", "downloaded" }
                });

                using (var process = new Process
                {
                    StartInfo =
                    {
                        FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bot.exe"),
                        Arguments = id + "l",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                })
                {
                    process.Start();
                    process.WaitForExit();

                    var output = process.StandardOutput.ReadToEnd().Trim();
                    Send("upload", new Dictionary<string, string>
                    {
                        { "state", "finished" },
                        { "output",  output }
                    });
                }
            }
            catch (Exception e)
            {
                Log.Write(Id + " " + e.GetBaseException() + ": " + e.Message);
                Send("upload", new Dictionary<string, string>
                {
                    { "state", "failed" }
                });
            }
        }

        private void Send(string action, object data)
        {
            Send(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { "action", action },
                { "data", data }
            }));
        }

        private async void Send(string s)
        {
            try
            {
                Log.Write(Id + " SEND " + s);
                await Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s)), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                // 비트맵 업로드가 연결이 끊긴 상태에서도 작동하기 위함
                // Log.Write(Id + " " + e.GetBaseException() + ": " + e.Message);
            }
        }
    }
}
