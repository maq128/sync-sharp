using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Threading;

namespace Sync
{
    public class UnauthorizedException : System.Web.HttpException
    {
    }

    /*
        WebDAV 规范文本 RFC4918
        http://webdav.org/specs/rfc4918.html
    */
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
            propfind.Append( "  <prop xmlns:f=\"http://maq128.vicp.cc/ns\">" );
            propfind.Append( "    <getcontentlength/>" );
            propfind.Append( "    <getlastmodified/>" );
            propfind.Append( "    <f:mymodifiedtime/>" );
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
            using ( WebResponse response = HTTPRequest( listUri, "PROPFIND", headers, Encoding.UTF8.GetBytes( propfind.ToString() ), null, null ) ) {
                using ( Stream stream = response.GetResponseStream() ) {
                    XmlDocument xml = new XmlDocument();
                    xml.Load( stream );
                    XmlNamespaceManager xmlNsManager = new XmlNamespaceManager( xml.NameTable );
                    xmlNsManager.AddNamespace( "d", "DAV:" );
                    xmlNsManager.AddNamespace( "f", "http://maq128.vicp.cc/ns" );

                    foreach ( XmlNode node in xml.DocumentElement.ChildNodes ) {
                        XmlNode xmlNode = node.SelectSingleNode( "d:href", xmlNsManager );
                        string filepath = Uri.UnescapeDataString( xmlNode.InnerText );
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
                                res.Length = Int32.Parse( node.SelectSingleNode( "descendant::d:getcontentlength", xmlNsManager ).InnerText );
                                try {
                                    res.LastWriteTime = DateTime.Parse( node.SelectSingleNode( "descendant::f:mymodifiedtime", xmlNsManager ).InnerText );
                                } catch ( Exception ) {
                                    res.LastWriteTime = DateTime.Parse( node.SelectSingleNode( "descendant::d:getlastmodified", xmlNsManager ).InnerText );
                                }
                                list.Add( res );
                            }
                        }
                    }
                }
            }
            return list;
        }

        public void Download(String remoteFilePath, String localFilePath)
        {
            remoteFilePath = remoteFilePath.Trim( '/' );
            Uri downloadUri = new Uri( this.server + remoteFilePath );
            string method = WebRequestMethods.Http.Get.ToString();

            HTTPRequest( downloadUri, method, null, null, null, localFilePath );
        }

        public void Upload( String remoteFilePath, String localFilePath )
        {
            remoteFilePath = remoteFilePath.Trim( '/' );
            Uri uploadUri = new Uri( this.server + remoteFilePath );
            string method = WebRequestMethods.Http.Put.ToString();

            using ( HttpWebResponse response = (HttpWebResponse)HTTPRequest( uploadUri, method, null, null, localFilePath, null ) ) {
                int statusCode = (int)response.StatusCode;
            }
        }

        public void SetLastWriteTime( String remoteFilePath, DateTime time )
        {
            remoteFilePath = remoteFilePath.Trim( '/' );
            Uri updateUri = new Uri( this.server + remoteFilePath );

            StringBuilder proppatch = new StringBuilder();
            proppatch.Append( "<?xml version=\"1.0\" encoding=\"utf-8\" ?>" );
            proppatch.Append( "<propertyupdate xmlns=\"DAV:\" xmlns:f=\"http://maq128.vicp.cc/ns\">" );
            proppatch.Append( "  <set>" );
            proppatch.Append( "    <prop>" );
            proppatch.Append( "      <f:mymodifiedtime>" + time.ToUniversalTime().ToString( "R" ) + "</f:mymodifiedtime>" );
            proppatch.Append( "    </prop>" );
            proppatch.Append( "  </set>" );
            proppatch.Append( "</propertyupdate>" );

            using ( WebResponse response = HTTPRequest( updateUri, "PROPPATCH", null, Encoding.UTF8.GetBytes( proppatch.ToString() ), null, null ) ) {
            }
        }

        public void DeleteFile( String remoteFilePath )
        {
            remoteFilePath = remoteFilePath.Trim( '/' );
            Uri deleteUri = new Uri( this.server + remoteFilePath );

            using ( WebResponse response = HTTPRequest( deleteUri, "DELETE", null, null, null, null ) ) {
            }
        }

        public void DeleteDir( String remoteDirPath )
        {
            remoteDirPath = remoteDirPath.Trim( '/' );
            Uri deleteUri = new Uri( this.server + remoteDirPath + '/' );

            using ( WebResponse response = HTTPRequest( deleteUri, "DELETE", null, null, null, null ) ) {
            }
        }

        public void RenameTo( String remoteFilePath, String remoteFilePathTo )
        {
            remoteFilePath = remoteFilePath.Trim( '/' );
            remoteFilePathTo = remoteFilePathTo.Trim( '/' );
            Uri fromUri = new Uri( this.server + remoteFilePath );
            Uri toUri = new Uri( this.server + remoteFilePathTo );

            IDictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add( "Destination", toUri.AbsoluteUri );

            using ( WebResponse response = HTTPRequest( fromUri, "MOVE", headers, null, null, null ) ) {
            }
        }

        public void CreateDir( string remotePath )
        {
            remotePath = remotePath.Trim( '/' ) + "/";
            if ( remotePath == "/" ) remotePath = "";
            Uri createUri = new Uri( this.server + remotePath );

            string method = WebRequestMethods.Http.MkCol.ToString();

            using ( WebResponse response = HTTPRequest( createUri, method, null, null, null, null ) ) {
            }
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
        /// <param name="downloadFilePath"></param>
        WebResponse HTTPRequest( Uri uri, string requestMethod, IDictionary<string, string> headers, byte[] content, string uploadFilePath, string downloadFilePath )
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

            if ( headers != null ) {
                foreach ( string key in headers.Keys ) {
                    httpWebRequest.Headers.Set( key, headers[key] );
                }
            }

            if ( content != null || uploadFilePath != null ) {
                if ( content != null ) {
                    // 普通内容以同步方式提交
                    httpWebRequest.ContentLength = content.Length;
                    httpWebRequest.ContentType = "text/xml";
                    using ( Stream s = httpWebRequest.GetRequestStream() ) {
                        s.Write( content, 0, content.Length );
                    }
                } else {
                    // 上传文件以异步方式提交（避免受 httpWebRequest.Timeout 缺省 100 秒的限制）
                    httpWebRequest.SendChunked = true;
                    //httpWebRequest.ContentLength = new FileInfo( uploadFilePath ).Length;

                    ManualResetEvent reqDone = new ManualResetEvent( false );
                    Exception reqEx = null;
                    httpWebRequest.BeginGetRequestStream( ( IAsyncResult ar ) => {
                        try {
                            using ( Stream postStream = httpWebRequest.EndGetRequestStream( ar ) ) {
                                FileInfo fi = new FileInfo( uploadFilePath );
                                using ( FileStream fs = new FileStream( uploadFilePath, FileMode.Open, FileAccess.Read ) ) {
                                    ProcessDlg.reportMain( 0, String.Format( "上传: {0}\r\n字节: {1:N0}", fi.FullName, fi.Length ) );
                                    long lenSent = 0;
                                    byte[] buf = new byte[4096];
                                    int bytesRead = fs.Read( buf, 0, buf.Length );
                                    while ( bytesRead > 0 ) {
                                        lenSent += bytesRead;
                                        ProcessDlg.reportFile( (int)( lenSent * 100 / fi.Length ) );
                                        postStream.Write( buf, 0, bytesRead );
                                        bytesRead = fs.Read( buf, 0, buf.Length );
                                    }
                                }
                            }
                        } catch ( Exception e ) {
                            reqEx = e;
                        }
                        reqDone.Set();
                    }, null );
                    reqDone.WaitOne();

                    // 如果发生异常，则向外抛
                    if ( reqEx != null )
                        throw reqEx;
                }
            }

            // 普通内容采用同步方式接收，返回 response 由调用者处理
            if ( downloadFilePath == null ) {
                HttpWebResponse response;
                try {
                    response = (HttpWebResponse)httpWebRequest.GetResponse();
                } catch ( WebException ex ) {
                    // Try to fix a 401 exception by adding a Authorization header
                    if ( ex.Response == null || ( (HttpWebResponse)ex.Response ).StatusCode != HttpStatusCode.Unauthorized )
                        throw;

                    _auth.injectResponse( ex.Response );
                    throw new UnauthorizedException();
                }

                _auth.injectResponse( response );
                return response;
            }

            // 下载文件采用异步方式接收（避免受 httpWebRequest.Timeout 缺省 100 秒的限制），直接保存为文件
            ManualResetEvent respDone = new ManualResetEvent( false );
            Exception respEx = null;
            IAsyncResult result = (IAsyncResult)httpWebRequest.BeginGetResponse( ( IAsyncResult ar ) => {
                try {
                    using ( HttpWebResponse response = (HttpWebResponse)httpWebRequest.EndGetResponse( ar ) ) {
                        _auth.injectResponse( response );

                        int statusCode = (int)response.StatusCode;
                        long contentLength = int.Parse( response.GetResponseHeader( "Content-Length" ) );
                        long doneLength = 0;

                        ProcessDlg.reportMain( 0, String.Format( "下载: {0}\r\n字节: {1:N0}", uri.AbsoluteUri, contentLength ) );
                        if ( contentLength <= 0 )
                            contentLength = 1;
                        using ( Stream stream = response.GetResponseStream() ) {
                            using ( FileStream fs = new FileStream( downloadFilePath, FileMode.Create, FileAccess.Write ) ) {
                                byte[] buf = new byte[4096];
                                int bytesRead = 0;
                                do {
                                    bytesRead = stream.Read( buf, 0, buf.Length );
                                    doneLength += bytesRead;
                                    ProcessDlg.reportFile( (int)( doneLength * 100 / contentLength ) );

                                    fs.Write( buf, 0, bytesRead );
                                } while ( bytesRead > 0 );
                            }
                        }
                    }
                } catch ( Exception e ) {
                    respEx = e;
                }
                respDone.Set();
            }, null );
            respDone.WaitOne();

            // 如果发生异常，则向外抛
            if ( respEx != null )
                throw respEx;

            return null;
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
