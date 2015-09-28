using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Manager;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using ICSharpCode.SharpZipLib;

namespace Bot
{
    static class Program
    {
        private static Request Request = new Request();

        private static void Main(string[] args)
        {
            const string url = "http://osu.ppy.sh/forum/ucp.php?mode=login";

            var wr = Request.Create(url, true);
            if (string.IsNullOrEmpty(Settings.Session))
            {
                using (var sw = new StreamWriter(wr.GetRequestStream()))
                {
                    sw.Write(string.Format("login=login&username={0}&password={1}&autologin=on",
                        Uri.EscapeDataString(Settings.OsuId),
                        Uri.EscapeDataString(Settings.OsuPw)));
                }
            }
            else
            {
                Request.AddCookie(Settings.SessionKey, Settings.Session);
            }
            using (var rp = (HttpWebResponse) wr.GetResponse())
            {
                if (rp.Cookies["last_login"] == null)
                {
                    Settings.Session = "";
                    Console.WriteLine("login failed");
                    Main(args);
                    return;
                }
            }
            Settings.Session = Request.GetCookie(Settings.SessionKey);

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
                    DateTime lastUpdate;
                    Sync(Set.GetByAPI(Convert.ToInt32(arg.Groups[1].Value), out lastUpdate), skipDownload, keepSynced);
                }
                return;
            }

            var bucket = new Stack<Set>();
            var lastCheckTime = Settings.LastCheckTime;
            using (var query = DB.Command)
            {
                query.CommandText = "SELECT status, synced FROM gosu_sets WHERE id = @i";
                query.Parameters.Add("@i", MySqlDbType.Int32);
                foreach (var r in Settings.BeatmapList)
                {
                    var page = 1;
                    do
                    {
                        foreach (var id in GrabSetIDFromBeatmapList(r, page))
                        {
                            DateTime lastUpdate;
                            var set = Set.GetByAPI(id, out lastUpdate);

                            if (lastUpdate > lastCheckTime)
                            {
                                lastCheckTime = lastUpdate;
                            }
                            if (lastUpdate <= Settings.LastCheckTime)
                            {
                                page = 0;
                                break;
                            }

                            var status = 0;
                            var synced = lastUpdate;
                            query.Parameters["@i"].Value = id;
                            using (var result = query.ExecuteReader())
                            {
                                if (result.Read())
                                {
                                    status = Convert.ToInt32(result.GetValue(0));
                                    synced = Convert.ToDateTime(result.GetValue(1));
                                }
                            }
                            if ((status != set.Status || synced < lastUpdate) &&
                                !bucket.Any(i => i.Id == id))
                            {
                                bucket.Push(set);
                            }
                        }
                    } while (page > 0 && page++ < 125);
                }
            }
            Settings.LastCheckTime = lastCheckTime;
            while (bucket.Any())
            {
                Sync(bucket.Pop());
            }
        }

        private static bool Sync(Set set, bool skipDownload = false, bool keepSynced = false)
        {
            Log.Flag = set.Id;
            var started = DateTime.Now;
            var result = false;
            try
            {
                Log.Write("DOWNLOAD");
                if (!skipDownload)
                {
                    Request.Download(set.Id, null);
                }

                var local = Set.GetByLocal(set.Id);
                local.Status = set.Status;
                foreach (var beatmap in local.Beatmaps.Where(i => i.BeatmapID == 0))
                {
                    var online = set.Beatmaps.Find(i => i.Version == beatmap.Version);
                    if (online == null)
                    {
                        throw new EntryPointNotFoundException();
                    }
                    beatmap.BeatmapID = online.BeatmapID;
                }

                Register(local, keepSynced ? DateTime.MinValue : started);
                result = true;
            }
            catch (SharpZipBaseException)
            {
                Log.Write("invalid beatmap set");
            }
            catch (WebException)
            {
                return Sync(set, skipDownload, keepSynced);
            }
            catch (Exception e)
            {
                Log.Write(e.GetBaseException() +"");
            }
            return result;
        }

        private static void Register(Set set, DateTime synced)
        {
            using (var query = DB.Command)
            {
                query.CommandText = "INSERT INTO gosu_artists (name, nameU)" +
                    "VALUES (@a, @au) " +
                    "ON DUPLICATE KEY UPDATE idx = LAST_INSERT_ID(idx), name = @a, nameU = @au";
                query.Parameters.AddWithValue("@a", set.Artist);
                query.Parameters.AddWithValue("@au", set.ArtistUnicode);
                query.ExecuteNonQuery();
                var artistId = query.LastInsertedId;

                var wr = Request.Create("http://osu.ppy.sh/s/" + set.Id);
                using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
                {
                    var beatmapPage = rp.ReadToEnd();
                    var creatorId = Convert.ToInt32(Regex.Match(beatmapPage, Settings.CreatorExpression).Groups["id"].Value);
                    query.CommandText = "INSERT INTO gosu_creators (id, name) "+
                        "VALUES (@ci, @c) " +
                        "ON DUPLICATE KEY UPDATE oldName = (CASE WHEN name = @c THEN oldName ELSE name END), name = @c";
                    query.Parameters.AddWithValue("@ci", creatorId);
                    query.Parameters.AddWithValue("@c", set.Creator);
                    query.ExecuteNonQuery();

                    query.CommandText = "SELECT oldName FROM gosu_creators WHERE id = @ci";
                    set.CreatorOld = Convert.ToString(query.ExecuteScalar());

                    query.CommandText = "INSERT INTO gosu_sets (id, status, artistId, creatorId, title, titleU, synced) " +
                        "VALUES (@i, @s, @ai, @ci, @t, @tu, @sy) " +
                        "ON DUPLICATE KEY UPDATE status = @s, artistId = @ai, title = @t, titleU = @tu, synced = @sy";
                    if (synced == DateTime.MinValue)
                    {
                        query.CommandText = query.CommandText.Replace("= @sy", "= synced");
                    }
                    query.Parameters.AddWithValue("@i", set.Id);
                    query.Parameters.AddWithValue("@s", set.Status);
                    query.Parameters.AddWithValue("@ai", artistId);
                    query.Parameters.AddWithValue("@t", set.Title);
                    query.Parameters.AddWithValue("@tu", set.TitleUnicode);
                    query.Parameters.AddWithValue("@sy", synced.ToString("s").Replace('T', ' '));
                    query.ExecuteNonQuery();
                }

                query.CommandText = "DELETE FROM gosu_beatmaps WHERE setId = @i";
                query.ExecuteNonQuery();

                query.Parameters.Add("@d1", MySqlDbType.Float);
                query.Parameters.Add("@d2", MySqlDbType.Float);
                query.Parameters.Add("@d3", MySqlDbType.Float);
                query.Parameters.Add("@d4", MySqlDbType.Float);
                query.Parameters.Add("@b", MySqlDbType.Float);
                query.Parameters.Add("@l", MySqlDbType.Int32);
                query.CommandText = "INSERT INTO gosu_beatmaps (setId, id, name, mode, hp, cs, od, ar, bpm, length) " +
                    "VALUES (@i, @ci, @tu, @ai, @d1, @d2, @d3, @d4, @b, @l)";
                foreach (var beatmap in set.Beatmaps)
                {
                    query.Parameters["@ci"].Value = beatmap.BeatmapID;
                    query.Parameters["@tu"].Value = beatmap.Version;
                    query.Parameters["@ai"].Value = beatmap.Mode;
                    query.Parameters["@d1"].Value = beatmap.HPDrainRate;
                    query.Parameters["@d2"].Value = beatmap.CircleSize;
                    query.Parameters["@d3"].Value = beatmap.OverallDifficulty;
                    query.Parameters["@d4"].Value = beatmap.ApproachRate;
                    query.Parameters["@b"].Value = beatmap.BPM;
                    query.Parameters["@l"].Value = beatmap.Length;
                    query.ExecuteNonQuery();
                }

                query.CommandText = "UPDATE gosu_sets SET keyword = @t where id = @i";
                query.Parameters["@t"].Value = set.ToString();
                query.ExecuteNonQuery();
            }
        }

        private static IEnumerable<int> GrabSetIDFromBeatmapList(int r, int page = 1)
        {
            const string url = "https://osu.ppy.sh/p/beatmaplist?r={0}&page={1}";

            var wr = Request.Create(string.Format(url, r, page));
            using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
            {
                return from Match i in Regex.Matches(rp.ReadToEnd(), Settings.SetIdExpression)
                       select Convert.ToInt32(i.Groups[1].Value);
            }
        }
    }
}
