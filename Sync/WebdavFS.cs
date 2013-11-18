using System.IO;
using System.Collections.Generic;
using WebDav.Client;
using WebDav.Client.Exceptions;
using System.Net;

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

                // ��һ����������£�����Ŀ¼��Ҳ����Ϊһ����¼������ items ���Ҫ�ų�����
                // �ƺ��ڷ�������ĳ������ʱ�����������⣺
                //   1. ������������ IP ��ַ
                //   2. �˿ںŲ��� 80
                //   3. ·���г�������
                if ( item.Href.ToString() == url ) {
                    continue;
                }

                // �ڳ������������ͬʱ��DisplayName Ҳ�����ȫ·������Ҫ�޳��������ݡ�
                string name = item.DisplayName;
                int pos = name.LastIndexOf( '/' );
                if ( pos > 0 ) {
                    name = name.Substring( pos + 1 );
                }

                // BUG: �����еļӺ�δ����ȷ��ԭ������˿ո�

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

        override public string ToString()
        {
            return this._root;
        }
    }
}
