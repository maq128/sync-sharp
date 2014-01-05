using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace Sync
{
    public class UnauthorizedException : System.Web.HttpException
    {
    }

    public class WebDAVClient
    {
        #region WebDAV connection parameters
        private String server;
        /// <summary>
        /// Specify the WebDAV hostname (required).
        /// </summary>
        public String Server
        {
            get { return server; }
            set
            {
                value = value.TrimEnd( '/' );
                server = value + "/";

                basePath = new Uri( server ).AbsolutePath;
            }
        }
        private String basePath = "/";

        ///// <summary>
        ///// Specify the path of a WebDAV directory to use as 'root' (default: /)
        ///// </summary>
        //public String BasePath
        //{
        //    get { return basePath; }
        //    set
        //    {
        //        value = value.Trim( '/' );
        //        basePath = "/" + value + "/";
        //    }
        //}
        //private int? port = null;
        ///// <summary>
        ///// Specify an port (default: null = auto-detect)
        ///// </summary>
        //public int? Port
        //{
        //    get { return port; }
        //    set { port = value; }
        //}
        private String user;
        /// <summary>
        /// Specify a username (optional)
        /// </summary>
        public String User
        {
            get { return user; }
            set { user = value; }
        }
        private String pass;
        /// <summary>
        /// Specify a password (optional)
        /// </summary>
        public String Pass
        {
            get { return pass; }
            set { pass = value; }
        }
        private String domain = null;
        public String Domain
        {
            get { return domain; }
            set { domain = value; }
        }

        Uri getServerUrl( String path, Boolean appendTrailingSlash )
        {
            //String completePath = basePath;
            //if ( path != null ) {
            //    completePath += path.Trim( '/' );
            //}

            //if ( appendTrailingSlash && completePath.EndsWith( "/" ) == false ) { completePath += '/'; }

            //if ( port.HasValue ) {
            //    return new Uri( server + ":" + port + completePath );
            //} else {
            //    return new Uri( server + completePath );
            //}

            string completePath = this.server + path.TrimStart( '/' );
            if ( appendTrailingSlash ) {
                completePath = completePath.TrimEnd( '/' ) + "/";
            }
            return new Uri( completePath );
        }
        #endregion

        public class Res
        {
            public string Name;
        }

        public class ResFile : Res
        {
            public DateTime LastWriteTime;
            public long Length;
        }

        #region WebDAV operations
        /// <summary>
        /// List files in the root directory
        /// </summary>
        public List<Res> List()
        {
            return List( "/" );
        }

        /// <summary>
        /// List all files present on the server.
        /// </summary>
        /// <param name="remoteFilePath">List only files in this path</param>
        /// <returns>A list of files (entries without a trailing slash) and directories (entries with a trailing slash)</returns>
        public List<Res> List( String remoteFilePath )
        {
            // Uri should end with a trailing slash
            remoteFilePath = remoteFilePath.Trim( '/' ) + "/";
            if ( remoteFilePath == "/" ) remoteFilePath = "";
            Uri listUri = new Uri( this.server + remoteFilePath );
            string prefix = Uri.UnescapeDataString( listUri.AbsolutePath );

            // http://webdav.org/specs/rfc4918.html#METHOD_PROPFIND
            StringBuilder propfind = new StringBuilder();
            propfind.Append( "<?xml version=\"1.0\" encoding=\"utf-8\" ?>" );
            propfind.Append( "<propfind xmlns=\"DAV:\">" );
            propfind.Append( "  <prop>" );
            propfind.Append( "    <getcontentlength/>" );
            propfind.Append( "    <getlastmodified/>" );
            propfind.Append( "  </prop>" );
            propfind.Append( "</propfind>" );

            // Depth header: http://webdav.org/specs/rfc4918.html#rfc.section.9.1.4
            IDictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add( "Depth", "1" );

/*
            <?xml version="1.0" encoding="utf-8"?>
            <D:multistatus xmlns:D="DAV:" xmlns:ns0="DAV:">

            <D:response xmlns:lp1="DAV:" xmlns:lp2="http://apache.org/dav/props/" xmlns:g0="DAV:">
                <D:href>/maq/</D:href>
                <D:propstat>
                    <D:prop>
                        <lp1:getlastmodified>Sun, 29 Dec 2013 12:46:49 GMT</lp1:getlastmodified>
                    </D:prop>
                    <D:status>HTTP/1.1 200 OK</D:status>
                </D:propstat>
                <D:propstat>
                    <D:prop>
                        <g0:getcontentlength/>
                    </D:prop>
                    <D:status>HTTP/1.1 404 Not Found</D:status>
                </D:propstat>
            </D:response>

            <D:response xmlns:lp1="DAV:" xmlns:lp2="http://apache.org/dav/props/">
                <D:href>/maq/%e9%93%be%e5%ae%b6%e7%a7%9f%e9%87%91%e6%94%af%e4%bb%98%e8%a1%a8.xls</D:href>
                <D:propstat>
                    <D:prop>
                        <lp1:getcontentlength>17408</lp1:getcontentlength>
                        <lp1:getlastmodified>Sat, 21 Dec 2013 07:06:51 GMT</lp1:getlastmodified>
                    </D:prop>
                    <D:status>HTTP/1.1 200 OK</D:status>
                </D:propstat>
            </D:response>
*/
            List<Res> list = new List<Res>();
            using ( WebResponse response = HTTPRequest( listUri, "PROPFIND", headers, Encoding.UTF8.GetBytes( propfind.ToString() ), null ) ) {
                using ( Stream stream = response.GetResponseStream() ) {
                    XmlDocument xml = new XmlDocument();
                    xml.Load( stream );
                    XmlNamespaceManager xmlNsManager = new XmlNamespaceManager( xml.NameTable );
                    xmlNsManager.AddNamespace( "d", "DAV:" );

                    foreach ( XmlNode node in xml.DocumentElement.ChildNodes ) {
                        XmlNode xmlNode = node.SelectSingleNode( "d:href", xmlNsManager );
                        string filepath = Uri.UnescapeDataString( xmlNode.InnerXml );
                        string[] file = filepath.Split( new string[1] { prefix }, 2, StringSplitOptions.RemoveEmptyEntries );
                        if ( file.Length > 0 ) {
                            // Want to see directory contents, not the directory itself.
                            if ( file[file.Length - 1] == remoteFilePath || file[file.Length - 1] == server ) { continue; }

                            string name = file[file.Length - 1];
                            if ( name.EndsWith( "/" ) ) {
                                // 这是一个 folder
                                Res res = new Res();
                                res.Name = name.Substring( 0, name.Length - 1 );
                                list.Add( res );
                            } else {
                                // 这是一个 file
                                ResFile res = new ResFile();
                                res.Name = name;
                                res.LastWriteTime = DateTime.Parse( node.SelectSingleNode( "descendant::d:getlastmodified", xmlNsManager ).InnerText );
                                res.Length = Int32.Parse( node.SelectSingleNode( "descendant::d:getcontentlength", xmlNsManager ).InnerText );
                                list.Add( res );
                            }
                        }
                    }
                }
            }
            return list;
        }
        #endregion

        #region Server communication

        DigestAuth _auth;

        /// <summary>
        /// Perform the WebDAV call and fire the callback when finished.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="requestMethod"></param>
        /// <param name="headers"></param>
        /// <param name="content"></param>
        /// <param name="uploadFilePath"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        WebResponse HTTPRequest( Uri uri, string requestMethod, IDictionary<string, string> headers, byte[] content, string uploadFilePath )
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create( uri );
            httpWebRequest.Method = requestMethod;

            /*
             * The following line fixes an authentication problem explained here:
             * http://www.devnewsgroups.net/dotnetframework/t9525-http-protocol-violation-long.aspx
             */
            System.Net.ServicePointManager.Expect100Continue = false;

            if ( _auth == null ) {
                _auth = new DigestAuth();
            }
            _auth._user = User;
            _auth._pass = Pass;
            _auth.injectRequest( httpWebRequest );

            //if ( user != null && pass != null ) {
            //    if ( domain != null ) {
            //        httpWebRequest.Credentials = new NetworkCredential( user, pass, domain );
            //    } else {
            //        httpWebRequest.Credentials = new NetworkCredential( user, pass );
            //    }
            //}
            //httpWebRequest.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
            //httpWebRequest.PreAuthenticate = true;

            if ( headers != null ) {
                foreach ( string key in headers.Keys ) {
                    httpWebRequest.Headers.Set( key, headers[key] );
                }
            }

            if ( content != null || uploadFilePath != null ) {
                if ( content != null ) {
                    // The request either contains actual content...
                    httpWebRequest.ContentLength = content.Length;
                    httpWebRequest.ContentType = "text/xml";
                    Stream s = httpWebRequest.GetRequestStream();
                    s.Write( content, 0, content.Length );
                } else {
                    // ...or a reference to the file to be added as content.
                    //httpWebRequest.ContentLength = new FileInfo( uploadFilePath ).Length;
                    //asyncState.uploadFilePath = uploadFilePath;
                }
            }

            HttpWebResponse response;
            try {
                response = (HttpWebResponse)httpWebRequest.GetResponse();
            } catch ( WebException ex ) {
                // Try to fix a 401 exception by adding a Authorization header
                if ( ex.Response == null || ( (HttpWebResponse)ex.Response ).StatusCode != HttpStatusCode.Unauthorized )
                    throw;

                if ( User == null || Pass == null )
                    throw new UnauthorizedException();

                _auth.injectResponse( ex.Response );

                httpWebRequest = (HttpWebRequest)HttpWebRequest.Create( uri );
                httpWebRequest.Method = requestMethod;

                _auth.injectRequest( httpWebRequest );

                if ( headers != null ) {
                    foreach ( string key in headers.Keys ) {
                        httpWebRequest.Headers.Set( key, headers[key] );
                    }
                }

                if ( content != null || uploadFilePath != null ) {
                    if ( content != null ) {
                        // The request either contains actual content...
                        httpWebRequest.ContentLength = content.Length;
                        httpWebRequest.ContentType = "text/xml";
                        Stream s = httpWebRequest.GetRequestStream();
                        s.Write( content, 0, content.Length );
                    } else {
                        // ...or a reference to the file to be added as content.
                        //httpWebRequest.ContentLength = new FileInfo( uploadFilePath ).Length;
                        //asyncState.uploadFilePath = uploadFilePath;
                    }
                }

                try {
                    response = (HttpWebResponse)httpWebRequest.GetResponse();
                } catch ( WebException ) {
                    throw new UnauthorizedException();
                }
            }

            _auth.injectResponse( response );
            return response;
        }
        #endregion
    }

    // Disget 算法
    // http://stackoverflow.com/questions/594323/implement-digest-authentication-via-httpwebrequest-in-c-sharp
    // http://hi.baidu.com/thinkinginlamp/item/77d3b7c014510962f7c95dd2

    // res: WWW-Authenticate: Digest realm="DeviceUser", nonce="AATvOCO9qUw=aeb0bb24c1993434e11bc8b6e7373832b7d655a1", algorithm=MD5, domain="/shares/ /shares/maq/ /maq/", qop="auth"
    // req: Authorization: Digest username="admin",realm="DeviceUser",nonce="AATvOCO9qUw=aeb0bb24c1993434e11bc8b6e7373832b7d655a1",uri="/maq/",algorithm="MD5",cnonce="9c0b3e4a2940694b2fe4ff1821548c71",nc=00000001,qop="auth",response="0ba8e3bb479ef18563d5caed6bb273b2"
    // res: Authentication-Info: rspauth="ee2503cffe9fe2fccc63b5dd17be349d", cnonce="9c0b3e4a2940694b2fe4ff1821548c71", nc=00000001, qop=auth

    public class DigestAuth
    {
        public string _user;
        public string _pass;
        string _realm;
        string _nonce;
        string _qop;
        string _cnonce;
        int _nc;

        public void injectRequest( WebRequest req )
        {
            if ( _nc == 0 )
                return;
            string authstr = GetDigestHeader( req.Method, req.RequestUri.AbsolutePath );
            req.Headers.Set( "Authorization", authstr );
            _nc++;
        }

        public void injectResponse( WebResponse resp )
        {
            var wwwAuthenticateHeader = resp.Headers["WWW-Authenticate"];
            if ( wwwAuthenticateHeader != null ) {
                _realm = GrabHeaderVar( "realm", wwwAuthenticateHeader );
                _nonce = GrabHeaderVar( "nonce", wwwAuthenticateHeader );
                _qop = GrabHeaderVar( "qop", wwwAuthenticateHeader );

                _nc = 1;
                _cnonce = new Random().Next( 123400, 9999999 ).ToString();
                return;
            }

            wwwAuthenticateHeader = resp.Headers["Authentication-Info"];
            if ( wwwAuthenticateHeader != null ) {
                _cnonce = GrabHeaderVar( "cnonce", wwwAuthenticateHeader );
                _qop = GrabHeaderVar( "qop", wwwAuthenticateHeader );
                _nc = Int32.Parse( GrabHeaderVar( "nc", wwwAuthenticateHeader ) ) + 1;
            }
        }

        string CalculateMd5Hash( string input )
        {
            var inputBytes = Encoding.ASCII.GetBytes( input );
            var hash = System.Security.Cryptography.MD5.Create().ComputeHash( inputBytes );
            var sb = new StringBuilder();
            foreach ( var b in hash )
                sb.Append( b.ToString( "x2" ) );
            return sb.ToString();
        }

        string GrabHeaderVar( string varName, string header )
        {
            var regHeader = new System.Text.RegularExpressions.Regex( string.Format( @"{0}=""?([^"", ]*)""?", varName ) );
            var matchHeader = regHeader.Match( header );
            if ( matchHeader.Success )
                return matchHeader.Groups[1].Value;
            throw new ApplicationException( string.Format( "Header {0} not found", varName ) );
        }

        string GetDigestHeader( string method, string uri )
        {
            var ha1 = CalculateMd5Hash( string.Format( "{0}:{1}:{2}", _user, _realm, _pass ) );
            var ha2 = CalculateMd5Hash( string.Format( "{0}:{1}", method, uri ) );
            var digestResponse =
                CalculateMd5Hash( string.Format( "{0}:{1}:{2:00000000}:{3}:{4}:{5}", ha1, _nonce, _nc, _cnonce, _qop, ha2 ) );

            return string.Format( "Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", " +
                "algorithm=MD5, response=\"{4}\", qop={5}, nc={6:00000000}, cnonce=\"{7}\"",
                _user, _realm, _nonce, uri, digestResponse, _qop, _nc, _cnonce );
        }
    }
}
