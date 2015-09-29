using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class HoldNote : HitNote
    {
        new public static int Id = 128;

        public HoldNote(string[] data, Beatmap caller) : base(data, caller)
        {
            this.EndTime = Convert.ToInt32(data[5].Split(':')[0]);
        }
    }
}
