using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Manager
{
    class Client
    {
        private WebSocket Socket;
        private int Id;
        private Request Request;

        public Client(WebSocket socket)
        {
            Socket = socket;
            Id = Socket.GetHashCode();
            Request = new Request();
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
                        Process(Encoding.UTF8.GetString(buffer, 0, payload.Count));
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

        private void Process(string s)
        {
            try
            {
                Log.Write(Id + " RECV " + s);
                Process(JObject.Parse(s));
            }
            catch (WebException e)
            {
                Log.Write(Id + " " + e.GetBaseException() + ": " + e.Message);
                Send("error", "wait");
            }
        }

        private void Process(JObject request)
        {
            switch (request["action"].Value<string>())
            {
                case "login":
                {
                    const string url = "http://osu.ppy.sh/forum/ucp.php?mode=login";

                    var wr = Request.Create(url, true);
                    var data = request["data"].Value<JObject>();
                    if (data["sid"] == null)
                    {
                        using (var sw = new StreamWriter(wr.GetRequestStream()))
                        {
                            sw.Write(string.Format("login=Login&username={0}&password={1}&autologin=on",
                                Uri.EscapeDataString(data["id"].Value<string>()),
                                Uri.EscapeDataString(data["pw"].Value<string>())));
                        }
                    }
                    else
                    {
                        Request.AddCookie(Settings.SessionKey, data["sid"].Value<string>());
                    }
                    using (var sr = new StreamReader(wr.GetResponse().GetResponseStream()))
                    {
                        var sessionGrab = Regex.Match(sr.ReadToEnd(), Settings.SessionExpression);
                        Send("login", sessionGrab.Success ?
                            new Dictionary<string, string>
                            {
                                { "id", sessionGrab.Groups["id"].Value },
                                { "name", sessionGrab.Groups["name"].Value },
                                { "sid", Request.GetCookie(Settings.SessionKey) }
                            } :
                            new Dictionary<string, string>
                            {
                                { "error", "true" }
                            });
                    }
                    break;
                }
                case "logout":
                {
                    const string url = "http://osu.ppy.sh/forum/ucp.php?mode=logout&sid=";

                    var session = Request.GetCookie(Settings.SessionKey);
                    if (session != null)
                    {
                        Request.Create(url + session).GetResponse().Dispose();
                    }
                    break;
                }
                case "upload":
                {
                    var id = request["data"].Value<JObject>()["id"].Value<int>();

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
                            break;
                        }
                    }

                    Send("upload", new Dictionary<string, string>
                    {
                        { "state", "fetching" }
                    });

                    // 맵퍼 본인 확인 절차
                    string mid, mname;
                    var wr = Request.Create("http://osu.ppy.sh/s/" + id);
                    using (var sr = new StreamReader(wr.GetResponse().GetResponseStream()))
                    {
                        var creatorGrab = Regex.Match(sr.ReadToEnd(), Settings.CreatorExpression);
                        if (!creatorGrab.Success)
                        {
                            Send("upload", new Dictionary<string, string>
                            {
                                { "state", "rejected" },
                                { "detail", "nomap" }
                            });
                            break;
                        }
                        var beatmap = GetAPIData("s=" + id).First();
                        // 품질 관리
                        if (beatmap["favourite_count"].Value<int>() < Settings.FavoriteMinimum)
                        {
                            Send("upload", new Dictionary<string, string>
                            {
                                { "state", "rejected" },
                                { "detail", "favorite" }
                            });
                            break;
                        }
                        // 랭크된 비트맵은 자동 동기화함
                        if (beatmap["approved"].Value<int>() > 0)
                        {
                            Send("upload", new Dictionary<string, string>
                            {
                                { "state", "rejected" },
                                { "detail", "will" }
                            });
                            break;
                        }

                        mid = creatorGrab.Groups["id"].Value;
                        mname = creatorGrab.Groups["name"].Value;
                    }

                    // 비로그인 유저는 내가 짬날 때 업로드
                    if (Request.GetCookie(Settings.SessionKey) == null)
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
                        break;
                    }

                    try
                    {
                        var pushed = DateTime.Now;
                        Request.Download(id, (received, total) =>
                        {
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
                        });
                        Send("upload", new Dictionary<string, string>
                        {
                            { "state", "downloaded" }
                        });

                        var process = new Process
                        {
                            StartInfo =
                            {
                                FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bot.exe"),
                                Arguments = id + "l",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                RedirectStandardOutput = true
                            }
                        };
                        process.Start();
                        process.WaitForExit();

                        var output = process.StandardOutput.ReadToEnd().Trim();
                        Send("upload", new Dictionary<string, string>
                        {
                            { "state", "finished" },
                            { "output",  output }
                        });
                    }
                    catch (Exception e)
                    {
                        Log.Write(Id + " " + e.GetBaseException() + ": " + e.Message);
                        Send("upload", new Dictionary<string, string>
                        {
                            { "state", "failed" }
                        });
                    }
                    break;
                }
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

        private static JArray GetAPIData(string query)
        {
            const string url = "http://osu.ppy.sh/api/get_beatmaps?k={0}&{1}";

            try
            {
                var wr = new Request().Create(string.Format(url, Settings.APIKey, query));
                using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
                {
                    return JArray.Parse(rp.ReadToEnd());
                }
            }
            catch (WebException)
            {
                return GetAPIData(query);
            }
        }
    }
}
