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
    internal class Beatmap
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int Mode { get; set; }

        public double HPDrainRate { get; set; }
        public double CircleSize { get; set; }
        public double OverallDifficulty { get; set; }
        public double ApproachRate { get; set; }

        public double BPM { get; set; }
        public int Length { get; set; }
    }

    internal class Set
    {
        public int ID { get; set; }

        private int _status;

        public int Status
        {
            get { return _status; }
            private set { _status = value < 0 ? 0 : value; }
        }

        public string Title { get; set; }
        public string Artist { get; set; }
        public string Creator { get; set; }
        public string CreatorOld { get; set; }

        private string TitleU;

        public string TitleUnicode
        {
            get { return TitleU; }
            set { TitleU = Title == value || value == "" ? null : value; }
        }

        private string ArtistU;

        public string ArtistUnicode
        {
            get { return ArtistU; }
            set { ArtistU = Artist == value || value == "" ? null : value; }
        }

        public string[] Tags { get; set; }

        //public int Genre { get; set; }
        //public int Language { get; set; }
        //public int CreatorID { get; set; }

        public List<Beatmap> Beatmaps = new List<Beatmap>();

        public override string ToString()
        {
            return
                Regex.Replace(
                    string.Join(
                        " ",
                        new[] { Title, TitleUnicode, Artist, ArtistUnicode, Creator, CreatorOld }.Concat(
                            Beatmaps.ConvertAll(i => i.Name)).Concat(Tags)), @"\s+", " ").ToUpper();
        }


        private static IEnumerable<JToken> GetAPIData(string query)
        {
            const string url = "http://osu.ppy.sh/api/get_beatmaps?k={0}&{1}";
            using (var wc = new WebClient())
            {
                return
                    (JArray)
                        JsonConvert.DeserializeObject(wc.DownloadString(string.Format(url, Settings.APIKey, query)));
            }
        }

        /// <summary>
        /// osu! API를 통해 기본 정보(랭크 상태, 비트맵의 ID와 이름, 총 길이)를 가져옴
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <param name="lastUpdate">Ranked 맵셋은 approved_date, 그외 맵셋은 last_update 값을 저장</param>
        /// <returns>Set</returns>
        public static Set GetByAPI(int id, out DateTime lastUpdate)
        {
            var set = new Set { ID = id };

            lastUpdate = new DateTime(0);
            var first = true;
            foreach (var i in GetAPIData("s=" + set.ID))
            {
                var tmp = Convert.ToDateTime(i["approved_date"].Value<string>() ?? i["last_update"].Value<string>())
                    .AddHours(1);
                    // 1 호주 기준 osu! 시간 보정
                if (tmp > lastUpdate)
                {
                    lastUpdate = tmp;
                }

                if (first)
                {
                    set.Status = Convert.ToInt32(i["approved"]);
                    set.Title = Convert.ToString(i["title"]);
                    set.Artist = Convert.ToString(i["Artist"]);
                    set.Creator = Convert.ToString(i["Creator"]);

                    first = false;
                }

                set.Beatmaps.Add(new Beatmap
                {
                    ID = Convert.ToInt32(i["beatmap_id"]),
                    Name = Convert.ToString(i["version"]),
                    //Length = Convert.ToInt32(i["total_length"]), 슬라이더에서 끝나는 시간이 정확하지 않음
                });
            }
            return set;
        }

        private static string GetSetting(string osu, string key, string def = "")
        {
            var val = Regex.Match(osu, key + @".*?:([^\r\n]*)").Groups[1].Value.Trim();
            return val == "" ? def : val;
        }

        /// <summary>
        /// 내려받은 맵셋 파일을 통해 자세한 정보를 가져옴
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <returns>Set</returns>
        public static Set GetByLocal(int id)
        {
            var set = new Set { ID = id };

            var first = true;
            using (var osz = new ZipFile(Path.Combine(Settings.Storage, id + "")))
            {
                foreach (var entry in osz.Cast<ZipEntry>().Where(i => i.IsFile && i.Name.EndsWith(".osu")))
                {
                    using (var reader = new StreamReader(osz.GetInputStream(entry.ZipFileIndex)))
                    {
                        var osu = reader.ReadToEnd();

                        if (first)
                        {
                            set.Title = GetSetting(osu, "Title");
                            set.TitleUnicode = GetSetting(osu, "TitleUnicode", set.Title);
                            set.Artist = GetSetting(osu, "Artist");
                            set.ArtistUnicode = GetSetting(osu, "ArtistUnicode", set.Artist);
                            set.Creator = GetSetting(osu, "Creator");
                            set.Tags =
                                (GetSetting(osu, "Source") + " " + GetSetting(osu, "Tags")).ToLower()
                                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            first = false;
                        }

                        var beatmap = new Beatmap
                        {
                            ID = Convert.ToInt32(GetSetting(osu, "BeatmapID", "-1")),
                            Name = GetSetting(osu, "Version", "Normal"),
                            Mode = Convert.ToInt32(GetSetting(osu, "Mode", "0")),
                            HPDrainRate = Convert.ToDouble(GetSetting(osu, "HPDrainRate")),
                            CircleSize = Convert.ToDouble(GetSetting(osu, "CircleSize")),
                            OverallDifficulty = Convert.ToDouble(GetSetting(osu, "OverallDifficulty")),
                        };
                        beatmap.ApproachRate =
                            Convert.ToDouble(GetSetting(osu, "ApproachRate", beatmap.OverallDifficulty.ToString("F")));

                        var p = osu.IndexOf("[TimingPoints]", StringComparison.Ordinal);
                        var tmp = osu.Substring(p, osu.IndexOf('[', p + 1) - p);

                        var timingPoints = new List<double>();
                        p = 0;
                        foreach (Match i in Regex.Matches(tmp, @"\d+,(-?\d+(?:\.\d+)?)[^\r\n]*"))
                        {
                            var bpm = Convert.ToDouble(i.Groups[1].Value);
                            if (bpm < 0)
                            {
                                bpm *= timingPoints[p] / -100;
                            }
                            else
                            {
                                p = timingPoints.Count;
                            }
                            timingPoints.Add(bpm);
                        }
                        beatmap.BPM = 60000 / timingPoints[0];

                        p = osu.IndexOf("[HitObjects]", StringComparison.Ordinal);
                        tmp = osu.Substring(p, osu.LastIndexOf('\n') - p);
                        var temp = Regex.Matches(tmp, @"\d+,\d+,(\d+),(\d+),\d+,?([^\r\n]*)");
                        var lastNote = temp[temp.Count - 1];
                        var endTime = Convert.ToInt32(lastNote.Groups[1].Value);
                        switch (Convert.ToInt32(lastNote.Groups[2].Value) & 11)
                        {
                            case 2:
                            {

                                endTime +=
                                    Convert.ToInt32(
                                        Convert.ToDouble(GetSetting(osu, "SliderLength", "1")) /
                                        Convert.ToDouble(GetSetting(osu, "SliderMultiplier", "1.4")) / 100 *
                                        timingPoints[timingPoints.Count - 1] *
                                        Convert.ToInt32(lastNote.Groups[3].Value.Split(',')[1]));
                                break;
                            }
                            case 8:
                            {
                                endTime = Convert.ToInt32(lastNote.Groups[3].Value.Split(',')[0]);
                                break;
                            }
                        }
                        beatmap.Length =
                            (int) Math.Ceiling((endTime - Convert.ToInt32(temp[0].Groups[1].Value)) / 1000.0);

                        set.Beatmaps.Add(beatmap);
                    }
                }
            }
            return set;
        }
    }
}