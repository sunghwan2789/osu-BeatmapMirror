using System;
using System.IO;
using System.Text;

namespace Bot
{
    internal class Log
    {
        public static StreamWriter Writer = new StreamWriter(Console.OpenStandardOutput(), Encoding.Default);
        private static readonly object Locker = new object();
        public static int Level = 0;
        public static int WriteLevel = 0;

        public static void Write(object str)
        {
            if (Level < WriteLevel)
            {
                return;
            }
            lock (Locker)
            {
                Writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + Convert.ToString(str));
                Writer.Flush();
            }
        }
    }
}