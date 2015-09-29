using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Bot
{
    internal class Beatmap
    {
        public static Dictionary<int, Type> Modes = new Dictionary<int, Type>
        {
            { Standard.Id, typeof(Standard) },
            { Taiko.Id, typeof(Taiko) },
            { CatchTheBeat.Id, typeof(CatchTheBeat) },
            { Mania.Id, typeof(Mania) }
        };
        public Dictionary<int, Type> HitObjectTypes = new Dictionary<int, Type>
        {
            { 0, typeof(HitObject) }
        };

        // [General]
        public int Mode
        {
            get; set;
        }

        // [Metadata]
        private string _Version;
        public string Title
        {
            get; set;
        }
        public string TitleUnicode
        {
            get; set;
        }
        public string Artist
        {
            get; set;
        }
        public string ArtistUnicode
        {
            get; set;
        }
        public string Creator
        {
            get; set;
        }
        public string Version
        {
            get
            {
                return string.IsNullOrEmpty(_Version) ? "Normal" : _Version;
            }
            set
            {
                _Version = value;
            }
        }
        public string Source
        {
            get; set;
        }
        public string Tags
        {
            get; set;
        }
        public int BeatmapID
        {
            get; set;
        }

        // [Difficulty]
        private double _ApproachRate;
        public double HPDrainRate
        {
            get; set;
        }
        public double CircleSize
        {
            get; set;
        }
        public double OverallDifficulty
        {
            get; set;
        }
        public double ApproachRate
        {
            get
            {
                return _ApproachRate < 0 ? OverallDifficulty : _ApproachRate;
            }
            set
            {
                _ApproachRate = value;
            }
        }
        public double SliderMultiplier
        {
            get; set;
        }

        // [TimingPoints]
        public List<TimingPoint> TimingPoints;
        public double BPM
        {
            get
            {
                var timingPoint = TimingPoints.FirstOrDefault();
                return timingPoint != null ? timingPoint.BPM : 0;
            }
        }

        // [HitObjects]
        public List<HitObject> HitObjects;
        /// <summary>
        /// 비트맵의 플레이 시간을 올림
        /// </summary>
        public int Length
        {
            get
            {
                var hitObjectL = HitObjects.LastOrDefault();
                var hitObjectF = HitObjects.FirstOrDefault();
                if (hitObjectL == null)
                {
                    return 0;
                }
                if (hitObjectF.Time == hitObjectL.EndTime)
                {
                    return 1;
                }
                return (int) Math.Ceiling((hitObjectL.EndTime - hitObjectF.Time) / 1000.0);
            }
        }

        public Beatmap()
        {
            Mode = 0;
            HPDrainRate = 5;
            CircleSize = 5;
            OverallDifficulty = 5;
            ApproachRate = -1;
            SliderMultiplier = 1.4;
            this.TimingPoints = new List<TimingPoint>();
            this.HitObjects = new List<HitObject>();
        }

        private string[] stream;
        public Beatmap(string osu) : this()
        {
            this.stream = osu.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        public static Beatmap Parse(string osu)
        {
            var mode = 0;
            var match = Regex.Match(osu, @"[\r\n]Mode.*?:(.*?)[\r\n]", RegexOptions.Singleline);
            if (match.Success)
            {
                mode = Convert.ToInt32(match.Groups[1].Value);
            }
            if (!Modes.ContainsKey(mode))
            {
                var beatmap = new Beatmap(osu);
                // Length 계산 힘들어 Mode 없음 해놨는데, 필요할 듯
                // beatmap.Mode = -1;
                beatmap.Load();
                return beatmap;
            }
            return (Beatmap) Activator.CreateInstance(Modes[mode], osu);
        }

        public int mask;
        public void Load()
        {
            this.mask = this.HitObjectTypes.Keys.Aggregate((a, b) => a | b);

            var currentSection = "";
            foreach (var line in this.stream)
            {
                // 주석
                if (line.StartsWith("//"))
                {
                    continue;
                }

                if (line.StartsWith("["))
                {
                    currentSection = line.Substring(1, line.IndexOf("]") - 1);
                    continue;
                }

                switch (currentSection)
                {
                    case "General":
                    case "Metadata":
                    case "Difficulty":
                    {
                        var pair = line.Split(new[] { ':' }, 2);
                        // 빈 줄인지 검사..
                        if (pair.Length != 2)
                        {
                            continue;
                        }
                        // if (key in this): from osu!Preview
                        var property = this.GetType().GetProperty(pair[0].Trim());
                        if (property != null)
                        {
                            property.SetValue(this, Convert.ChangeType(pair[1].Trim(), property.PropertyType));
                        }
                        break;
                    }
                    case "TimingPoints":
                    {
                        try
                        {
                            this.TimingPoints.Add(new TimingPoint(line));
                        }
                        catch { }
                        break;
                    }
                    case "HitObjects":
                    {
                        try
                        {
                            this.HitObjects.Add(HitObject.Parse(line, this));
                        }
                        catch { }
                        break;
                    }
                }
            }
        }

        public int TimingPointIndexAt(int time)
        {
            var begin = 0;
            var end = this.TimingPoints.Count - 1;
            while (begin <= end)
            {
                var mid = (begin + end) / 2;
                if (time >= this.TimingPoints[mid].Time)
                {
                    if (mid + 1 == this.TimingPoints.Count ||
                        time < this.TimingPoints[mid + 1].Time)
                    {
                        return mid;
                    }
                    begin = mid + 1;
                }
                else
                {
                    end = mid - 1;
                }
            }
            return 0;
        }

        public TimingPoint TimingPointAt(int time)
        {
            return this.TimingPoints[TimingPointIndexAt(time)];
        }
    }
}
