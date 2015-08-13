using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Manager
{
    public class Request
    {
        public CookieContainer Cookie;

        public Request()
        {
            Cookie = new CookieContainer();
        }

        public HttpWebRequest Create(string url, bool post = false)
        {
            var wr = (HttpWebRequest) WebRequest.Create(url);
            wr.ServicePoint.Expect100Continue = false;
            wr.CookieContainer = Cookie;
            wr.Timeout = Settings.ResponseTimeout;
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
            var cookie = Cookie.GetCookies(new Uri("http://osu.ppy.sh"))[name];
            if (cookie == null)
            {
                return null;
            }
            return cookie.Value;
        }

        /// <summary>
        /// osu!에서 비트맵셋을 내려받습니다.
        /// </summary>
        /// <param name="id">비트맵셋 ID</param>
        /// <param name="onprogress"><code>(received, total) => { ... }</code></param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="SharpZipBaseException"></exception>
        public void Download(int id, Action<int, long> onprogress)
        {
            var url = "http://osu.ppy.sh/d/" + id;
            var path = Path.Combine(Settings.Storage, id + ".osz.download");

            var wr = Create(url);
            using (var rp = (HttpWebResponse) wr.GetResponse())
            using (var rs = rp.GetResponseStream())
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                int got;
                var received = 0;
                var buffer = new byte[4096];
                onprogress(received, rp.ContentLength);
                while ((got = rs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, got);
                    received += got;
                    onprogress(received, rp.ContentLength);
                }
            }

            using (new ZipFile(path))
            {
            }
        }
    }
}
