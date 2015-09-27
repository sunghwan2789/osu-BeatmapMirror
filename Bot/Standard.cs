using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class Standard : Beatmap
    {
        public static int Id = 0;

        public Standard(string osu) : base(osu)
        {
            this.HitObjectTypes.Add(HitCircle.Id, typeof(HitCircle));
            this.HitObjectTypes.Add(Slider.Id, typeof(Slider));
            this.HitObjectTypes.Add(Spinner.Id, typeof(Spinner));
            base.Load();
        }
    }
}
