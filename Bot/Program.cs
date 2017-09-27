using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Diagnostics;
using osu.Game.Beatmaps.IO;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Beatmaps.Formats;

namespace Bot
{
    static class Program
    {
        private static Request Request = new Request();

        private static List<string> faults;

        private static void Main(string[] args)
        {
            System.AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;


            OsuLegacyDecoder.Register();



            Settings.Session = string.IsNullOrEmpty(Settings.Session) ?
                Request.Login(Settings.OsuId, Settings.OsuPw) :
                Request.Login(Settings.Session);
            if (Settings.Session == null)
            {
                Console.WriteLine("login failed");
                Main(args);
                return;
            }

            if (args.Length > 0)
            {
                if (args[0] == "/?")
                {
                    Console.WriteLine();
                    Console.WriteLine(Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location) + " (SetID[l][s])*");
                    Console.WriteLine();
                    Console.WriteLine("\tl");
                    Console.WriteLine("\t\t내려받기를 건너뛰고 로컬에 있는 맵셋 파일 사용");
                    Console.WriteLine("\ts");
                    Console.WriteLine("\t\tsynced 열의 값을 유지하면서 데이터베이스 갱신");
                    Console.WriteLine();
                    return;
                }

                if (args[0] == "reset")
                {
                    faults = new List<string>();
                    using (var query = DB.Command)
                    {
                        query.CommandText = "SELECT id FROM gosu_sets {122} ORDER BY synced DESC";
                        if (args.Length > 1)
                        {
                            query.CommandText = query.CommandText.Replace("{122}", "WHERE synced < (SELECT synced FROM gosu_sets WHERE id = @id)");
                            query.Parameters.AddWithValue("@id", int.Parse(args[1]));
                        }
                        else
                        {
                            query.CommandText = query.CommandText.Replace("{122}", "");
                        }
                        using (var result = query.ExecuteReader())
                        {
                            while (result.Read())
                            {
                                if (!Sync(Set.GetByDB(result.GetInt32(0)), true, true))
                                {
                                    faults.Add(result.GetString(0));
                                }
                            }
                        }
                    }
                    if (faults.Count > 0)
                    {
                        throw new NotImplementedException();
                    }
                    return;
                }

                foreach (Match arg in Regex.Matches(string.Join(" ", args), @"(\d+)([^\s]*)"))
                {
                    var skipDownload = false;
                    var keepSynced = false;
                    foreach (var op in arg.Groups[2].Value)
                    {
                        if (op == 'l')
                        {
                            skipDownload = true;
                        }
                        else if (op == 's')
                        {
                            keepSynced = true;
                        }
                    }
                    Sync(Set.GetByAPI(Convert.ToInt32(arg.Groups[1].Value)), skipDownload, keepSynced);
                }

                if (args[0] != "manage")
                {
                    return;
                }
                Log.Writer = new StreamWriter(File.Open(Settings.LogPath + ".bot.log", FileMode.Create));
            }

            var bucket = new Stack<Set>();
            var lastCheckTime = Settings.LastCheckTime;
            foreach (var r in Settings.BeatmapList)
            {
                var page = 1;
                do
                {
                    var ids = GrabSetIDFromBeatmapList(r, page);
                    if (ids.Count() == 0)
                    {
                        break;
                    }
                    foreach (var id in ids)
                    {
                        var set = Set.GetByAPI(id);

                        if (lastCheckTime < set.LastUpdate)
                        {
                            lastCheckTime = set.LastUpdate;
                        }
                        // 마지막 확인한 비트맵과 이 비트맵의 날짜를 비교 후 탐색 중지 여부 검사
                        // API에 정보가 늦게 등록될 수 있음 + 비트맵 리스트 캐시 피하기 위함
                        if (set.LastUpdate < Settings.LastCheckTime.AddHours(-12))
                        {
                            page = 0;
                            break;
                        }

                        var savedSet = Set.GetByDB(id);
                        // 랭크 상태가 다르거나, 수정 날짜가 다르면 업데이트
                        if ((savedSet.Status != set.Status || savedSet.LastUpdate < set.LastUpdate) &&
                            !bucket.Any(i => i.Id == id))
                        {
                            bucket.Push(set);
                        }
                    }
                } while (page > 0 && page++ < 125);
            }
            while (bucket.Any())
            {
                if (!Sync(bucket.Pop()))
                {
                    // 동기화에 실패한 비트맵을 다음 번에 다시 확인해 보아야 한다.
                    lastCheckTime = Settings.LastCheckTime;
                }
            }
            Settings.LastCheckTime = lastCheckTime;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception) e.ExceptionObject;
            Log.Write(ex.GetBaseException().ToString());
            Log.Write(string.Join("ls ", faults) + "ls\n");
        }

        /// <summary>
        /// 비트맵을 내려받고 DB에 등록합니다.
        /// </summary>
        /// <param name="set">기본 정보가 입력된 Set.
        /// 여기서 기본 정보는 <code>Id, Status, Beatmaps[i].BeatmapID, Beatmaps[i].Version, Beatmaps[i].Creator</code>입니다.</param>
        /// <param name="skipDownload"></param>
        /// <param name="keepSynced"></param>
        /// <returns></returns>
        private static bool Sync(Set set, bool skipDownload = false, bool keepSynced = false)
        {
            var started = keepSynced ? DateTime.MinValue : DateTime.Now;
            var result = false;
            try
            {
                var path = Request.Download(set.Id, null, skipDownload);
                Log.Write(set.Id + " DOWNLOADED");

                var local = Set.GetByLocal(set.Id, path);
                local.Status = set.Status;
                local.Beatmaps = set.Beatmaps.Select(i =>
                {
                    // BeatmapID로 찾지 않는 이유는
                    // 올린 비트맵을 삭제하고 다시 올리면
                    // 맵셋 Id만 올라가고 비트맵 Id는 그대로이기 때문임.
                    var beatmap = local.Beatmaps.Find(j =>
                        j.BeatmapInfo.Version == i.BeatmapInfo.Version &&
                        j.Metadata.Author == i.Metadata.Author);
                    if (beatmap == null)
                    {
                        throw new EntryPointNotFoundException();
                    }
                    beatmap.BeatmapInfo.OnlineBeatmapID = i.BeatmapInfo.OnlineBeatmapID;
                    return beatmap;
                }).ToList();
                Log.Write(set.Id + " IS VALID");

                Register(local, started);
                Log.Write(set.Id + " REGISTERED");

                try
                {
                    var oldBeatmap = path.Remove(path.LastIndexOf(".download"));
                    if (File.Exists(oldBeatmap))
                    {
                        File.Delete(oldBeatmap);
                    }
                    File.Move(path, oldBeatmap);
                }
                catch { }

                result = true;
            }
            catch (WebException)
            {
                return Sync(set, skipDownload, keepSynced);
            }
            catch (EntryPointNotFoundException)
            {
                Log.Write(set.Id + " CORRUPTED ENTRY");
            }
            catch (Exception e)
            {
                Log.Write(set.Id + " " + e.GetBaseException() + ": " + e.Message);
            }
            return result;
        }

        private static void Register(Set set, DateTime synced)
        {
            using (var conn = DB.Connect())
            using (var tr = conn.BeginTransaction())
            using (var query = conn.CreateCommand())
            {
                query.CommandText = "INSERT INTO ggosu_sets (id, status, artist, artistU, title, titleU, creatorId, creator, synced) " +
                    "VALUES (@i, @s, @a, @au, @t, @tu, @ci, @c, @sy) " +
                    "ON DUPLICATE KEY UPDATE status = @s, artist = @a, artistU = @au, title = @t, titleU = @tu, creator = @c, synced = @sy";
                if (synced == DateTime.MinValue)
                {
                    query.CommandText = query.CommandText.Replace("= @sy", "= synced");
                }
                query.Parameters.AddWithValue("@i", set.Id);
                query.Parameters.AddWithValue("@s", set.Status);
                query.Parameters.AddWithValue("@a", set.Artist);
                query.Parameters.AddWithValue("@au", set.ArtistUnicode);
                query.Parameters.AddWithValue("@t", set.Title);
                query.Parameters.AddWithValue("@tu", set.TitleUnicode);
                query.Parameters.AddWithValue("@ci", set.CreatorID);
                query.Parameters.AddWithValue("@c", set.Creator);
                query.Parameters.AddWithValue("@sy", synced.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                query.ExecuteNonQuery();

                query.CommandText = "DELETE FROM ggosu_beatmaps WHERE setId = @i";
                query.ExecuteNonQuery();

                query.Parameters.Add("@d1", MySqlDbType.Float);
                query.Parameters.Add("@d2", MySqlDbType.Float);
                query.Parameters.Add("@d3", MySqlDbType.Float);
                query.Parameters.Add("@d4", MySqlDbType.Float);
                query.Parameters.Add("@b", MySqlDbType.Float);
                query.Parameters.Add("@l", MySqlDbType.Int32);
                query.Parameters.Add("@d0", MySqlDbType.Float);
                query.CommandText = "INSERT INTO ggosu_beatmaps (setId, id, name, mode, hp, cs, od, ar, bpm, length, star) " +
                    "VALUES (@i, @s, @a, @ci, @d1, @d2, @d3, @d4, @b, @l, @d0)";
                foreach (var beatmap in set.Beatmaps)
                {
                    query.Parameters["@s"].Value = beatmap.BeatmapInfo.OnlineBeatmapID;
                    query.Parameters["@a"].Value = beatmap.BeatmapInfo.Version;
                    query.Parameters["@ci"].Value = beatmap.BeatmapInfo.RulesetID;
                    query.Parameters["@d1"].Value = beatmap.BeatmapInfo.Difficulty.DrainRate;
                    query.Parameters["@d2"].Value = beatmap.BeatmapInfo.Difficulty.CircleSize;
                    query.Parameters["@d3"].Value = beatmap.BeatmapInfo.Difficulty.OverallDifficulty;
                    query.Parameters["@d4"].Value = beatmap.BeatmapInfo.Difficulty.ApproachRate;
                    query.Parameters["@b"].Value = beatmap.ControlPointInfo.BPMMode;
                    // https://github.com/ppy/osu/blob/e93d0cbb3ab7daf6c2ef4a80c755f429a5d88609/osu.Game/Screens/Select/BeatmapInfoWedge.cs#L109
                    var lastObject = beatmap.HitObjects.LastOrDefault();
                    var endTime = (lastObject as IHasEndTime)?.EndTime ?? lastObject?.StartTime ?? 0;
                    query.Parameters["@l"].Value = (int)(endTime - (beatmap.HitObjects.FirstOrDefault()?.StartTime ?? 0)) / 1000;
                    query.Parameters["@d0"].Value = beatmap.BeatmapInfo.StarDifficulty;
                    query.ExecuteNonQuery();
                }

                query.CommandText = "UPDATE ggosu_sets SET keyword = @t where id = @i";
                query.Parameters["@t"].Value = set.ToString();
                query.ExecuteNonQuery();

                tr.Commit();
            }
        }

        private static IEnumerable<int> GrabSetIDFromBeatmapList(int r, int page = 1)
        {
            const string url = "http://osu.ppy.sh/p/beatmaplist?r={0}&page={1}";

            try
            {
                var wr = Request.Create(string.Format(url, r, page));
                using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
                {
                    return from Match i in Regex.Matches(rp.ReadToEnd(), Settings.SetIdExpression)
                           select Convert.ToInt32(i.Groups[1].Value);
                }
            }
            catch (WebException)
            {
                return GrabSetIDFromBeatmapList(r, page);
            }
        }
    }
}
