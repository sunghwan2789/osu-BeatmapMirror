using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class Settings
    {
        private static readonly string Path = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "osu!BeatmapMirror.cfg");

        [DllImport("kernel32.dll")]
        private static extern void GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32.dll")]
        private static extern int WritePrivateProfileString(string section, string key, string val, string filePath);

        private static string Get(string section, string key)
        {
            var temp = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", temp, temp.Capacity, Path);
            return temp.ToString();
        }

        private static void Set(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, Path);
        }


        private static string _Prefix = null;
        private static byte _TLSOnly = 2;
        private static string _Fallback = null;
        /// <summary>
        /// 이 서버에 접속하기 위해 사용할 주소
        /// </summary>
        /// <remarks>
        /// 프로토콜을 생략한 주소를 사용합니다.
        /// 프로토콜을 제한하려면 <c>SSLOnly</c>를 설정하세요.
        /// </remarks>
        public static string Prefix
        {
            get
            {
                if (_Prefix == null)
                {
                    _Prefix = Get("WS", "Prefix");
                    if (!_Prefix.EndsWith("/"))
                    {
                        _Prefix += "/";
                    }
                }
                return _Prefix;
            }
        }
        /// <summary>
        /// 안전한 연결만 허용할지를 정합니다.
        /// </summary>
        public static bool TLSOnly
        {
            get
            {
                if (_TLSOnly == 2)
                {
                    try
                    {
                        _TLSOnly = Convert.ToByte(Get("WS", "TLSOnly"));
                        if (_TLSOnly > 2)
                        {
                            throw new Exception();
                        }
                    }
                    catch
                    {
                        _TLSOnly = 0;
                    }
                }
                return _TLSOnly == 1;
            }
        }
        /// <summary>
        /// 비정상적인 접속시 안내할 페이지 주소
        /// </summary>
        /// <remarks>
        /// 설정하지 않으면 HTTP 코드 400을 반환하고 연결을 끊습니다.
        /// </remarks>
        public static string Fallback
        {
            get
            {
                if (_Fallback == null)
                {
                    _Fallback = Get("WS", "Fallback");
                }
                return _Fallback;
            }
        }

        public static string DBConnectionString
        {
            get
            {
                return Get("DB", "ConnectionString");
            }
        }


        private static string _Storage = null;
        private static string _LogPath = null;
        private static int _ResponseTimeout = -1;
        private static int _FavoriteMinimum = -1;
        private static int _SyncInterval = -1;
        private static IEnumerable<int> _BeatmapList = null;
        /// <summary>
        /// 비트맵셋 파일 저장소
        /// </summary>
        public static string Storage
        {
            get
            {
                if (_Storage == null)
                {
                    _Storage = Get("ENV", "Storage");
                }
                return _Storage;
            }
        }
        public static string LogPath
        {
            get
            {
                if (_LogPath == null)
                {
                    _LogPath = Get("ENV", "LogPath");
                }
                return _LogPath;
            }
        }
        /// <summary>
        /// 인터넷 탐색시 응답 시간 제한
        /// </summary>
        public static TimeSpan ResponseTimeout
        {
            get
            {
                if (_ResponseTimeout < 0)
                {
                    try
                    {
                        _ResponseTimeout = Convert.ToInt32(Get("ENV", "ResponseTimeout")) * 1000;
                        if (_ResponseTimeout < 0)
                        {
                            throw new OverflowException();
                        }
                    }
                    catch
                    {
                        _ResponseTimeout = 5 * 1000;
                    }
                }
                return TimeSpan.FromMilliseconds(_ResponseTimeout);
            }
        }
        public static int FavoriteMinimum
        {
            get
            {
                if (_FavoriteMinimum < 0)
                {
                    try
                    {
                        _FavoriteMinimum = Convert.ToInt32(Get("ENV", "FavoriteMinimum"));
                        if (_FavoriteMinimum < 0)
                        {
                            throw new OverflowException();
                        }
                    }
                    catch
                    {
                        _FavoriteMinimum = 2;
                    }
                }
                return _FavoriteMinimum;
            }
        }
        public static TimeSpan SyncInterval
        {
            get
            {
                if (_SyncInterval < 0)
                {
                    try
                    {
                        _SyncInterval = Convert.ToInt32(Get("ENV", "SyncInterval")) * 1000;
                        if (_SyncInterval < 0)
                        {
                            throw new OverflowException();
                        }
                    }
                    catch
                    {
                        // 2015-08-11 기준 osu! Memcache Lifetime은 3시간
                        _SyncInterval = 60 * 60 * 3 * 1000;
                    }
                }
                return TimeSpan.FromMilliseconds(_SyncInterval);
            }
        }
        public static IEnumerable<int> BeatmapList
        {
            get
            {
                if (_BeatmapList == null)
                {
                    _BeatmapList = Get("ENV", "BeatmapList").Split(',').Select(i => Convert.ToInt32(i));
                }
                return _BeatmapList;
            }
        }


        private static string _SessionKey = null;
        private static string _SessionExpression = null;
        private static string _CreatorExpression = null;
        private static string _SetIdExpression = "";
        /// <summary>
        /// osu! 세션 쿠키의 이름
        /// </summary>
        public static string SessionKey
        {
            get
            {
                if (_SessionKey == null)
                {
                    _SessionKey = Get("EXP", "SessionKey");
                }
                return _SessionKey;
            }
        }
        /// <summary>
        /// osu! 세션의 정보를 긁는 정규식
        /// </summary>
        public static string SessionExpression
        {
            get
            {
                if (_SessionExpression == null)
                {
                    _SessionExpression = Get("EXP", "Session");
                }
                return _SessionExpression;
            }
        }
        /// <summary>
        /// osu! 비트맵 페이지에서 맵퍼 정보를 긁는 정규식
        /// </summary>
        public static string CreatorExpression
        {
            get
            {
                if (_CreatorExpression == null)
                {
                    _CreatorExpression = Get("EXP", "Creator");
                }
                return _CreatorExpression;
            }
        }
        /// <summary>
        /// osu! 비트맵 목록 페이지에서 맵셋 ID를 긁는 정규식
        /// </summary>
        public static string SetIdExpression
        {
            get
            {
                if (_SetIdExpression == "")
                {
                    _SetIdExpression = Get("EXP", "SetId");
                }
                return _SetIdExpression;
            }
        }


        private static string _Session = null;
        private static string _OsuId = null;
        private static string _OsuPw = null;
        private static string _APIKey = null;
        private static DateTime _LastCheckTime = DateTime.MinValue;
        private static DateTime _LastSummaryTime = DateTime.MinValue;
        /// <summary>
        /// 동기화 봇이 사용하는 세션 값
        /// </summary>
        public static string Session
        {
            get
            {
                if (_Session == null)
                {
                    _Session = Get("KEY", "Session");
                }
                return _Session;
            }
            set
            {
                _Session = value;
                Set("KEY", "Session", value);
            }
        }
        public static string OsuId
        {
            get
            {
                if (_OsuId == null)
                {
                    _OsuId = Get("KEY", "Id");
                }
                return _OsuId;
            }
        }
        public static string OsuPw
        {
            get
            {
                if (_OsuPw == null)
                {
                    _OsuPw = Get("KEY", "Pw");
                }
                return _OsuPw;
            }
        }
        public static string APIKey
        {
            get
            {
                if (_APIKey == null)
                {
                    _APIKey = Get("KEY", "API");
                }
                return _APIKey;
            }
        }
        public static DateTime LastCheckTime
        {
            get
            {
                if (_LastCheckTime == DateTime.MinValue)
                {
                    try
                    {
                        _LastCheckTime = Convert.ToDateTime(Get("KEY", "LastCheckTime"));
                    }
                    catch
                    {
                        _LastCheckTime = DateTime.MinValue;
                    }
                    if (_LastCheckTime == DateTime.MinValue)
                    {
                        _LastCheckTime = _LastCheckTime.AddTicks(1);
                    }
                }
                return _LastCheckTime;
            }
            set
            {
                _LastCheckTime = LastCheckTime;
                Set("KEY", "LastCheckTime", value.ToString("s"));
            }
        }
        public static DateTime LastSummaryTime
        {
            get
            {
                if (_LastSummaryTime == DateTime.MinValue)
                {
                    try
                    {
                        _LastSummaryTime = Convert.ToDateTime(Get("KEY", "LastSummaryTime"));
                    }
                    catch
                    {
                        _LastSummaryTime = DateTime.MinValue;
                    }
                    if (_LastSummaryTime == DateTime.MinValue)
                    {
                        _LastSummaryTime = _LastSummaryTime.AddTicks(1);
                    }
                }
                return _LastSummaryTime;
            }
            set
            {
                _LastSummaryTime = LastSummaryTime;
                Set("KEY", "LastSummaryTime", value.ToString("s"));
            }
        }

        public static string UserAgent
        {
            get => Get("KEY", "UserAgent");
            set => Set("KEY", "UserAgent", value);
        }

        public static CookieCollection CFSession
        {
            get
            {
                CookieCollection cookies = null;
                try
                {
                    using (var fs = new FileStream(Path + ".cfsession", FileMode.OpenOrCreate, FileAccess.Read))
                    {
                        cookies = new BinaryFormatter().Deserialize(fs) as CookieCollection;
                    }
                }
                catch {}
                return cookies ?? new CookieCollection();
            }
            set
            {
                using (var fs = new FileStream(Path + ".cfsession", FileMode.Truncate, FileAccess.Write))
                {
                    new BinaryFormatter().Serialize(fs, value);
                }
            }
        }
    }
}
