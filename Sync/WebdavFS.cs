using System.IO;
using System.Collections.Generic;
using WebDav.Client;
using System.Net;

namespace Sync
{
    public class WebdavFS : ISimpleFS
    {
        private string _root;
        private string _username;
        private string _password;

        public WebdavFS( string root, string user, string pass )
        {
            this._username = user;
            this._password = pass;
            this._root = root;
        }

        public SortedList<string, SimpleDirInfo> getSubdirs( string path )
        {
            WebDavSession session = new WebDavSession();
            session.Credentials = new NetworkCredential( this._username, this._password );
            IFolder folder = session.OpenFolder( this._root + path );
            IHierarchyItem[] items = folder.GetChildren();
            SortedList<string, SimpleDirInfo> result = new SortedList<string, SimpleDirInfo>();
            foreach ( IHierarchyItem item in items ) {
                if ( item.ItemType != ItemType.Folder ) {
                    continue;
                }
                SimpleDirInfo subdir = new SimpleDirInfo( this );
                subdir.Name = item.DisplayName;
                subdir.FullName = path + "/" + item.DisplayName;
                result.Add( subdir.Name, subdir );
            }
            return result;
        }

        public SortedList<string, SimpleFileInfo> getFiles( string path )
        {
            WebDavSession session = new WebDavSession();
            session.Credentials = new NetworkCredential( this._username, this._password );
            IFolder folder = session.OpenFolder( this._root + path );
            IHierarchyItem[] items = folder.GetChildren();
            SortedList<string, SimpleFileInfo> result = new SortedList<string, SimpleFileInfo>();
            foreach ( IHierarchyItem item in items ) {
                if ( item.ItemType != ItemType.Resource ) {
                    continue;
                }
                SimpleFileInfo file = new SimpleFileInfo();
                file.Name = item.DisplayName;
                file.FullName = path + "/" + item.DisplayName;
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
            return result;
        }
    }

}
