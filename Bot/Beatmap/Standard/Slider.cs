using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class Slider : HitCircle
    {
        new public static int Id = 2;

        public Slider(string[] data, Beatmap caller) : base(data, caller)
        {
            var repeat = Convert.ToInt32(data[6]);
            var pixelLength = Convert.ToDouble(data[7]);

            var sliderTime = this.Constructor.TimingPointAt(this.Time).BeatLength * (
                pixelLength / this.Constructor.SliderMultiplier
            ) / 100;
            this.EndTime += (int) (sliderTime * repeat);
        }
    }
}
