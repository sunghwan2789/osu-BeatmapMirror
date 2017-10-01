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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utility;
using osu.Game.Beatmaps.IO;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using System.Reflection;
using osu.Framework.Extensions;

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
        ///         <term>4</term>
        ///         <description>loved</description>
        ///     </item>
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

        public BeatmapMetadata Metadata => Beatmaps.First().Metadata;

        public string Title => Metadata.Title;
        public string TitleUnicode
        {
            get
            {
                var unicode = Metadata.TitleUnicode;
                return string.IsNullOrEmpty(unicode) || Title == unicode ? null : unicode;
            }
        }
        public string Artist => Metadata.Artist;
        public string ArtistUnicode
        {
            get
            {
                var unicode = Metadata.ArtistUnicode;
                return string.IsNullOrEmpty(unicode) || Artist == unicode ? null : unicode;
            }
        }
        public string Creator => Metadata.Author;
        public int CreatorID
        {
            get
            {
                // 먼저, DB에서 해당 비트맵셋의 creatorID를 가져옵니다.
                // 전에 내려받은 비트맵을 갱신하는 경우, 캐시 사용
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
                    var wr = new Request().Create("http://osu.ppy.sh/s/" + Id);
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
        public int Favorites { get; set; }

        public string Source => Metadata.Source;
        public string Tags => Metadata.Tags;

        /// <summary>Ranked 맵셋은 approved_date, 그외 맵셋은 last_update 값을 저장</summary>
        public DateTime LastUpdate { get; set; }

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
        /// osu! API를 통해 기본 정보(랭크 상태, 비트맵의 ID와 이름, 갱신 날짜)를 가져옴.
        /// 여기서 기본 정보는 <code>LastUpdate, Status, Creator, Beatmaps[i].BeatmapID, Beatmaps[i].Version</code>입니다.
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <returns></returns>
        public static Set GetByAPI(int id)
        {
            Set set = null;
            //TODO last_update가 approved_date보다 최신이면 keep_synced로 업데이트 하기
            var inited = false;
            foreach (JObject i in GetAPIData("s=" + id))
            {
                var update = i.Value<DateTime?>("approved_date") ?? i.Value<DateTime>("last_update");
                // 호주 기준 UTC+8을 현지 시각으로 변환
                if (update.Kind == DateTimeKind.Unspecified)
                {
                    update = TimeZoneInfo.ConvertTime(update, TimeZoneInfo.FindSystemTimeZoneById("W. Australia Standard Time"), TimeZoneInfo.Local);
                }
                
                if (!inited)
                {
                    inited = true;
                    set = new Set
                    {
                        Id = id,
                        Status = i.Value<int>("approved"),
                        LastUpdate = update,
                        Favorites = i.Value<int>("favourite_count"),
                    };
                    // set.Genre = i["genre_id"].Value<int>();
                    // set.Language = i["language_id"].Value<int>();
                }

                if (set.LastUpdate < update)
                {
                    set.LastUpdate = update;
                }

                set.Beatmaps.Add(new Beatmap
                {
                    BeatmapInfo = new BeatmapInfo
                    {
                        OnlineBeatmapID = i.Value<int>("beatmap_id"),
                        Version = i.Value<string>("version"),
                        RulesetID = i.Value<int>("mode"),
                        MD5Hash = i.Value<string>("file_md5"),
                        Metadata = new BeatmapMetadata
                        {
                            Author = i.Value<string>("creator"),
                            Artist = i.Value<string>("artist"),
                            Title = i.Value<string>("title"),
                        },
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
            Set set = null;
            //TODO last_update가 approved_date보다 최신이면 keep_synced로 업데이트 하기
            var inited = false;
            using (var query = DB.Command)
            {
                query.CommandText = "SELECT set.status, set.creator, beatmap.id, beatmap.name, set.synced, beatmap.hash_md5 FROM gosu_beatmaps beatmap " +
                    "LEFT JOIN gosu_sets `set` ON set.id = beatmap.setId " +
                    "WHERE beatmap.setId = @s";
                query.Parameters.AddWithValue("@s", id);
                using (var result = query.ExecuteReader())
                {
                    while (result.Read())
                    {
                        if (!inited)
                        {
                            inited = true;
                            set = new Set
                            {
                                Id = id,
                                Status = result.GetInt32(0),
                                LastUpdate = result.GetDateTime(4),
                            };
                        }

                        set.Beatmaps.Add(new Beatmap
                        {
                            BeatmapInfo = new BeatmapInfo
                            {
                                OnlineBeatmapID = result.GetInt32(2),
                                Version = result.GetString(3),
                                MD5Hash = result.GetString(5),
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

        private static readonly Dictionary<Assembly, Type> loaded_assemblies = new Dictionary<Assembly, Type>();
        private const string ruleset_library_prefix = "osu.Game.Rulesets";
        private static void LoadRulesetFromFile(string file)
        {
            var filename = Path.GetFileNameWithoutExtension(file);

            if (loaded_assemblies.Values.Any(t => t.Namespace == filename))
                return;

            try
            {
                var assembly = Assembly.LoadFrom(file);
                loaded_assemblies[assembly] = assembly.GetTypes().First(t => t.IsSubclassOf(typeof(Ruleset)));
            }
            catch (Exception) { }
        }
        private static Assembly currentDomain_AssemblyResolve(object sender, ResolveEventArgs args) => loaded_assemblies.Keys.FirstOrDefault(a => a.FullName == args.Name);

        private static List<RulesetInfo> rulesets = null;
        private static RulesetInfo CreateRulesetInfo(Ruleset ruleset) => new RulesetInfo
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
                AppDomain.CurrentDomain.AssemblyResolve += currentDomain_AssemblyResolve;
                // https://github.com/ppy/osu/blob/99b512cce57d2308cde8dea7ffbfe6ba84cbb32e/osu.Game/Rulesets/RulesetStore.cs
                foreach (string file in Directory.GetFiles(Environment.CurrentDirectory, $"{ruleset_library_prefix}.*.dll"))
                    LoadRulesetFromFile(file);
                var instances = loaded_assemblies.Values.Select(r => (Ruleset)Activator.CreateInstance(r, new RulesetInfo()));

                rulesets = new List<RulesetInfo>();
                //add all legacy modes in correct order
                foreach (var r in instances.Where(r => r.LegacyID >= 0).OrderBy(r => r.LegacyID))
                {
                    rulesets.Add(CreateRulesetInfo(r));
                }
            }


            var set = new Set { Id = id };
            using (var fs = File.OpenRead(path))
            using (var osz = new OszArchiveReader(fs))
            {
                if (!osz.Filenames.Any(f => f.EndsWith(@".osu")))
                {
                    throw new InvalidOperationException("No beatmap files found in the map folder.");
                }
                
                // https://github.com/ppy/osu/blob/9576f71b10bab8d0a860afb2d888b808dab45902/osu.Game/Beatmaps/BeatmapManager.cs#L471
                foreach (var entry in osz.Filenames.Where(i => i.EndsWith(@".osu")))
                {
                    using (var raw = osz.GetStream(entry))
                    using (var ms = new MemoryStream()) //we need a memory stream so we can seek and shit
                    using (var sr = new StreamReader(ms))
                    {
                        raw.CopyTo(ms);
                        ms.Position = 0;

                        var beatmap = BeatmapDecoder.GetDecoder(sr).Decode(sr);

                        if (string.IsNullOrEmpty(beatmap.BeatmapInfo.Version))
                        {
                            beatmap.BeatmapInfo.Version = "Normal";
                        }

                        beatmap.BeatmapInfo.Hash = ms.ComputeSHA2Hash();
                        beatmap.BeatmapInfo.MD5Hash = ms.ComputeMD5Hash();

                        // TODO: this should be done in a better place once we actually need to dynamically update it.
                        beatmap.BeatmapInfo.Ruleset = rulesets.FirstOrDefault(r => r.ID == beatmap.BeatmapInfo.RulesetID);
                        beatmap.BeatmapInfo.StarDifficulty = beatmap.BeatmapInfo.Ruleset?.CreateInstance()?.CreateDifficultyCalculator(beatmap).Calculate() ?? 0;

                        set.Beatmaps.Add(beatmap);
                    }
                }
            }
            return set;
        }
    }
}