using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using TreeViewWithCheckBoxes;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;

using WebDav.Client;
using System.Net;

namespace Sync
{
    /// <summary>
    /// MainWin.xaml 的交互逻辑
    /// </summary>
    public partial class MainWin : Window
    {
        NameComparer<DirectoryInfo> _dirNameComparer;
        NameComparer<FileInfo> _fileNameComparer;

        public MainWin()
        {
            InitializeComponent();

            DirectoryInfo cwd = new DirectoryInfo( Directory.GetCurrentDirectory() );
            DirectoryInfo home = cwd.Parent.Parent.Parent;
            textDirA.Text = home.FullName + "\\测试目录A";
            textDirB.Text = home.FullName + "\\测试目录B";

            _dirNameComparer = new NameComparer<DirectoryInfo>();
            _fileNameComparer = new NameComparer<FileInfo>();
        }

        BackgroundWorker _worker;
        bool _cancel;
        int _progress;

        private void btnCompare_Click( object sender, RoutedEventArgs e )
        {
            WebDavSession session = new WebDavSession();
            session.Credentials = new NetworkCredential( "admin", "*" );
            IFolder folder = session.OpenFolder( "http://192.168.1.80/maq/" );
            IHierarchyItem[] items = folder.GetChildren();
            foreach ( IHierarchyItem item in items ) {
                Console.WriteLine( item.DisplayName );
            }
            return;

            FooViewModel rootAonly = FooViewModel.CreateRootItem( "仅在A中存在的文件" );
            FooViewModel rootAnewer = FooViewModel.CreateRootItem( "在A中较新的文件" );
            FooViewModel rootAB = FooViewModel.CreateRootItem( "在AB中相同的文件" );
            FooViewModel rootBnewer = FooViewModel.CreateRootItem( "在B中较新的文件" );
            FooViewModel rootBonly = FooViewModel.CreateRootItem( "仅在B中存在的文件" );

            DirectoryInfo dirA = new DirectoryInfo( textDirA.Text );
            DirectoryInfo dirB = new DirectoryInfo( textDirB.Text );

            ProcessDlgWorkingHandler fnWorking = delegate( BackgroundWorker worker ) {
                _worker = worker;
                _cancel = false;
                _progress = 0;
                compareTree( dirA, dirB, rootAonly, rootAnewer, rootAB, rootBnewer, rootBonly );

                return _cancel;
            };

            ProcessDlgFinishHandler fnFinish = delegate() {
                rootAonly.IsExpanded = true;
                rootAnewer.IsExpanded = true;
                rootAB.IsExpanded = true;
                rootBnewer.IsExpanded = true;
                rootBonly.IsExpanded = true;

                treeAony.DataContext = new List<FooViewModel> { rootAonly };
                treeAnewer.DataContext = new List<FooViewModel> { rootAnewer };
                treeAB.DataContext = new List<FooViewModel> { rootAB };
                treeBnewer.DataContext = new List<FooViewModel> { rootBnewer };
                treeBonly.DataContext = new List<FooViewModel> { rootBonly };

                tabControl1.SelectedIndex = 2;
            };

            ProcessDlg dlg = new ProcessDlg( fnWorking, fnFinish );
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void reportProgress()
        {
            _worker.ReportProgress( _progress++ );
            if ( _progress > 100 ) {
                _progress = 0;
            }
        }

        private void compareTree( DirectoryInfo dirA, DirectoryInfo dirB, FooViewModel parentAonly, FooViewModel parentAnewer, FooViewModel parentAB, FooViewModel parentBnewer, FooViewModel parentBonly )
        {
            if ( _worker.CancellationPending || _cancel ) {
                _cancel = true;
                return;
            }

            reportProgress();

            DirectoryInfo[] subDirsA = dirA.GetDirectories();
            DirectoryInfo[] subDirsB = dirB.GetDirectories();

            // 对 A、B 中同时存在的子目录进行遍历
            foreach ( DirectoryInfo a in subDirsA.Intersect( subDirsB, _dirNameComparer ) ) {
                DirectoryInfo b = new DirectoryInfo( dirB.FullName + "\\" + a.Name );

                FooViewModel subAonly = parentAonly.CreateFolderItem( a.Name );
                FooViewModel subAnewer = parentAnewer.CreateFolderItem( a.Name );
                FooViewModel subAB = parentAB.CreateFolderItem( a.Name );
                FooViewModel subBnewer = parentBnewer.CreateFolderItem( b.Name );
                FooViewModel subBonly = parentBonly.CreateFolderItem( b.Name );

                compareTree( a, b, subAonly, subAnewer, subAB, subBnewer, subBonly );
                if ( subAonly.Children.Count > 0 ) {
                    parentAonly.Children.Add( subAonly );
                }
                if ( subAnewer.Children.Count > 0 ) {
                    parentAnewer.Children.Add( subAnewer );
                }
                if ( subAB.Children.Count > 0 ) {
                    parentAB.Children.Add( subAB );
                }
                if ( subBnewer.Children.Count > 0 ) {
                    parentBnewer.Children.Add( subBnewer );
                }
                if ( subBonly.Children.Count > 0 ) {
                    parentBonly.Children.Add( subBonly );
                }
            }

            // 对仅在 A 中存在的子目录进行遍历
            foreach ( DirectoryInfo a in subDirsA.Except( subDirsB, _dirNameComparer ) ) {
                parentAonly.Children.Add( parentAonly.CreateLazyFolderItem( a.FullName ) );
            }

            // 对仅在 B 中存在的子目录进行遍历
            foreach ( DirectoryInfo b in subDirsB.Except( subDirsA, _dirNameComparer ) ) {
                parentBonly.Children.Add( parentBonly.CreateLazyFolderItem( b.FullName ) );
            }

            FileInfo[] subFilesA = dirA.GetFiles();
            FileInfo[] subFilesB = dirB.GetFiles();

            // 对 A、B 中同时存在的文件进行遍历
            foreach ( FileInfo a in subFilesA.Intersect( subFilesB, _fileNameComparer ) ) {
                FileInfo b = new FileInfo( dirB.FullName + "\\" + a.Name );

                if ( a.LastWriteTime > b.LastWriteTime.AddSeconds( 5 ) ) {
                    // A > B
                    parentAnewer.Children.Add( parentAnewer.CreateFileItem( a.Name ) );
                } else if ( a.LastWriteTime < b.LastWriteTime.AddSeconds( -5 ) ) {
                    // A < B
                    parentBnewer.Children.Add( parentBnewer.CreateFileItem( a.Name ) );
                } else {
                    // A = B
                    parentAB.Children.Add( parentAB.CreateFileItem( a.Name ) );
                }
            }

            // 对仅在 A 中存在的文件进行遍历
            foreach ( FileInfo a in subFilesA.Except( subFilesB, _fileNameComparer ) ) {
                parentAonly.Children.Add( parentAonly.CreateFileItem( a.Name ) );
            }

            // 对仅在 B 中存在的文件进行遍历
            foreach ( FileInfo b in subFilesB.Except( subFilesA, _fileNameComparer ) ) {
                parentBonly.Children.Add( parentBonly.CreateFileItem( b.Name ) );
            }
        }
    }

    public class NameComparer<T> : IEqualityComparer<T> where T : FileSystemInfo
    {
        public NameComparer() { }

        public bool Equals( T x, T y )
        {
            return x.Name.ToLower() == y.Name.ToLower();
        }

        public int GetHashCode( T obj )
        {
            return obj.Name.ToLower().GetHashCode();
        }
    }
}
