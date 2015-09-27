using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Manager;

namespace Bot
{
    internal class Set
    {
        public int Id;

        private int _status;
        /// <summary>
        /// 맵셋의 랭크 상태를 나타냅니다.
        /// <list type="number">
        ///     <item>
        ///         <term>3</term>
        ///         <description>qualified</description>
        ///     </item>
        ///     <item>
        ///         <term>2</term>
        ///         <description>approved</description>
        ///     </item>
        ///     <item>
        ///         <term>1</term>
        ///         <description>ranked</description>
        ///     </item>
        ///     <item>
        ///         <term>0</term>
        ///         <description>pending</description>
        ///     </item>
        ///     <item>
        ///         <term>-1</term>
        ///         <description>WIP</description>
        ///     </item>
        ///     <item>
        ///         <term>-2</term>
        ///         <description>graveyard</description>
        ///     </item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// 0 이하는 unranked로 통칩니다.
        /// </remarks>
        public int Status
        {
            get
            {
                return _status;
            }
            private set
            {
                _status = value < 0 ? 0 : value;
            }
        }

        public string Title
        {
            get
            {
                return Beatmaps.FirstOrDefault().Title;
            }
        }
        public string TitleUnicode
        {
            get
            {
                var unicode = Beatmaps.FirstOrDefault().TitleUnicode;
                return string.IsNullOrEmpty(unicode) || Title == unicode ? null : unicode;
            }
        }
        public string Artist
        {
            get
            {
                return Beatmaps.FirstOrDefault().Artist;
            }
        }
        public string ArtistUnicode
        {
            get
            {
                var unicode = Beatmaps.FirstOrDefault().ArtistUnicode;
                return string.IsNullOrEmpty(unicode) || Artist == unicode ? null : unicode;
            }
        }
        public string Creator
        {
            get
            {
                return Beatmaps.FirstOrDefault().Creator;
            }
        }
        public string CreatorOld;
        // public int CreatorID { get; set; }

        // public int Genre { get; set; }
        // public int Language { get; set; }

        public string[] Tags
        {
            get
            {
                var beatmap = Beatmaps.FirstOrDefault();
                return (beatmap.Source + " " + beatmap.Tags).ToLower()
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public List<Beatmap> Beatmaps;

        public Set()
        {
            Beatmaps = new List<Beatmap>();
        }


        public override string ToString()
        {
            return Regex.Replace(string.Join(" ",
                new[] { Title, TitleUnicode, Artist, ArtistUnicode, Creator, CreatorOld }
                .Concat(Beatmaps.ConvertAll(i => i.Version)).Concat(Tags)), @"\s+", " ").ToUpper();
        }


        private static JArray GetAPIData(string query)
        {
            const string url = "http://osu.ppy.sh/api/get_beatmaps?k={0}&{1}";

            var wr = WebRequest.CreateHttp(string.Format(url, Settings.APIKey, query));
            using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
            {
                return JArray.Parse(rp.ReadToEnd());
            }
        }

        /// <summary>
        /// osu! API를 통해 기본 정보(랭크 상태, 비트맵의 ID와 이름)를 가져옴
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <param name="lastUpdate">Ranked 맵셋은 approved_date, 그외 맵셋은 last_update 값을 저장</param>
        /// <returns>Set</returns>
        public static Set GetByAPI(int id, out DateTime lastUpdate)
        {
            lastUpdate = DateTime.MinValue;
            var set = new Set { Id = id };
            foreach (var i in GetAPIData("s=" + set.Id))
            {
                var update = Convert.ToDateTime(i["approved_date"].Value<string>() ?? i["last_update"].Value<string>())
                    .AddHours(1);
                // 1 호주 기준 osu! 시간 보정
                if (update > lastUpdate)
                {
                    lastUpdate = update;
                }

                set.Status = i["approved"].Value<int>();
                // set.Genre = i["genre_id"].Value<int>();
                // set.Language = i["language_id"].Value<int>();

                set.Beatmaps.Add(new Beatmap
                {
                    BeatmapId = i["beatmap_id"].Value<int>(),
                    Version = i["version"].Value<string>(),
                    Title = i["title"].Value<string>(),
                    Artist = i["artist"].Value<string>(),
                    Creator = i["creator"].Value<string>(),
                });
            }
            return set;
        }

        /// <summary>
        /// 내려받은 맵셋 파일을 통해 자세한 정보를 가져옴
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <returns>Set</returns>
        public static Set GetByLocal(int id)
        {
            var set = new Set { Id = id };
            using (var osz = new ZipFile(Path.Combine(Settings.Storage, id + "")))
            {
                foreach (var entry in osz.Cast<ZipEntry>().Where(i => i.IsFile && i.Name.EndsWith(".osu")))
                {
                    using (var reader = new StreamReader(osz.GetInputStream(entry.ZipFileIndex)))
                    {
                        set.Beatmaps.Add(Beatmap.Parse(reader.ReadToEnd()));
                    }
                }
            }
            return set;
        }
    }
}