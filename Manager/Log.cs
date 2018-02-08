using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace Manager
{
    class Log
    {
        private static readonly StreamWriter Writer = new StreamWriter(Settings.LogPath, true);
        private static readonly object Locker = new object();

        public static void Write(object str)
        {
            lock (Locker)
            {
                Writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + Convert.ToString(str));
                Writer.Flush();
            }
        }
    }
}
