using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class Spinner : HitCircle
    {
        public static int Id = 8;

        public Spinner(string[] data) : base(data)
        {
            this.EndTime = Convert.ToInt32(data[5]);
        }
    }
}
