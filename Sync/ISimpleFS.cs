using System;
using System.Collections.Generic;

namespace Sync
{
    public class SimpleInfoBase
    {
        // 不带路径的名字
        public string Name;

        // 全路径（相对于 root）。
        // 结尾一定没有 /。
        // 如果不为空的话，开头一定有一个 /。
        public string FullName;
    }

    public class SimpleDirInfo : SimpleInfoBase
    {
        public ISimpleFS rootFS;
        public SimpleDirInfo( ISimpleFS rootFS )
        {
            this.rootFS = rootFS;
            this.Name = "";
            this.FullName = "";
        }

        public SortedList<string, SimpleDirInfo> getSubdirs()
        {
            return this.rootFS.getSubdirs( this.FullName );
        }

        public SortedList<string, SimpleFileInfo> getFiles()
        {
            return this.rootFS.getFiles( this.FullName );
        }

        public SimpleDirInfo getSubdir( string name )
        {
            SimpleDirInfo subdir = new SimpleDirInfo( this.rootFS );
            subdir.Name = name;
            subdir.FullName = this.FullName + '/' + name;
            return subdir;
        }

    }

    public class SimpleFileInfo : SimpleInfoBase
    {
        public DateTime LastWriteTime;
        public long Length;
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
        /// 获取 path 路径下的子目录。
        /// </summary>
        SortedList<string, SimpleDirInfo> getSubdirs( string path );

        /// <summary>
        /// 获取 path 路径下的文件。
        /// </summary>
        SortedList<string, SimpleFileInfo> getFiles( string path );
	}

}
