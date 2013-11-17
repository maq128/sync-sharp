using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;

namespace Sync
{
    // 参考资料: http://www.codeproject.com/Articles/159450/fastJSON

    [Serializable()]
    public class Config
    {
        // “目录对”列表
        public Dictionary<string, ComparePoint> cps { get; set; }

        // “身份证”列表
        public Dictionary<string, AuthInfo> auths { get; set; }

        static public Config load()
        {
            fastJSON.JSON.Instance.Parameters.UseEscapedUnicode = false;

            Config cfg = null;
            try {
                using ( StreamReader sr = new StreamReader( "sync.json" ) ) {
                    string jsonText = sr.ReadToEnd();
                    var obj = fastJSON.JSON.Instance.ToObject( jsonText );
                    cfg = (Config)obj;
                }
            } catch ( Exception ) {
                cfg = new Config();
            }

            return cfg;
        }

        public void save()
        {
            // TODO: 先清理无效数据（比如多余的身份信息）

            string jsonText = fastJSON.JSON.Instance.ToJSON( this );
            jsonText = fastJSON.JSON.Instance.Beautify( jsonText );
            using ( StreamWriter sw = new StreamWriter( "sync.json" ) ) {
                sw.Write( jsonText );
            }
        }

        public Config()
        {
            this.cps = new Dictionary<string, ComparePoint>();
            this.auths = new Dictionary<string, AuthInfo>();
        }

        public void setComparePoint( string name, string a, string b )
        {
            ComparePoint cp = new ComparePoint();
            cp.a = a;
            cp.b = b;
            this.cps[name] = cp;
        }

        public bool getComparePoint( string name, out string a, out string b )
        {
            if ( this.cps.ContainsKey( name ) ) {
                ComparePoint cp = this.cps[name];
                a = cp.a;
                b = cp.b;
                return true;
            }
            a = "";
            b = "";
            return false;
        }

        public void removeComparePoint( string name )
        {
            this.cps.Remove( name );
        }

        public void setAuthInfo( string name, string username, string password )
        {
            AuthInfo auth = new AuthInfo();
            auth.username = username;
            auth.password = password;
            this.auths[name] = auth;
        }

        public bool getAuthInfo( string name, out string username, out string password )
        {
            if ( this.auths.ContainsKey( name ) ) {
                AuthInfo auth = this.auths[name];
                username = auth.username;
                password = auth.password;
                return true;
            }
            username = "";
            password = "";
            return false;
        }

        public void removeAuthInfo( string name )
        {
            this.auths.Remove( name );
        }
    }

    [Serializable()]
    public class ComparePoint
    {
        public string a { get; set; }
        public string b { get; set; }
    }

    [Serializable()]
    public class AuthInfo
    {
        public string username { get; set; }
        public string password { get; set; }
    }
}
