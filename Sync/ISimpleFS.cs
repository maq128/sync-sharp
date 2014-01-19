using System;
using System.Collections.Generic;

namespace Sync
{
    public class SimpleInfoBase
    {
        public ISimpleFS rootFS;

        // ����·��������
        public string Name;

        // ȫ·��������� root����
        // ��βһ��û�� /��
        // �����Ϊ�յĻ�����ͷһ����һ�� /��
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

        // ���� getChildren() ʵ�ֵı�ݷ���
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

        // ���� getChildren() ʵ�ֵı�ݷ���
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

        // ���������Ϊ�ա������ڡ������ơ�ʱ��¼�Է��ļ����ԣ���Ҫ�� LastWriteTime��
        public SimpleFileInfo opposite;
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
        /// ��ȡ path ·���µ�������Ŀ¼���ļ���
        /// ���ݷ��ؽ�����ж�������ͣ�SimpleDirInfo/SimpleFileInfo������������Ŀ¼�����ļ���
        /// </summary>
        SortedList<string, SimpleInfoBase> getChildren( string path );

        /// <summary>
        /// �� sourcePath ָ�����ļ����Ƶ� realpath ָ����λ�á�
        /// </summary>
        /// <param name="sourcePath">Ϊ�� FS �ڲ��ľ���·����</param>
        /// <param name="realpath">Ϊ����Ӳ�̵ľ���·����</param>
        /// <param name="bForce">��ʾ�Ƿ�ǿ�Ƹ����ļ��� realpath���� bForce Ϊ false ʱ��ʵ�������
        /// ����������������Ǹ����ļ��� realpath ����ֱ�ӷ���Դ�ļ���</param>
        /// <returns>�����ļ��ı���Ӳ�̾���·�������ʵ�ʸ��Ƶ� realpath ָ���ļ��Ļ����򷵻� realpath��</returns>
        string getFileCopy( string sourcePath, string realpath, bool bForce );

        /// <summary>
        /// �� source ָ�����ļ��������ڵ� FS ���Ƶ��� FS��
        /// </summary>
        /// <param name="source">ΪԴ�ļ���</param>
        bool copyFileIn( SimpleFileInfo source );

        /// <summary>
        /// ɾ��ָ�����ļ���
        /// </summary>
        /// <param name="path">ָ�����ļ���</param>
        bool delFile( string path );

        /// <summary>
        /// ɾ��ָ����Ŀ¼��ֻ���ڸ�Ŀ¼Ϊ�յ�ʱ��Ż�ɾ����
        /// </summary>
        /// <param name="path">ָ����Ŀ¼��</param>
        bool delDir( string path );
    }
}
