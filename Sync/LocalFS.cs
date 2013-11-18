using System.IO;
using System.Collections.Generic;

namespace Sync
{
    public class LocalFS : ISimpleFS
    {
        private string _root;

        public LocalFS( string root )
        {
            this._root = root;
        }

        public SortedList<string, SimpleInfoBase> getChildren( string path )
        {
            SortedList<string, SimpleInfoBase> result = new SortedList<string, SimpleInfoBase>();
            DirectoryInfo dir = new DirectoryInfo( this._root + path );

            DirectoryInfo[] subs = dir.GetDirectories();
            for ( int i = 0; i < subs.Length; i++ ) {
                SimpleDirInfo subdir = new SimpleDirInfo( this );
                subdir.Name = subs[i].Name;
                subdir.FullName = path + "/" + subs[i].Name;
                result.Add( subdir.Name, subdir );
            }

            FileInfo[] files = dir.GetFiles();
            for ( int i = 0; i < files.Length; i++ ) {
                SimpleFileInfo file = new SimpleFileInfo();
                file.Name = files[i].Name;
                file.FullName = path + "/" + files[i].Name;
                file.LastWriteTime = files[i].LastWriteTime;
                file.Length = files[i].Length;
                result.Add( file.Name, file );
            }
            return result;
        }

        override public string ToString()
        {
            return this._root;
        }
    }

}
