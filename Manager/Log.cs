using System;
using System.IO;
using Utility;

namespace Manager
{
    internal class Log
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