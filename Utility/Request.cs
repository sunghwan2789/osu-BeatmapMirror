using CloudFlareUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class Request
    {
        public HttpClient Client;
        public CookieContainer Cookie;
        public ClearanceHandler Handler;

        public static Request Context { get; set; }

        static Request()
        {
            Context = new Request();
        }

        public Request()
        {
            Cookie = new CookieContainer();
            Cookie.Add(new System.Net.Cookie("osu_site_v", "old", "/", "osu.ppy.sh"));

            Handler = new ClearanceHandler
            {
                InnerHandler = new HttpClientHandler
                {
                    CookieContainer = Cookie,
                },
            };

            Client = new HttpClient(Handler);
        }

        public HttpWebRequest Create(string url, bool post = false)
        {
            var wr = WebRequest.CreateHttp(url);
            wr.ServicePoint.Expect100Continue = false;
            wr.CookieContainer = Cookie;
            wr.Timeout = Settings.ResponseTimeout;
            wr.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.BypassCache);
            if (post)
            {
                wr.Method = "POST";
                wr.ContentType = "application/x-www-form-urlencoded";
            }
            return wr;
        }

        public void AddCookie(string name, string content)
        {
            Cookie.Add(new Cookie(name, content, "/", "osu.ppy.sh"));
        }

        public string GetCookie(string name)
        {
            return Cookie.GetCookies(new Uri("https://osu.ppy.sh"))[name]?.Value;
        }

        private bool LoginValidate(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                return false;
            }

            return cookies.Any(cookie => cookie.Contains("last_login"));
        }

        public async Task<string> LoginAsync(string id, string pw)
        {
            using (var response = await Client.PostAsync($"https://osu.ppy.sh/forum/ucp.php?mode=login", new FormUrlEncodedContent(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("login", "Login"),
                new KeyValuePair<string, string>("username", id),
                new KeyValuePair<string, string>("password", pw),
                new KeyValuePair<string, string>("autologin", "on"),
            })))
            {
                return LoginValidate(response) ? GetCookie(Settings.SessionKey) : null;
            }
        }

        public async Task<string> LoginAsync(string sid)
        {
            AddCookie(Settings.SessionKey, sid);

            using (var response = await Client.PostAsync($"https://osu.ppy.sh/forum/ucp.php?mode=login", new FormUrlEncodedContent(new KeyValuePair<string, string>[]
            {
            })))
            {
                return LoginValidate(response) ? GetCookie(Settings.SessionKey) : null;
            }
        }

        private bool ValidateOsz(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var zs = new osu.Game.IO.Archives.ZipArchiveReader(fs))
            {
                return true;
            }
        }

        /// <summary>
        /// osu!에서 비트맵셋을 내려받고 올바른 파일인지 확인합니다.
        /// </summary>
        /// <param name="id">비트맵셋 ID</param>
        /// <param name="onprogress"><code>(received, total) => { ... }</code></param>
        /// <param name="skipDownload">파일을 내려받고 검증할 때 <code>true</code></param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="SharpZipBaseException">올바른 비트맵 파일이 아님.</exception>
        public async Task<string> DownloadAsync(int id, Action<int, long> onprogress, bool skipDownload = false)
        {
            var url = "https://osu.ppy.sh/d/" + id;
            var path = Path.Combine(Settings.Storage, id + ".osz.download");

            if (skipDownload)
            {
                if (File.Exists(path) && ValidateOsz(path))
                {
                    return path;
                }

                path = path.Remove(path.LastIndexOf(".download"));
                if (ValidateOsz(path))
                {
                    return path;
                }

                throw new Exception("올바른 비트맵 파일이 아님");
            }

            var wr = Create(url);
            using (var rp = (HttpWebResponse) wr.GetResponse())
            using (var rs = rp.GetResponseStream())
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                int got;
                var received = 0;
                var buffer = new byte[4096];
                onprogress?.Invoke(received, rp.ContentLength);
                while ((got = rs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, got);
                    received += got;
                    onprogress?.Invoke(received, rp.ContentLength);
                }
            }
            return await DownloadAsync(id, onprogress, true);
        }

        public async Task<JArray> GetBeatmapsAPIAsync(string query)
        {
            const string url = "https://osu.ppy.sh/api/get_beatmaps?k={0}&{1}";

            try
            {
                var wr = Create(string.Format(url, Settings.APIKey, query));
                using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
                {
                    return JArray.Parse(rp.ReadToEnd());
                }
            }
            catch (Exception e) when (e is WebException || e is JsonReaderException || e is IOException)
            {
                return await GetBeatmapsAPIAsync(query);
            }
        }
    }
}
