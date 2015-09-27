using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class Mania : Beatmap
    {
        public static int Id = 3;

        public Mania(string osu) : base(osu)
        {
            this.HitObjectTypes.Add(HitNote.Id, typeof(HitNote));
            this.HitObjectTypes.Add(HoldNote.Id, typeof(HoldNote));
            this.Load();
        }
    }
}
