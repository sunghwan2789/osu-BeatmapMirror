using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class HitObject
    {
        public Beatmap Constructor;

        public int Time;
        public int EndTime;

        public HitObject(string[] data)
        {
            this.Time = Convert.ToInt32(data[2]);
            this.EndTime = this.Time;
        }

        public static HitObject Parse(string line, Beatmap caller)
        {
            var data = line.Split(',');
            if (data.Length < 5)
            {
                throw new FormatException();
            }

            var type = Convert.ToInt32(data[3]) & caller.mask;
            if (!caller.HitObjectTypes.ContainsKey(type))
            {
                return new HitObject(data);
            }

            var hitObject = (HitObject) Activator.CreateInstance(caller.HitObjectTypes[type], data);
            hitObject.Constructor = caller;
            return hitObject;
        }
    }
}
