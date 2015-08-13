using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Manager
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

        private static byte _TLSOnly = 2;

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

        private static string _Fallback = null;

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


        public static string DBServer
        {
            get
            {
                return Get("DB", "Server");
            }
        }

        public static string DBUserId
        {
            get
            {
                return Get("DB", "UserId");
            }
        }

        public static string DBPassword
        {
            get
            {
                return Get("DB", "Password");
            }
        }

        public static string DBDatabase
        {
            get
            {
                return Get("DB", "Database");
            }
        }


        private static string _Storage = null;
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


        private static string _SessionKey = null;

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

        private static string _SessionExpression = null;

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

        private static string _CreatorExpression = null;

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

        private static string _FavoriteExpression = null;

        /// <summary>
        /// osu! 비트맵 페이지에서 좋아요 정보를 긁는 정규식
        /// </summary>
        public static string FavoriteExpression
        {
            get
            {
                if (_FavoriteExpression == null)
                {
                    _FavoriteExpression = Get("EXP", "Favorite");
                }
                return _FavoriteExpression;
            }
        }



        private static string _LogFile = null;

        public static string LogFile
        {
            get
            {
                if (_LogFile == null)
                {
                    _LogFile = Get("ENV", "LogFile");
                }
                return _LogFile;
            }
        }

        private static int _ResponseTimeout = -1;

        /// <summary>
        /// 인터넷 탐색시 응답 시간 제한
        /// </summary>
        public static int ResponseTimeout
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
                return _ResponseTimeout;
            }
        }

        private static int _FavoriteMinimum = -1;

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

        private static int _SyncInterval = -1;

        public static int SyncInterval
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
                return _SyncInterval;
            }
        }


        private static string _Session = null;

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

        private static string _OsuId = null;

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
        private static string _OsuPw = null;

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
        private static string _APIKey = null;

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
    }
}
