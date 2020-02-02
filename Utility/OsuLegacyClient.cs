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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utility
{
    public class OsuLegacyClient
    {
        private const string COOKIE_SESSION = "phpbb3_2cjk5_sid";
        private const string COOKIE_USER_NUMBER = "phpbb3_2cjk5_u";
        private const string COOKIE_USER_NAME = "last_login";

        private static Uri BaseAddress => new Uri("https://osu.ppy.sh/");

        private CookieContainer CookieContainer { get; }
        private CloudFlareHandler Handler { get; }
        private HttpClient Client { get; }

        public string Session => GetCookie(COOKIE_SESSION);
        public string UserNumber => GetCookie(COOKIE_USER_NUMBER);
        public string UserName => GetCookie(COOKIE_USER_NAME);
        public bool IsAuthenticated { get; private set; }

        public static OsuLegacyClient Context { get; } = new OsuLegacyClient();

        public OsuLegacyClient()
        {
            CookieContainer = new CookieContainer();
            AddCookie("osu_site_v", "old");

            Handler = new CloudFlareHandler(new HttpClientHandler
            {
                CookieContainer = CookieContainer,
            });

            Client = new HttpClient(Handler)
            {
                BaseAddress = BaseAddress,
                Timeout = Settings.ResponseTimeout,
            };
            Client.DefaultRequestHeaders.Add("Referer", BaseAddress.AbsoluteUri);
        }

        public void AddCookie(string name, string content)
        {
            CookieContainer.Add(new Cookie(name, content, "/", BaseAddress.Host));
        }

        public string GetCookie(string name)
        {
            return CookieContainer.GetCookies(BaseAddress)[name]?.Value;
        }

        public Task<bool> LoginAsync(string id, string pw)
        {
            return LoginAsync(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("login", "Login"),
                new KeyValuePair<string, string>("username", id),
                new KeyValuePair<string, string>("password", pw),
                new KeyValuePair<string, string>("autologin", "on"),
                new KeyValuePair<string, string>("redirect", "/p/beatmaplist"),
            });
        }

        public Task<bool> LoginAsync(string sid)
        {
            AddCookie(COOKIE_SESSION, sid);
            return LoginAsync(new List<KeyValuePair<string, string>>());
        }

        private async Task<bool> LoginAsync(IEnumerable<KeyValuePair<string, string>> data)
        {
            using (var response = await Client.PostAsync("forum/ucp.php?mode=login", new FormUrlEncodedContent(data)))
            {
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                IsAuthenticated = CheckAuthenticated(content);
                return IsAuthenticated;
            }

            bool CheckAuthenticated(string content) => !content.Contains("incorrect password");
        }

        public async Task LogoutAsync()
        {
            using (var response = await Client.GetAsync($"forum/ucp.php?mode=logout&sid={Session}"))
            {
                response.EnsureSuccessStatusCode();
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
        /// <exception cref="HttpRequestException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="SharpZipBaseException">올바른 비트맵 파일이 아님.</exception>
        public async Task<string> DownloadAsync(int id, IProgress<(int, long)> onprogress, bool skipDownload = false)
        {
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

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var response = await Client.GetAsync($"d/{id}", HttpCompletionOption.ResponseHeadersRead))
            using (var data = await response.Content.ReadAsStreamAsync())
            {
                response.EnsureSuccessStatusCode();

                int got;
                var received = 0;
                var buffer = new byte[4096];
                var total = response.Content.Headers.ContentLength.Value;
                onprogress?.Report((received, total));
                while ((got = await data.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, got);
                    received += got;
                    onprogress?.Report((received, total));
                }
            }
            return await DownloadAsync(id, onprogress, true);
        }

        public async Task<JArray> GetBeatmapsAPIAsync(string query)
        {
            try
            {
                using (var response = await Client.GetAsync($"api/get_beatmaps?k={Settings.APIKey}&{query}"))
                {
                    response.EnsureSuccessStatusCode();

                    return JArray.Parse(await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException || e is JsonReaderException)
            {
                return await GetBeatmapsAPIAsync(query);
            }
        }

        public async Task<IEnumerable<int>> GrabSetIDFromBeatmapListAsync(int r, int page = 1)
        {
            try
            {
                using (var response = await Client.GetAsync($"p/beatmaplist?r={r}&page={page}"))
                {
                    response.EnsureSuccessStatusCode();

                    var data = await response.Content.ReadAsStringAsync();
                    return Regex.Matches(data, Settings.SetIdExpression).Cast<Match>()
                        .Select(setId => int.Parse(setId.Groups[1].Value));
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException)
            {
                return await GrabSetIDFromBeatmapListAsync(r, page);
            }
        }
    }
}
