using System;
using System.Collections.Generic;

namespace Sync
{
    public class SimpleInfoBase
    {
        // ����·��������
        public string Name;

        // ȫ·��������� root����
        // ��βһ��û�� /��
        // �����Ϊ�յĻ�����ͷһ����һ�� /��
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

    /* �򵥵��ļ�ϵͳ���ʽӿڡ�
     * ÿ��ʵ����Ӧ����һ�� root �ĸ��
     * �ӿ����漰���� path ��������� root ���Եġ�
     * path �е� \ ��ͬ�� /��
     * path Ϊ�ձ�ʾ��������
     * path �ǿ�ʱ����ͷӦ���� /����β��Ӧ���� /��
     */
    public interface ISimpleFS
    {
        /// <summary>
        /// ��ȡ path ·���µ���Ŀ¼��
        /// </summary>
        SortedList<string, SimpleDirInfo> getSubdirs( string path );

        /// <summary>
        /// ��ȡ path ·���µ��ļ���
        /// </summary>
        SortedList<string, SimpleFileInfo> getFiles( string path );
	}

}
