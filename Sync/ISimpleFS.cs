using System;
using System.Collections.Generic;

namespace Sync
{
    public class SimpleInfoBase
    {
        public ISimpleFS rootFS;

        // 不带路径的名字
        public string Name;

        // 全路径（相对于 root）。
        // 结尾一定没有 /。
        // 如果不为空的话，开头一定有一个 /。
        public string FullName;
    }

    public class SimpleDirInfo : SimpleInfoBase
    {
        public SimpleDirInfo( ISimpleFS rootFS )
        {
            this.rootFS = rootFS;
            this.Name = "";
            this.FullName = "";
        }

        public SortedList<string, SimpleInfoBase> getChildren()
        {
            return this.rootFS.getChildren( this.FullName );
        }

        // 基于 getChildren() 实现的便捷方法
        public SortedList<string, SimpleDirInfo> getSubdirs()
        {
            SortedList<string, SimpleDirInfo> result = new SortedList<string, SimpleDirInfo>();
            SortedList<string, SimpleInfoBase> children = getChildren();
            foreach ( KeyValuePair<string, SimpleInfoBase> item in children ) {
                string name = item.Key;
                SimpleInfoBase value = item.Value;
                if ( value.GetType() == typeof( SimpleDirInfo ) ) {
                    result.Add( name, (SimpleDirInfo)value );
                }
            }
            return result;
        }

        // 基于 getChildren() 实现的便捷方法
        public SortedList<string, SimpleFileInfo> getFiles()
        {
            SortedList<string, SimpleFileInfo> result = new SortedList<string, SimpleFileInfo>();
            SortedList<string, SimpleInfoBase> children = getChildren();
            foreach ( KeyValuePair<string, SimpleInfoBase> item in children ) {
                string name = item.Key;
                SimpleInfoBase value = item.Value;
                if ( value.GetType() == typeof( SimpleFileInfo ) ) {
                    result.Add( name, (SimpleFileInfo)value );
                }
            }
            return result;
        }
    }

    public class SimpleFileInfo : SimpleInfoBase
    {
        public DateTime LastWriteTime;
        public long Length;

        public SimpleFileInfo( ISimpleFS rootFS )
        {
            this.rootFS = rootFS;
            this.Name = "";
            this.FullName = "";
            this.LastWriteTime = new DateTime();
            this.Length = 0;
        }

        public SimpleFileInfo( SimpleFileInfo other )
        {
            this.rootFS = other.rootFS;
            this.Name = other.Name;
            this.FullName = other.FullName;
            this.LastWriteTime = other.LastWriteTime;
            this.Length = other.Length;
        }
    }

    /* 简单的文件系统访问接口。
     * 每个实现类应该有一个 root 的概念。
     * 接口中涉及到的 path 都是相对于 root 而言的。
     * path 中的 \ 视同于 /。
     * path 为空表示“根”。
     * path 非空时，开头应该有 /，结尾不应该有 /。
     */
    public interface ISimpleFS
    {
        /// <summary>
        /// 获取 path 路径下的所有子目录和文件。
        /// 根据返回结果集中对象的类型（SimpleDirInfo/SimpleFileInfo）可以区分子目录还是文件。
        /// </summary>
        SortedList<string, SimpleInfoBase> getChildren( string path );

        /// <summary>
        /// 把 sourcePath 指定的文件复制到 realpath 指定的位置。
        /// sourcePath 为本 FS 内部的绝对路径。
        /// realpath 为本地硬盘的绝对路径。
        /// </summary>
        void copyFileOut( string sourcePath, string realpath );

        /// <summary>
        /// 把 source 指定的文件从其所在的 FS 复制到本 FS。
        /// source 为源文件。
        /// </summary>
        bool copyFileIn( SimpleFileInfo source );
    }
}
