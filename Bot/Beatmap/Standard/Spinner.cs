using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class Spinner : HitCircle
    {
        new public static int Id = 8;

        public Spinner(string[] data, Beatmap caller) : base(data, caller)
        {
            this.EndTime = Convert.ToInt32(data[5]);
        }
    }
}
