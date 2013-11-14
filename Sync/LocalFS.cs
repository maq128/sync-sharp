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

        public SortedList<string, SimpleDirInfo> getSubdirs( string path )
        {
            DirectoryInfo dir = new DirectoryInfo( this._root + path );
            DirectoryInfo[] subs = dir.GetDirectories();
            SortedList<string, SimpleDirInfo> result = new SortedList<string, SimpleDirInfo>();
            for ( int i = 0; i < subs.Length; i++ ) {
                SimpleDirInfo subdir = new SimpleDirInfo( this );
                subdir.Name = subs[i].Name;
                subdir.FullName = path + "/" + subs[i].Name;
                result.Add( subdir.Name, subdir );
            }
            return result;
        }

        public SortedList<string, SimpleFileInfo> getFiles( string path )
        {
            DirectoryInfo dir = new DirectoryInfo( this._root + path );
            FileInfo[] files = dir.GetFiles();
            SortedList<string, SimpleFileInfo> result = new SortedList<string, SimpleFileInfo>();
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
    }

}
