using osu.Game.Beatmaps.ControlPoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class Beatmap : osu.Game.Beatmaps.Beatmap
    {
        private int statudId = 0;
        public int StatusId
        {
            get
            {
                return statudId;
            }
            set
            {
                statudId = Math.Max(0, value);
            }
        }
    }
}
