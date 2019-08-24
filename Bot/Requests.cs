using Newtonsoft.Json.Linq;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace Bot
{
    class Requests
    {
        /// <summary>
        /// osu! API를 통해 기본 정보(랭크 상태, 비트맵의 ID와 이름, 갱신 날짜)를 가져옴.
        /// 여기서 기본 정보는 <code>LastUpdate, Status, Creator, CreatorId, Beatmaps[i].BeatmapID, Beatmaps[i].Version</code>입니다.
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <returns></returns>
        public static async Task<Set> GetSetFromAPIAsync(int id)
        {
            DateTime ConvertAPIDateTimeToLocal(DateTime dateTime)
            {
                // 호주 기준 UTC+8을 현지 시각으로 변환
                if (dateTime.Kind == DateTimeKind.Unspecified)
                {
                    return TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.FindSystemTimeZoneById("W. Australia Standard Time"), TimeZoneInfo.Local);
                }
                // TimeZone을 삽입한 DateTime은 자동으로 Local로 바뀌어서 작업할 필요가 없다.
                return dateTime;
            }

            Set set = null;
            //TODO last_update가 approved_date보다 최신이면 keep_synced로 업데이트 하기
            var inited = false;
            foreach (JObject i in await Request.Context.GetBeatmapsAPIAsync("s=" + id))
            {
                var rankedAt = i.Value<DateTime?>("approved_date");
                if (rankedAt != null)
                {
                    rankedAt = ConvertAPIDateTimeToLocal(rankedAt.Value);
                }
                var updatedAt = ConvertAPIDateTimeToLocal(i.Value<DateTime>("last_update"));

                if (!inited)
                {
                    inited = true;
                    set = new Set
                    {
                        SetId = id,
                        Title = i.Value<string>("title"),
                        Artist = i.Value<string>("artist"),
                        Creator = i.Value<string>("creator"),
                        CreatorId = i.Value<int>("creator_id"),
                        //StatusId = i.Value<int>("approved"),
                        RankedAt = rankedAt,
                        UpdatedAt = updatedAt,
                        Favorites = i.Value<int>("favourite_count"),
                        GenreId = i.Value<int>("genre_id"),
                        LanguageId = i.Value<int>("language_id")
                    };
                }

                // 정보가 캐시된 비트맵들 때문에 더 최신 정보를 확인해줘야 한다.
                var isLatest = set.UpdatedAt < updatedAt
                    || (set.RankedAt == null && rankedAt != null)
                    || (set.RankedAt != null && rankedAt == null)
                    || (set.RankedAt < rankedAt)
                    || (set.RankedAt != null && set.StatusId < i.Value<int>("approved"));
                if (isLatest)
                {
                    set.Title = i.Value<string>("title");
                    set.Artist = i.Value<string>("artist");
                    set.Creator = i.Value<string>("creator");
                    set.CreatorId = i.Value<int>("creator_id");
                    //set.StatusId = i.Value<int>("approved");
                    set.RankedAt = rankedAt;
                    set.UpdatedAt = updatedAt;
                    set.Favorites = i.Value<int>("favourite_count");
                    set.GenreId = i.Value<int>("genre_id");
                    set.LanguageId = i.Value<int>("language_id");
                }

                set.Beatmaps.Add(new Beatmap
                {
                    StatusId = i.Value<int>("approved"),
                    BeatmapInfo = new BeatmapInfo
                    {
                        OnlineBeatmapID = i.Value<int>("beatmap_id"),
                        Version = i.Value<string>("version"),
                        RulesetID = i.Value<int>("mode"),
                        MD5Hash = i.Value<string>("file_md5"),
                        StarDifficulty = i.Value<double>("difficultyrating"),
                        Metadata = new BeatmapMetadata
                        {
                            AuthorString = i.Value<string>("creator"),
                            Artist = i.Value<string>("artist"),
                            Title = i.Value<string>("title"),
                        },
                        //OnlineInfo = new BeatmapOnlineInfo
                        //{
                        //    Length = i.Value<double>("total_length")
                        //}
                    }
                });
            }
            return set;
        }

        /// <summary>
        /// DB를 통해 기본 정보(랭크 상태, 비트맵의 ID와 이름)를 가져옴.
        /// 여기서 기본 정보는 <code>Status, Creator, CreatorId, Beatmaps[i].BeatmapID, Beatmaps[i].Version</code>입니다.
        /// </summary>
        /// <param name="id">맵셋 ID</param>
        /// <returns></returns>
        public static Set GetSetFromDB(int id)
        {
            Set set = null;
            //TODO last_update가 approved_date보다 최신이면 keep_synced로 업데이트 하기
            var inited = false;
            using (var query = DB.Command)
            {
                query.CommandText = @"SELECT set.status, set.creator, beatmap.id,
                        beatmap.name, set.synced, beatmap.hash_md5,
                        beatmap.hash_sha2, set.rankedAt, set.genreId,
                        set.languageId, beatmap.star, beatmap.author,
                        set.title, set.artist, beatmap.status, set.creatorId,
                    FROM gosu_beatmaps beatmap 
                    LEFT JOIN gosu_sets `set` ON set.id = beatmap.setId 
                    WHERE beatmap.setId = @s";
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
                                SetId = id,
                                Title = result.GetString(12),
                                Artist = result.GetString(13),
                                Creator = result.GetString(1),
                                CreatorId = result.GetInt32(15),
                                StatusId = result.GetInt32(0),
                                RankedAt = result.IsDBNull(7) ? (DateTime?) null : result.GetDateTime(7),
                                UpdatedAt = result.GetDateTime(4),
                                GenreId = result.GetInt32(8),
                                LanguageId = result.GetInt32(9)
                            };
                        }

                        set.Beatmaps.Add(new Beatmap
                        {
                            StatusId = result.GetInt32(result.GetInt32(14) == 0 ? 0 : 14),
                            BeatmapInfo = new BeatmapInfo
                            {
                                OnlineBeatmapID = result.GetInt32(2),
                                Version = result.GetString(3),
                                MD5Hash = result.GetString(5),
                                Hash = result.GetString(6),
                                StarDifficulty = result.GetDouble(10),
                                Metadata = new BeatmapMetadata
                                {
                                    AuthorString = string.IsNullOrEmpty(result.GetString(11))
                                        ? result.GetString(1)
                                        : result.GetString(11),
                                },
                                //OnlineInfo = new BeatmapOnlineInfo
                                //{
                                //    Length
                                //}
                            }
                        });
                    }
                }
            }
            return set;
        }


        /// <summary>
        /// 내려받은 맵셋 파일을 통해 자세한 정보를 가져옴
        /// </summary>
        /// <param name="path">맵셋 파일 경로</param>
        /// <returns>Set</returns>
        public static Set GetSetFromLocal(int id, string path)
        {
            var set = new Set { SetId = id };
            using (var fs = File.OpenRead(path))
            using (var osz = new osu.Game.IO.Archives.ZipArchiveReader(fs))
            {
                if (!osz.Filenames.Any(f => f.EndsWith(@".osu")))
                {
                    throw new InvalidOperationException("No beatmap files found in the map folder.");
                }

                // https://github.com/ppy/osu/blob/v2018.201.0/osu.Game/Beatmaps/BeatmapManager.cs#L584
                foreach (var entry in osz.Filenames.Where(i => i.EndsWith(@".osu")))
                {
                    using (var raw = osz.GetStream(entry))
                    using (var ms = new MemoryStream()) //we need a memory stream so we can seek and shit
                    using (var sr = new StreamReader(ms))
                    {
                        raw.CopyTo(ms);
                        ms.Position = 0;

                        var beatmap = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<osu.Game.Beatmaps.Beatmap>(sr).Decode(sr);

                        beatmap.BeatmapInfo.Hash = ms.ComputeSHA2Hash();
                        beatmap.BeatmapInfo.MD5Hash = ms.ComputeMD5Hash();

                        //RulesetInfo ruleset = RulesetStore.GetRuleset(beatmap.BeatmapInfo.RulesetID);

                        // TODO: this should be done in a better place once we actually need to dynamically update it.
                        //beatmap.BeatmapInfo.StarDifficulty = Math.Max(0, ruleset?.CreateInstance()?.CreateDifficultyCalculator(beatmap).Calculate() ?? 0);

                        // https://github.com/ppy/osu/blob/v2018.201.0/osu.Game/Screens/Select/BeatmapInfoWedge.cs#L220
                        var lastObject = beatmap.HitObjects.LastOrDefault();
                        var endTime = (lastObject as IHasEndTime)?.EndTime ?? lastObject?.StartTime ?? 0;
                        var startTime = beatmap.HitObjects.FirstOrDefault()?.StartTime ?? 0;
                        beatmap.BeatmapInfo.OnlineInfo = new BeatmapOnlineInfo
                        {
                            Length = (endTime - startTime) / 1000.0
                        };

                        set.Beatmaps.Add(new Beatmap(beatmap));
                    }
                }
            }
            return set;
        }
    }
}
