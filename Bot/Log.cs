﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    class Log
    {
        public static int Flag
        {
            get; set;
        }

        public static void Write(object str)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + Flag + " " + Convert.ToString(str));
        }
    }
}