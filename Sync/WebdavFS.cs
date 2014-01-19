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

            return result;
        }

        public string getFileCopy( string sourcePath, string realpath, bool bForce )
        {
            Directory.CreateDirectory( Path.GetDirectoryName( realpath ) );
            this._client.Download( sourcePath, realpath );
            return realpath;
        }

        public bool copyFileIn( SimpleFileInfo source )
        {
            string tempFileName = Path.GetTempFileName();
            try {
                // 上传到一个临时文件
                string tempDest = source.FullName + ".sync.temp";

                // 创建所需的目录
                createDirIfNecessary( getDirectoryName( tempDest ) );

                // 上传
                string fileCopy = source.rootFS.getFileCopy( source.FullName, tempFileName, false );
                this._client.Upload( tempDest, fileCopy );

                // 设置时间
                this._client.SetLastWriteTime( tempDest, source.LastWriteTime );

                // 临时文件改名覆盖目标文件
                this._client.RenameTo( tempDest, source.FullName );

            } catch ( Exception ) {
                File.Delete( tempFileName );
                return false;
            }
            File.Delete( tempFileName );
            return true;
        }

        private string getDirectoryName( string remotePath )
        {
            remotePath = remotePath.Trim( '/' );
            int pos = remotePath.LastIndexOf( '/' );
            if ( pos < 0 )
                return "";
            return remotePath.Substring( 0, pos + 1 );
        }

        List<string> _exist_dirs = new List<string>();
        private void createDirIfNecessary( string remotePath )
        {
            string[] segs = remotePath.Split( new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries );
            string dir = "";
            foreach ( string seg in segs ) {
                dir += seg + "/";
                if ( _exist_dirs.Contains( dir ) )
                    continue;
                try {
                    this._client.CreateDir( dir );
                } catch ( Exception ) {
                }
                _exist_dirs.Add( dir );
            }
        }

        public bool delFile( string path )
        {
            try {
                this._client.DeleteFile( path );
            } catch ( Exception ) {
                return false;
            }
            return true;
        }

        public bool delDir( string path )
        {
            try {
                SortedList<string, SimpleInfoBase> children = this.getChildren( path );
                if ( children.Count > 0 )
                    return false;
                this._client.DeleteDir( path );
            } catch ( Exception ) {
                return false;
            }
            return true;
        }

        override public string ToString()
        {
            return this._root;
        }
    }
}
