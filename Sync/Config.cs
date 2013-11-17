using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;

namespace Sync
{
    [Serializable()]
    public class Config
    {
        public Dictionary<string, ComparePoint> cps { get; set; }

        static public Config load()
        {
            // http://www.codeproject.com/Articles/159450/fastJSON

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

            string jsonText = fastJSON.JSON.Instance.ToJSON( this );
            jsonText = fastJSON.JSON.Instance.Beautify( jsonText );
            using ( StreamWriter sw = new StreamWriter( "sync.json" ) ) {
                sw.Write( jsonText );
            }
        }

        public Config()
        {
            this.cps = new Dictionary<string, ComparePoint>();
        }

        public void addComparePoint( string name, string a, string b )
        {
            ComparePoint cp = new ComparePoint();
            cp.name = name;
            cp.a = a;
            cp.b = b;
            this.cps[name] = cp;
        }
    }

    [Serializable()]
    public class ComparePoint
    {
        public string name { get; set; }
        public string a { get; set; }
        public string b { get; set; }
    }
}
