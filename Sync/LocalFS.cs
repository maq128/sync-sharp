using System;
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
                SimpleFileInfo file = new SimpleFileInfo( this );
                file.Name = files[i].Name;
                file.FullName = path + "/" + files[i].Name;
                file.LastWriteTime = files[i].LastWriteTime;
                file.Length = files[i].Length;
                result.Add( file.Name, file );
            }
            return result;
        }

        public string getFileCopy( string sourcePath, string realpath, bool bForce )
        {
            if ( bForce ) {
                string sourceFileName = this._root + sourcePath;
                string destFileName = realpath;
                Directory.CreateDirectory( Path.GetDirectoryName( destFileName ) );
                // ²Î¿¼×ÊÁÏ
                // http://www.pinvoke.net/default.aspx/kernel32.CopyFileEx
                File.Copy( sourceFileName, destFileName, true );
                return realpath;
            }
            return this._root + sourcePath;
        }

        public bool copyFileIn( SimpleFileInfo source )
        {
            string destFileName = Path.GetFullPath( this._root + source.FullName );
            string tempFileName = destFileName + ".sync.temp";
            try {
                source.rootFS.getFileCopy( source.FullName, tempFileName, true );
                File.SetLastWriteTime( tempFileName, source.LastWriteTime );
                File.Delete( destFileName );
                File.Move( tempFileName, destFileName );
            } catch ( Exception ) {
                File.Delete( tempFileName );
                return false;
            }
            return true;
        }

        public bool del( string path )
        {
            try {
                Console.WriteLine( "delete: " + this._root + path );
                File.Delete( this._root + path );
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
