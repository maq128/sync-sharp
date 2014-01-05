using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using WebDav.Client;
using WebDav.Client.Exceptions;

namespace Sync
{
    public class WebdavFS : ISimpleFS
    {
        private string _root;
        WebDavSession _session;

        public WebdavFS( string root )
        {
            this._root = root;
            if ( ! this._root.EndsWith( "/" ) ) {
                this._root += "/";
            }
            this._session = new WebDavSession();

            ServicePoint sp = ServicePointManager.FindServicePoint( new Uri( this._root ) );
            sp.Expect100Continue = false;
        }

        public void setAuth( string username, string password )
        {
            this._session.Credentials = new NetworkCredential( username, password );
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

            IFolder folder = this._session.OpenFolder( url );
            IHierarchyItem[] items = folder.GetChildren();
            foreach ( IHierarchyItem item in items ) {

                // 在一种特殊情况下，“本目录”也会作为一条记录出现在 items 里，需要排除掉。
                // 似乎在符合以下某个条件时会出现这个问题：
                //   1. 用域名而不是 IP 地址
                //   2. 端口号不是 80
                //   3. 路径中出现中文
                if ( item.Href.ToString() == url ) {
                    continue;
                }

                // 在出现上面现象的同时，DisplayName 也变成了全路径，需要剔除多余内容。
                string name = item.DisplayName;
                int pos = name.LastIndexOf( '/' );
                if ( pos > 0 ) {
                    name = name.Substring( pos + 1 );
                }

                // BUG: 名字中的加号未能正确还原，变成了空格

                if ( item.ItemType == ItemType.Folder ) {
                    SimpleDirInfo subdir = new SimpleDirInfo( this );
                    subdir.Name = name;
                    subdir.FullName = path + "/" + name;
                    result.Add( subdir.Name, subdir );
                } else if ( item.ItemType == ItemType.Resource ) {
                    SimpleFileInfo file = new SimpleFileInfo( this );
                    file.Name = name;
                    file.FullName = path + "/" + name;
                    file.LastWriteTime = item.LastModified;
                    file.Length = 0;
                    foreach ( Property prop in item.Properties ) {
                        if ( prop.Name.ToString() == "getcontentlength" ) {
                            file.Length = System.Convert.ToInt32( prop.StringValue );
                            break;
                        }
                    }
                    result.Add( file.Name, file );
                }
            }
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
            WebDavSession sess = new WebDavSession();
            sess.Credentials = this._session.Credentials;
            IResource file = sess.OpenResource( url );
            file.Download( realpath );
        }

        public bool copyFileIn( SimpleFileInfo source )
        {
            string destUrl = this._root;
            if ( source.FullName.StartsWith( "/" ) ) {
                destUrl += source.FullName.Substring( 1 );
            } else {
                destUrl += source.FullName;
            }
            string tempUrl = destUrl + ".sync.temp";
            string tempFileName = Path.GetTempFileName();
            try {
                source.rootFS.copyFileOut( source.FullName, tempFileName );

                //string folderUrl = tempUrl.Substring( 0, tempUrl.LastIndexOf( "/" ) + 1 );
                //string fileName = tempUrl.Substring( tempUrl.LastIndexOf( "/" ) + 1 );
                //IFolder folder = _session.OpenFolder( folderUrl );
                //IResource file = folder.CreateResource( fileName );

                WebDavResource file = new WebDavResource();
                file.SetHref( new Uri( tempUrl ) );
                file.SetCredentials( this._session.Credentials );
                file.Upload( tempFileName );
            } catch ( Exception e ) {
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
