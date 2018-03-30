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
        public int SetId { get; set; }

        private int? statusId = null;
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
        public int StatusId
        {
            get
            {
                return statusId ?? Beatmaps.OrderBy(i => i.StatusId).FirstOrDefault(i => i.StatusId > 0)?.StatusId ?? 0;
            }
            set
            {
                statusId = Math.Max(0, value);
            }
        }

        public BeatmapMetadata Metadata => Beatmaps.First().Metadata;

        //public string Title => Encoding.ASCII.GetString(Encoding.UTF8.GetBytes(Metadata.Title));
        private string title = null;
        public string Title
        {
            get
            {
                return title ?? Metadata.Title;
            }
            set
            {
                title = value;
            }
        }
        public string TitleUnicode
        {
            get
            {
                var unicode = Metadata.TitleUnicode;
                // old beatmap metadata
                if (string.IsNullOrEmpty(unicode))
                {
                    unicode = Metadata.Title;
                }
                return string.IsNullOrEmpty(unicode) || Title.Equals(unicode) ? null : unicode;
            }
        }
        private string artist = null;
        public string Artist
        {
            get
            {
                return artist ?? Metadata.Artist;
            }
            set
            {
                artist = value;
            }
        }
        public string ArtistUnicode
        {
            get
            {
                var unicode = Metadata.ArtistUnicode;
                // old beatmap metadata
                if (string.IsNullOrEmpty(unicode))
                {
                    unicode = Metadata.Artist;
                }
                return string.IsNullOrEmpty(unicode) || Artist.Equals(unicode) ? null : unicode;
            }
        }
        private string creator = null;
        public string Creator
        {
            get
            {
                return creator ?? Metadata.AuthorString;
            }
            set
            {
                creator = value;
            }
        }
        public int CreatorId
        {
            get
            {
                // 먼저, DB에서 해당 비트맵셋의 creatorID를 가져옵니다.
                // 전에 내려받은 비트맵을 갱신하는 경우, 캐시 사용
                using (var query = DB.Command)
                {
                    query.CommandText = "SELECT creatorId FROM gosu_sets WHERE id = @id";
                    query.Parameters.AddWithValue("@id", SetId);
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
                    var wr = Request.Context.Create("http://osu.ppy.sh/s/" + SetId);
                    using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
                    {
                        var beatmapPage = rp.ReadToEnd();
                        return Convert.ToInt32(Regex.Match(beatmapPage, Settings.CreatorExpression).Groups["id"].Value);
                    }
                }
                catch (WebException)
                {
                    return CreatorId;
                }
            }
        }

        public int GenreId { get; set; }
        public int LanguageId { get; set; }
        public int Favorites { get; set; }

        public string Source => Metadata.Source;
        public string Tags => Metadata.Tags;

        public DateTime? RankedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime InfoChangedAt => (RankedAt ?? UpdatedAt) > UpdatedAt
            ? (RankedAt ?? UpdatedAt)
            : UpdatedAt;

        public SyncOption SyncOption = SyncOption.Default;

        public string[] SearchableTerms => new[]
        {
            Title,
            TitleUnicode,
            $"({Artist})",
            $"({ArtistUnicode})",
            Source,
            Tags
        }.Concat(new[]
            {
                Creator
            }.Concat(Beatmaps.Select(i => i.BeatmapInfo.Metadata.AuthorString))
            .GroupBy(i => i)
            .Select(i => $"({i.Key})"))
        .Concat(Beatmaps.Select(i => i.BeatmapInfo.Version)
            .GroupBy(i => i)
            .Where(i => !string.IsNullOrEmpty(i.Key))
            .Select(i => $"[{i.Key}]"))
        .ToArray();

        public List<Bot.Beatmap> Beatmaps = new List<Beatmap>();


        public override string ToString()
        {
            return Regex.Replace(string.Join(" ", SearchableTerms), @"\s+", " ").ToUpper();
        }




    }
}