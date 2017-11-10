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
        public string Creator => Metadata.AuthorString;
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




    }
}