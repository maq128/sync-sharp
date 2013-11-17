using System.IO;
using System.Collections.Generic;
using WebDav.Client;
using WebDav.Client.Exceptions;
using System.Net;

namespace Sync
{
    public class WebdavFS : ISimpleFS
    {
        public delegate bool RequireAuthHandler( string name, out string username, out string password );

        private string _root;
        RequireAuthHandler _callback;
        private string _username;
        private string _password;
        WebDavSession _session;

        public WebdavFS( string root, RequireAuthHandler callback )
        {
            this._root = root;
            if ( ! this._root.EndsWith( "/" ) ) {
                this._root += "/";
            }
            this._callback = callback;
            this._username = "";
            this._password = "";

            this._session = new WebDavSession();
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

            IFolder folder = null;
            while ( true ) {
                try {
                    folder = this._session.OpenFolder( url );
                    break;
                } catch ( UnauthorizedException ) {
                    if ( this._callback( url, out this._username, out this._password ) ) {
                        this._session.Credentials = new NetworkCredential( this._username, this._password );
                    } else {
                        throw new System.Exception( "没有权限访问 " + url );
                    }
                }
            }

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
                    SimpleFileInfo file = new SimpleFileInfo();
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
    }
}
