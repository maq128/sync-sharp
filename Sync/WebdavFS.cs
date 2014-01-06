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

        private void createDirIfNecessary( string dir )
        {
            if ( Directory.Exists( dir ) )
                return;
            createDirIfNecessary( Path.GetDirectoryName( dir ) );
            Directory.CreateDirectory( dir );
        }

        public void copyFileOut( string sourcePath, string realpath )
        {
            string destFileName = realpath;
            createDirIfNecessary( Path.GetDirectoryName( destFileName ) );
            this._client.Download( sourcePath, realpath );
        }

        public bool copyFileIn( SimpleFileInfo source )
        {
            string tempDest = source.FullName + ".sync.temp";
            string tempFileName = Path.GetTempFileName();
            try {
                source.rootFS.copyFileOut( source.FullName, tempFileName );
                this._client.Upload( tempDest, tempFileName );

                // 设置时间
                this._client.SetLastWriteTime( tempDest, DateTime.Parse( "Mon, 12 Jan 1998 09:00:00 GMT" ) );

                // 删除旧文件, 新文件改名

            } catch ( Exception ) {
                File.Delete( tempFileName );
                return false;
            }
            File.Delete( tempFileName );
            return true;
        }

        override public string ToString()
        {
            return this._root;
        }
    }
}
