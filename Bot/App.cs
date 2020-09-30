using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace Bot
{
    class App : BackgroundService
    {
        private string[] Args { get; }
        private IHostApplicationLifetime ApplicationLifetime { get; }
        private ILogger Logger { get; }
        private OsuLegacyClient OsuLegacyClient { get; }
        private Requests Requests { get; }

        public App(
            CommandLineArgs commandLineArgs,
            IHostApplicationLifetime applicationLifetime,
            ILogger<App> logger,
            OsuLegacyClient osuLegacyClient,
            Requests requests)
        {
            Args = commandLineArgs.Args;
            ApplicationLifetime = applicationLifetime;
            Logger = logger;
            OsuLegacyClient = osuLegacyClient;
            Requests = requests;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Release current synchronization context for next services.
            await Task.Yield();

            try
            {
                await LoginAsync(stoppingToken);

                if (Args.Length > 0)
                {
                    await ProcessCommandLineToolsAsync(stoppingToken);
                }

                await FetchAndSyncBeatmapsetsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // noop
            }
            finally
            {
                ApplicationLifetime.StopApplication();
            }
        }

        private async Task LoginAsync(CancellationToken token = default)
        {
            for (var tries = 5; tries > 0; tries -= 1)
            {
                if (string.IsNullOrEmpty(Settings.Session)
                    ? await OsuLegacyClient.LoginAsync(Settings.OsuId, Settings.OsuPw, token)
                    : await OsuLegacyClient.LoginAsync(Settings.Session, token))
                {
                    Settings.Session = OsuLegacyClient.Session;
                    return;
                }
                else
                {
                    Console.WriteLine("login failed");
                    Settings.Session = null;
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }

            throw new AuthenticationException();
        }

        private async Task ProcessCommandLineToolsAsync(CancellationToken token = default)
        {
            var failures = new List<string>();

            if (Args[0] == "/?")
            {
                Console.WriteLine();
                Console.WriteLine(Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location) + " (SetID[l][s])*");
                Console.WriteLine();
                Console.WriteLine("\tl");
                Console.WriteLine("\t\t내려받기를 건너뛰고 로컬에 있는 맵셋 파일 사용");
                Console.WriteLine("\ts");
                Console.WriteLine("\t\tsynced 열의 값을 유지하면서 데이터베이스 갱신");
                Console.WriteLine();
                throw new OperationCanceledException();
            }

            if (Args[0] == "boo")
            {
                using (var conn = DB.Connect())
                using (var tx = conn.BeginTransaction())
                using (var query = conn.CreateCommand())
                {
                    var r = new Random(DateTime.Now.Millisecond);
                    query.CommandText = "SELECT id, synced, rankedAt FROM gosu_sets WHERE synced <= rankedAt";
                    var records = new List<Tuple<int, DateTime, DateTime>>();
                    using (var result = query.ExecuteReader())
                    {
                        while (await result.ReadAsync())
                        {
                            var setId = result.GetInt32(0);
                            var synced = result.GetDateTime(1);
                            var rankedAt = result.GetDateTime(2);
                            synced = synced.AddMinutes(r.Next(3));
                            synced = synced.AddSeconds(r.NextDouble());
                            records.Add(new Tuple<int, DateTime, DateTime>(setId, synced, rankedAt));
                        }
                    }
                    Console.WriteLine($"{records.Count} to update");
                    query.CommandText = "UPDATE gosu_sets SET synced=@s WHERE id=@i";
                    query.Parameters.Add("@s", MySqlDbType.DateTime);
                    query.Parameters.Add("@i", MySqlDbType.Int32);
                    foreach (var record in records)
                    {
                        query.Parameters["@s"].Value = record.Item2.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        query.Parameters["@i"].Value = record.Item1;
                        query.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                throw new OperationCanceledException();
            }

            if (Args[0] == "health")
            {
                Log.WriteLevel = 1;
                foreach (var file in Directory.EnumerateFiles(Settings.Storage, "*.download"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        Console.WriteLine($"{file} 삭제 실패!");
                    }
                }
                var queue = new Queue<int>();
                using (var query = DB.Command)
                {
                    query.CommandText = "SELECT id FROM gosu_sets {} ORDER BY synced DESC";
                    if (Args.Length > 1)
                    {
                        query.CommandText = query.CommandText.Replace("{}", "WHERE synced <= (SELECT synced FROM gosu_sets WHERE id = @id)");
                        query.Parameters.AddWithValue("@id", int.Parse(Args[1]));
                    }
                    else
                    {
                        query.CommandText = query.CommandText.Replace("{}", "");
                    }
                    using (var result = query.ExecuteReader())
                    {
                        while (await result.ReadAsync())
                        {
                            queue.Enqueue(result.GetInt32(0));
                        }
                    }
                }
                var startedAt = DateTime.Now;
                while (queue.Any())
                {
                    Console.Title = $@"Now {queue.Peek()}, {queue.Count} Left, {DateTime.Now.Subtract(startedAt)} Passed";
                    //var set = Requests.GetSetFromDB(result.GetInt32(0));
                    //set.SyncOption |= SyncOption.KeepSyncedAt | SyncOption.SkipDownload;
                    //if (!Sync(set))
                    //{
                    //    faults.Add(result.GetString(0));
                    //}

                    var id = queue.Dequeue();
                    var set = await Requests.GetSetFromAPIAsync(id, token) ?? await Requests.GetSetFromDBAsync(id, token);
                    var saved = await Requests.GetSetFromDBAsync(id, token);
                    // 랭크 상태가 다르거나, 수정 날짜가 다르면 업데이트
                    //if ((
                    //        (set.StatusId > 0 && (saved == null || (saved.UpdatedAt < set.InfoChangedAt || saved.StatusId != set.StatusId)))
                    //        || (set.StatusId == 0 && (saved != null && (saved.UpdatedAt < set.InfoChangedAt || saved.StatusId != set.StatusId)))
                    //    ))
                    //{
                    set.SyncOption |= SyncOption.SkipDownload;
                    if (saved.UpdatedAt < set.InfoChangedAt || saved.StatusId != set.StatusId)
                    {
                        Console.WriteLine($"{id}가 변경된 것 같습니다.");
                        Console.WriteLine($@"Updated: {saved.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")} ==> {set.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")}");
                        Console.WriteLine($@"Ranked: {saved.RankedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"} ==> {set.RankedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}");
                        Console.WriteLine($@"Status: {saved.StatusId} ==> {set.StatusId}");
                        Console.WriteLine($@"Beatmaps: {saved.Beatmaps.Count} ==> {set.Beatmaps.Count}");
                        Console.Write($"{id}를 내려받을까요? (Y/N): ");
                        if (saved.StatusId == 0 && set.StatusId == 0)
                        {
                            Console.Write("미등록 비트맵은 자동으로 내려받습니다...");
                            set.SyncOption &= ~SyncOption.SkipDownload;
                        }
                        else //if (Console.ReadKey().Key == ConsoleKey.Y)
                        {
                            Console.Write("등록된 비트맵은 첫 시도에 있는 파일로 등록을 시도합니다...");
                            //set.SyncOption &= ~SyncOption.SkipDownload;
                        }
                        Console.WriteLine();
                    }
                    // 1차 시도
                    if (await Sync(set, token))
                    {
                        continue;
                    }

                    Console.WriteLine($"{id}가 깨진 것 같습니다.");
                    Console.Write($"{id}를 내려받을까요? (Y/N): ");
                    if (saved.StatusId == 0 && set.StatusId == 0)
                    {
                        Console.Write("미등록 비트맵은 자동으로 내려받습니다...");
                        set.SyncOption &= ~SyncOption.SkipDownload;
                    }
                    else //if (Console.ReadKey().Key == ConsoleKey.Y)
                    {
                        Console.Write("자야되니까 자동 체크 합니다!!!!!!!!!!!!!!!!!!!!!!!");
                        set.SyncOption &= ~SyncOption.SkipDownload;
                    }
                    Console.WriteLine();
                    // 2차부터는 그냥 나중에 수동으로 하는 거루...
                    if (await Sync(set, token))
                    {
                        continue;
                    }

                    failures.Add(id + "");
                    //}
                }
                if (failures.Count > 0)
                {
                    throw new NotImplementedException(string.Join("\t", failures.Select(i => $"{i}ls")));
                }
                throw new OperationCanceledException();
            }

            // 인자로 비트맵셋 ID를 주었을 때: 19293ls 2929ls ...
            foreach (Match arg in Regex.Matches(string.Join(" ", Args), @"(\d+)([^\s]*)"))
            {
                Set set = await Requests.GetSetFromAPIAsync(Convert.ToInt32(arg.Groups[1].Value), token)
                    ?? await Requests.GetSetFromDBAsync(Convert.ToInt32(arg.Groups[1].Value), token);
                if (set == null)
                {
                    failures.Add(arg.Groups[1].Value);
                    continue;
                }

                foreach (var op in arg.Groups[2].Value)
                {
                    if (op == 'l')
                    {
                        set.SyncOption |= SyncOption.SkipDownload;
                    }
                    else if (op == 's')
                    {
                        set.SyncOption |= SyncOption.KeepSyncedAt;
                    }
                }
                if (!await Sync(set, token))
                {
                    failures.Add(arg.Groups[1].Value);
                }
            }
            if (failures.Count > 0)
            {
                throw new NotImplementedException(string.Join("\t", failures.Select(i => $"{i}ls")));
            }

            if (Args[0] != "manage")
            {
                throw new OperationCanceledException();
            }
            Log.Writer = new StreamWriter(File.Open(Settings.LogPath + ".bot.log", FileMode.Create));
        }

        private async Task FetchAndSyncBeatmapsetsAsync(CancellationToken token = default)
        {
            var bucket = new Stack<Set>();
            var lastCheckTime = Settings.LastCheckTime;
            foreach (var r in Settings.BeatmapList)
            {
                var page = 1;
                do
                {
                    var ids = await OsuLegacyClient.GrabSetIDFromBeatmapListAsync(r, page, token);
                    if (!ids.Any())
                    {
                        break;
                    }

                    foreach (var id in ids)
                    {
                        var set = await Requests.GetSetFromAPIAsync(id, token);
                        if (set == null)
                        {
                            // ID NOT FOUND BUT CONTINUE
                            continue;
                        }

                        if (lastCheckTime < set.InfoChangedAt)
                        {
                            lastCheckTime = set.InfoChangedAt;
                        }
                        // 마지막 확인한 비트맵과 이 비트맵의 날짜를 비교 후 탐색 중지 여부 검사
                        // API에 정보가 늦게 등록될 수 있음 + 비트맵 리스트 캐시 피하기 위함
                        if (set.InfoChangedAt < Settings.LastCheckTime.AddHours(-12))
                        {
                            page = 0;
                            break;
                        }

                        var saved = await Requests.GetSetFromDBAsync(id, token);
                        // 랭크 상태가 다르거나, 수정 날짜가 다르면 업데이트
                        if ((
                                (set.StatusId > 0 && (saved == null || (saved.UpdatedAt < set.InfoChangedAt || saved.StatusId != set.StatusId)))
                                || (set.StatusId == 0 && (saved != null && (saved.UpdatedAt < set.InfoChangedAt || saved.StatusId != set.StatusId)))
                            )
                            && !bucket.Any(i => i.SetId == id))
                        {
                            bucket.Push(set);
                        }
                    }
                } while (page > 0 && page++ < 125);
            }
            while (bucket.Any())
            {
                if (!await Sync(bucket.Pop(), token))
                {
                    // 동기화에 실패한 비트맵을 다음 번에 다시 확인해 보아야 한다.
                    lastCheckTime = Settings.LastCheckTime;
                }
            }
            Settings.LastCheckTime = lastCheckTime;
        }

        /// <summary>
        /// 비트맵을 내려받고 DB에 등록합니다.
        /// </summary>
        /// <param name="set">기본 정보가 입력된 Set.
        /// 여기서 기본 정보는 <code>Id, Status, Beatmaps[i].BeatmapID, Beatmaps[i].Version, Beatmaps[i].Creator</code>입니다.</param>
        /// <param name="skipDownload"></param>
        /// <param name="keepSynced"></param>
        /// <returns></returns>
        private async Task<bool> Sync(Set set, CancellationToken token = default)
        {
            try
            {
                var path = await OsuLegacyClient.DownloadBeatmapsetAsync(set.SetId, null, set.SyncOption.HasFlag(SyncOption.SkipDownload), token);
                Log.Write(set.SetId + " DOWNLOADED");

                var local = Requests.GetSetFromLocal(set.SetId, path);
                local.Title = set.Title;
                local.Artist = set.Artist;
                local.Creator = set.Creator;
                local.CreatorId = set.CreatorId;
                local.StatusId = set.StatusId;
                local.RankedAt = set.RankedAt;
                //local.Favorites = set.Favorites;
                local.GenreId = set.GenreId;
                local.LanguageId = set.LanguageId;
                local.UpdatedAt = File.GetLastWriteTime(path);
                // 온라인 맵셋 정보를 로컬 맵셋 데이터에 추가
                local.Beatmaps = set.Beatmaps.Select(oBeatmap =>
                {
                    // BeatmapID로 찾지 않는 이유는
                    // 실제로 등록되지 않은 비트맵이 로컬 맵셋에 들어있을 때
                    // 이(백업 파일?)를 등록된 비트맵으로 착각할 수 있기 때문임.
                    var beatmap = local.Beatmaps.Find(lBeatmap =>
                        lBeatmap.BeatmapInfo.Hash.Equals(oBeatmap.BeatmapInfo.Hash)
                        || lBeatmap.BeatmapInfo.MD5Hash.Equals(oBeatmap.BeatmapInfo.MD5Hash)
                        // 해시값이 제공되는 이상 아래와 같은 비교는
                        // 적당한 데이터가 DB에 없고, 현재 온라인에서 삭제된 비트맵만...
                        || (
                            (
                                lBeatmap.BeatmapInfo.Version.Equals(oBeatmap.BeatmapInfo.Version)
                                // 비트맵 이름은 공백인데 Normal로 등록된 경우
                                // https://osu.ppy.sh/s/1785
                                || (
                                    string.IsNullOrEmpty(lBeatmap.BeatmapInfo.Version)
                                    && oBeatmap.BeatmapInfo.Version.Equals("Normal")
                                )
                            )
                            // 맵셋 등록자 이름과 비트맵 작성자 이름을 비교해서
                            // 참고용으로 넣은 파일이 등록되었는지 확인
                            && lBeatmap.Metadata.AuthorString.Equals(oBeatmap.Metadata.AuthorString)
                        ));
                    // 읭? 온라인엔 있는 비트맵이 로컬에 없다구??
                    // 다시 받아봐...
                    if (beatmap == null)
                    {
                        throw new EntryPointNotFoundException("온라인 비트맵과 정보가 일치하지 않습니다.");
                    }

                    //
                    // 온라인 데이터를 로컬 비트맵에 추가
                    // 별 영양가 있는 건 아니다.
                    //
                    beatmap.BeatmapInfo.OnlineBeatmapID = oBeatmap.BeatmapInfo.OnlineBeatmapID;
                    //beatmap.BeatmapInfo.Version = oBeatmap.BeatmapInfo.Version;
                    //TODO delete when it's implemented
                    beatmap.BeatmapInfo.StarDifficulty = oBeatmap.BeatmapInfo.StarDifficulty;
                    beatmap.StatusId = oBeatmap.StatusId;
                    return beatmap;
                }).ToList();
                // 짜집기 형식의 맵셋은 허용하지 않습니다...
                // 근데 osu!는 해주기 때문에... 언랭맵만 내가 좋아하는 대루..^^
                //if (set.StatusId == 0 && local.Beatmaps.GroupBy(i => $"{i.Metadata.Source}@{i.Metadata.Artist}|{i.Metadata.ArtistUnicode}\\{i.Metadata.Title}:{i.Metadata.TitleUnicode}%{i.Metadata.Tags}").Count() > 1)
                //{
                //    throw new EntryPointNotFoundException("메타데이터가 동일하지 않습니다.");
                //}
                Log.Write(set.SetId + " IS VALID");

                Register(local);
                Log.Write(set.SetId + " REGISTERED");

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

                return true;
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException)
            {
                return await Sync(set, token);
            }
            catch (EntryPointNotFoundException e)
            {
                Log.Level = 1;
                Log.Write(set.SetId + " CORRUPTED ENTRY");
                Log.Write(e.GetBaseException());
                Log.Level = 0;
            }
            catch (Exception e)
            {
                Log.Level = 1;
                Log.Write(set?.SetId + " " + e.GetBaseException() + ": " + e.Message);
                Log.Level = 0;
            }
            return false;
        }

        private void Register(Set set)
        {
            using (var conn = DB.Connect())
            using (var tr = conn.BeginTransaction())
            using (var query = conn.CreateCommand())
            {
                query.CommandText = "INSERT INTO gosu_sets (id, status, artist, artistU, title, titleU, creatorId, creator, genreId, languageId, source, tags, rankedAt, synced, keyword) " +
                    "VALUES (@i, @s, @a, @au, @t, @tu, @ci, @c, @gi, @li, @ss, @tg, @r, @sy, @k) " +
                    "ON DUPLICATE KEY UPDATE status = @s, artist = @a, artistU = @au, title = @t, titleU = @tu, creator = @c, genreId = @gi, languageId = @li, source = @ss, tags = @tg, rankedAt = @r, synced = @sy, keyword = @k";
                if (set.SyncOption.HasFlag(SyncOption.KeepSyncedAt))
                {
                    query.CommandText = query.CommandText.Replace("= @sy", "= synced");
                }
                query.Parameters.AddWithValue("@i", set.SetId);
                query.Parameters.AddWithValue("@s", set.StatusId);
                query.Parameters.AddWithValue("@a", set.Artist);
                query.Parameters.AddWithValue("@au", set.ArtistUnicode);
                query.Parameters.AddWithValue("@t", set.Title);
                query.Parameters.AddWithValue("@tu", set.TitleUnicode);
                query.Parameters.AddWithValue("@ci", set.CreatorId);
                query.Parameters.AddWithValue("@c", set.Creator);
                query.Parameters.AddWithValue("@gi", set.GenreId);
                query.Parameters.AddWithValue("@li", set.LanguageId);
                query.Parameters.AddWithValue("@ss", set.Source);
                query.Parameters.AddWithValue("@tg", set.Tags);
                query.Parameters.AddWithValue("@r", set.RankedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? null);
                query.Parameters.AddWithValue("@sy", set.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                query.Parameters.AddWithValue("@k", set.ToString());
                query.ExecuteNonQuery();

                query.CommandText = "DELETE FROM gosu_beatmaps WHERE setId = @i";
                query.ExecuteNonQuery();

                query.Parameters.Add("@d1", MySqlDbType.Float);
                query.Parameters.Add("@d2", MySqlDbType.Float);
                query.Parameters.Add("@d3", MySqlDbType.Float);
                query.Parameters.Add("@d4", MySqlDbType.Float);
                query.Parameters.Add("@b", MySqlDbType.Double);
                query.Parameters.Add("@l", MySqlDbType.Int32);
                query.Parameters.Add("@d0", MySqlDbType.Double);
                query.Parameters.Add("@h1", MySqlDbType.String);
                query.Parameters.Add("@h2", MySqlDbType.String);
                query.CommandText = "INSERT INTO gosu_beatmaps (setId, id, name, mode, hp, cs, od, ar, bpm, length, star, hash_md5, hash_sha2, author, status) " +
                    "VALUES (@i, @s, @a, @ci, @d1, @d2, @d3, @d4, @b, @l, @d0, @h1, @h2, @c, @gi)";
                foreach (var beatmap in set.Beatmaps)
                {
                    query.Parameters["@s"].Value = beatmap.BeatmapInfo.OnlineBeatmapID;
                    query.Parameters["@a"].Value = beatmap.BeatmapInfo.Version;
                    query.Parameters["@ci"].Value = beatmap.BeatmapInfo.RulesetID;
                    query.Parameters["@d1"].Value = beatmap.BeatmapInfo.BaseDifficulty.DrainRate;
                    query.Parameters["@d2"].Value = beatmap.BeatmapInfo.BaseDifficulty.CircleSize;
                    query.Parameters["@d3"].Value = beatmap.BeatmapInfo.BaseDifficulty.OverallDifficulty;
                    query.Parameters["@d4"].Value = beatmap.BeatmapInfo.BaseDifficulty.ApproachRate;
                    query.Parameters["@b"].Value = double.IsInfinity(beatmap.ControlPointInfo.BPMMode)
                        ? 0
                        : beatmap.ControlPointInfo.BPMMode;
                    query.Parameters["@l"].Value = beatmap.BeatmapInfo.OnlineInfo.Length;
                    query.Parameters["@d0"].Value = beatmap.BeatmapInfo.StarDifficulty;
                    query.Parameters["@h1"].Value = beatmap.BeatmapInfo.MD5Hash;
                    query.Parameters["@h2"].Value = beatmap.BeatmapInfo.Hash;
                    query.Parameters["@c"].Value = beatmap.BeatmapInfo.Metadata.AuthorString;
                    query.Parameters["@gi"].Value = beatmap.StatusId;
                    query.ExecuteNonQuery();
                }

                tr.Commit();
            }
        }
    }
}
