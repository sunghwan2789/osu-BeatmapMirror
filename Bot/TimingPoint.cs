using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class TimingPoint
    {
        public int Time;
        public double BeatLength;
        public int Meter = 4;

        public static TimingPoint tpParent;
        private TimingPoint Parent;

        public TimingPoint(string line)
        {
            var data = line.Split(',');
            if (data.Length < 3)
            {
                throw new FormatException();
            }

            this.Time = Convert.ToInt32(data[0]);
            this.BeatLength = Convert.ToDouble(data[1]);
            this.Meter = Convert.ToInt32(data[2]);

            if (this.BeatLength >= 0)
            {
                tpParent = this;
            }
            else
            {
                this.Parent = tpParent;
                var sliderVelocity = -100 / this.BeatLength;
                this.BeatLength = this.Parent.BeatLength / sliderVelocity;
                this.Meter = this.Parent.Meter;
            }
        }

        public double BPM
        {
            get
            {
                return BeatLength > 0 ? 60000 / this.BeatLength : -1;
            }
        }
    }
}
