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
using osu.Game.Beatmaps.IO;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using System.Reflection;

namespace Bot
{
    internal class Set
    {
        public int Id { get; set; }

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
            set
            {
                _status = value < 0 ? 0 : value;
            }
        }

        public string Title => Beatmaps.First().Metadata.Title;
        public string TitleUnicode
        {
            get
            {
                var unicode = Beatmaps.First().Metadata.TitleUnicode;
                return string.IsNullOrEmpty(unicode) || Title == unicode ? null : unicode;
            }
        }
        public string Artist => Beatmaps.First().Metadata.Artist;
        public string ArtistUnicode
        {
            get
            {
                var unicode = Beatmaps.First().Metadata.ArtistUnicode;
                return string.IsNullOrEmpty(unicode) || Artist == unicode ? null : unicode;
            }
        }
        public string Creator => Beatmaps.First().Metadata.Author;
        public int CreatorID
        {
            get
            {
                using (var query = DB.Command)
                {
                    query.CommandText = "SELECT creatorId FROM gosu_sets WHERE id = @id";
                    query.Parameters.AddWithValue("@id", Id);
                    using (var result = query.ExecuteReader())
                    {
                        if (result.Read())
                        {
                            return result.GetInt32(0);
                        }
                    }
                }

                try
                {
                    var wr = Program.Request.Create("http://osu.ppy.sh/s/" + Id);
                    using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
                    {
                        var beatmapPage = rp.ReadToEnd();
                        return Convert.ToInt32(Regex.Match(beatmapPage, Settings.CreatorExpression).Groups["id"].Value);
                    }
                }
                catch (WebException)
                {
                    return CreatorID;
                }
            }
        }

        // public int Genre { get; set; }
        // public int Language { get; set; }

        public string Source => Beatmaps.First().Metadata.Tags;
        public string Tags => Beatmaps.First().Metadata.Tags;

        public string[] SearchableTerms => new[]
        {
            Title,
            TitleUnicode,
            Artist,
            ArtistUnicode,
            Creator,
            Source,
            Tags
        }.Where(s => !string.IsNullOrEmpty(s)).ToArray();

        public List<Beatmap> Beatmaps = new List<Beatmap>();


        public override string ToString()
        {
            return Regex.Replace(
                string.Join(" ", SearchableTerms.Concat(Beatmaps.Select(i => i.BeatmapInfo.Version))),
                @"\s+", " ").ToUpper();
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
            catch (Exception e) when (e is WebException || e is JsonReaderException || e is IOException)
            {
                return GetAPIData(query);
            }
        }

        /// <summary>
        /// osu! API를 통해 기본 정보(랭크 상태, 비트맵의 ID와 이름)를 가져옴.
        /// 여기서 기본 정보는 <code>Status, Creator, Beatmaps[i].BeatmapID, Beatmaps[i].Version</code>입니다.
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <param name="lastUpdate">Ranked 맵셋은 approved_date, 그외 맵셋은 last_update 값을 저장</param>
        /// <returns></returns>
        public static Set GetByAPI(int id, out DateTime lastUpdate)
        {
            var set = new Set { Id = id };
            lastUpdate = DateTime.MinValue;
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
                    BeatmapInfo = new BeatmapInfo
                    {
                        OnlineBeatmapID = i["beatmap_id"].Value<int>(),
                        Version = i["version"].Value<string>(),
                        Metadata = new BeatmapMetadata
                        {
                            Author = i["creator"].Value<string>()
                        }
                    }
                });
            }
            return set;
        }

        /// <summary>
        /// DB를 통해 기본 정보(랭크 상태, 비트맵의 ID와 이름)를 가져옴.
        /// 여기서 기본 정보는 <code>Status, Creator, Beatmaps[i].BeatmapID, Beatmaps[i].Version</code>입니다.
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <returns></returns>
        public static Set GetByDB(int id)
        {
            var set = new Set { Id = id };
            using (var query = DB.Command)
            {
                query.CommandText = "SELECT set.status, set.creator, beatmap.id, beatmap.name FROM gosu_beatmaps beatmap " +
                    "LEFT JOIN gosu_sets `set` ON set.id = beatmap.setId " +
                    "WHERE beatmap.setId = @s";
                query.Parameters.AddWithValue("@s", id);
                using (var result = query.ExecuteReader())
                {
                    while (result.Read())
                    {
                        set.Status = result.GetInt32(0);

                        set.Beatmaps.Add(new Beatmap
                        {
                            BeatmapInfo = new BeatmapInfo
                            {
                                OnlineBeatmapID = result.GetInt32(2),
                                Version = result.GetString(3),
                                Metadata = new BeatmapMetadata
                                {
                                    Author = result.GetString(1)
                                }
                            }
                        });
                    }
                }
            }
            return set;
        }

        private static List<RulesetInfo> rulesets = null;
        private static RulesetInfo createRulesetInfo(Ruleset ruleset) => new RulesetInfo
        {
            Name = ruleset.Description,
            InstantiationInfo = ruleset.GetType().AssemblyQualifiedName,
            ID = ruleset.LegacyID
        };

        /// <summary>
        /// 내려받은 맵셋 파일을 통해 자세한 정보를 가져옴
        /// </summary>
        /// <param name="path">맵셋 파일 경로</param>
        /// <returns>Set</returns>
        public static Set GetByLocal(int id, string path)
        {
            if (rulesets == null)
            {
                // https://github.com/ppy/osu/blob/7fbbe74b65e7e399072c198604e9db09fb729626/osu.Game/Database/RulesetDatabase.cs
                List<Ruleset> instances = new List<Ruleset>();

                foreach (string file in Directory.GetFiles(Environment.CurrentDirectory, @"osu.Game.Rulesets.*.dll"))
                {
                    try
                    {
                        var assembly = Assembly.LoadFile(file);
                        var rulesets = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Ruleset)));

                        if (rulesets.Count() != 1)
                            continue;

                        foreach (Type rulesetType in rulesets)
                            instances.Add((Ruleset)Activator.CreateInstance(rulesetType));
                    }
                    catch (Exception) { }
                }

                rulesets = new List<RulesetInfo>();
                foreach (var r in instances.Where(r => r.LegacyID >= 0).OrderBy(r => r.LegacyID))
                {
                    rulesets.Add(createRulesetInfo(r));
                }
            }


            var set = new Set { Id = id };
            using (var fs = File.OpenRead(path))
            using (var osz = new OszArchiveReader(fs))
            {
                foreach (var entry in osz.BeatmapFilenames)
                {
                    using (var sr = new StreamReader(osz.GetStream(entry)))
                    {
                        var beatmap = BeatmapDecoder.GetDecoder(sr).Decode(sr);
                        var ruleset = rulesets.First(r => r.ID == beatmap.BeatmapInfo.RulesetID).CreateInstance();
                        beatmap.BeatmapInfo.StarDifficulty = ruleset?.CreateDifficultyCalculator(beatmap).Calculate() ?? 0;
                        set.Beatmaps.Add(beatmap);
                    }
                }
            }
            return set;
        }
    }
}