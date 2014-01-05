using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Sync
{
    public class WebdavFS : ISimpleFS
    {
        private string _root;
        WebDAVClient _client;

        public WebdavFS( string root )
        {
            this._root = root;
            if ( ! this._root.EndsWith( "/" ) ) {
                this._root += "/";
            }

            this._client = new WebDAVClient();
            this._client.Server = new Uri( this._root ).GetLeftPart( UriPartial.Path );
        }

        public void setAuth( string username, string password )
        {
            this._client.User = username;
            this._client.Pass = password;
        }

        public SortedList<string, SimpleInfoBase> getChildren( string path )
        {
            string url = this._root;
            if ( path.StartsWith( "/" ) ) {
                url += path.Substring( 1 );
            } else {
                url += path;
            }
            if ( ! url.EndsWith( "/" ) ) {
                url += "/";
            }

            SortedList<string, SimpleInfoBase> result = new SortedList<string, SimpleInfoBase>();

            List<WebDAVClient.Res> list = this._client.List( path );
            foreach ( WebDAVClient.Res res in list ) {
                if ( res.GetType() == typeof( WebDAVClient.ResFile ) ) {
                    WebDAVClient.ResFile resfile = (WebDAVClient.ResFile)res;
                    SimpleFileInfo file = new SimpleFileInfo( this );
                    file.Name = resfile.Name;
                    file.FullName = path + "/" + resfile.Name;
                    file.LastWriteTime = resfile.LastWriteTime;
                    file.Length = resfile.Length;
                    result.Add( file.Name, file );
                } else {
                    SimpleDirInfo subdir = new SimpleDirInfo( this );
                    subdir.Name = res.Name;
                    subdir.FullName = path + "/" + res.Name;
                    result.Add( subdir.Name, subdir );
                }
            }

            //Console.WriteLine( "[" + path + "]" );
            //foreach ( KeyValuePair<string, SimpleInfoBase> item in result ) {
            //    string name = item.Key;
            //    SimpleInfoBase value = item.Value;
            //    if ( value.GetType() == typeof( SimpleDirInfo ) ) {
            //        Console.WriteLine( "  folder: " + value.Name );
            //    } else {
            //        Console.WriteLine( "  file  : " + value.Name );
            //    }
            //}

            return result;
        }

        public void copyFileOut( string sourcePath, string realpath )
        {
            string url = this._root;
            if ( sourcePath.StartsWith( "/" ) ) {
                url += sourcePath.Substring( 1 );
            } else {
                url += sourcePath;
            }
            //WebDavSession sess = new WebDavSession();
            //sess.Credentials = this._session.Credentials;
            //IResource file = sess.OpenResource( url );
            //file.Download( realpath );
        }

        public bool copyFileIn( SimpleFileInfo source )
        {
            //string destUrl = this._root;
            //if ( source.FullName.StartsWith( "/" ) ) {
            //    destUrl += source.FullName.Substring( 1 );
            //} else {
            //    destUrl += source.FullName;
            //}
            //string tempUrl = destUrl + ".sync.temp";
            //string tempFileName = Path.GetTempFileName();
            //try {
            //    source.rootFS.copyFileOut( source.FullName, tempFileName );

            //    //string folderUrl = tempUrl.Substring( 0, tempUrl.LastIndexOf( "/" ) + 1 );
            //    //string fileName = tempUrl.Substring( tempUrl.LastIndexOf( "/" ) + 1 );
            //    //IFolder folder = _session.OpenFolder( folderUrl );
            //    //IResource file = folder.CreateResource( fileName );

            //    WebDavResource file = new WebDavResource();
            //    file.SetHref( new Uri( tempUrl ) );
            //    file.SetCredentials( this._session.Credentials );
            //    file.Upload( tempFileName );
            //} catch ( Exception e ) {
            //    File.Delete( tempFileName );
            //    return false;
            //}
            //File.Delete( tempFileName );
            return true;
        }

        override public string ToString()
        {
            return this._root;
        }
    }
}
