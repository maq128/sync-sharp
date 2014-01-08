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

using System.Collections;
using System.Net;

namespace Sync
{
    /// <summary>
    /// MainWin.xaml 的交互逻辑
    /// </summary>
    public partial class MainWin : Window
    {
        public MainWin()
        {
            InitializeComponent();

            _config = Config.load();

            // 仅用于调试目的
            if ( _config.cps.Count() == 0 ) {
                DirectoryInfo cwd = new DirectoryInfo( Directory.GetCurrentDirectory() );
                DirectoryInfo home = cwd.Parent.Parent.Parent;
                textDirA.Text = home.FullName + "\\测试目录A";
                textDirB.Text = home.FullName + "\\测试目录B";

                _config.setComparePoint( "debug", textDirA.Text, textDirB.Text );
                _config.save();
            }

            if ( _config.cps.Count() > 0 ) {
                ComparePoint cp = _config.cps.First().Value;
                textDirA.Text = cp.a;
                textDirB.Text = cp.b;
            }
        }

        Config _config;
        BackgroundWorker _worker;
        int _scanCount;

        ISimpleFS aFS;
        ISimpleFS bFS;

        private ISimpleFS createFS( string name )
        {
            if ( name.StartsWith( "http://" ) || name.StartsWith( "https://" ) ) {
                WebdavFS fs = new WebdavFS( name );
                while ( true ) {
                    try {
                        // 验证是否具有访问权限
                        fs.getChildren( "" );
                        break;
                    } catch ( Sync.UnauthorizedException ) {
                        // 从配置文件中取出身份信息
                        string username = "";
                        string password = "";
                        bool bSaved = this._config.getAuthInfo( name, out username, out password );

                        WebdavAuthDlg dlgAuth = new WebdavAuthDlg();
                        dlgAuth.Owner = this;
                        dlgAuth.labelUrl.Content = name;
                        dlgAuth.textUsername.Text = username;
                        dlgAuth.textPassword.Password = password;
                        dlgAuth.cbSavePassword.IsChecked = bSaved;
                        bool? ok = dlgAuth.ShowDialog();
                        if ( ok.HasValue && (bool)ok ) {
                            fs.setAuth( dlgAuth.textUsername.Text, dlgAuth.textPassword.Password );

                            if ( dlgAuth.cbSavePassword.IsChecked.HasValue && (bool)dlgAuth.cbSavePassword.IsChecked ) {
                                if ( dlgAuth.textUsername.Text != username || dlgAuth.textPassword.Password != password ) {
                                    // 如果选择了“保存密码”，而且身份信息有修改，则保存入配置文件
                                    this._config.setAuthInfo( name, dlgAuth.textUsername.Text, dlgAuth.textPassword.Password );
                                    this._config.save();
                                }
                            } else {
                                if ( bSaved ) {
                                    // 如果未选择“保存密码”，而配置文件中已经保存了身份信息，则删除掉
                                    this._config.removeAuthInfo( name );
                                    this._config.save();
                                }
                            }
                        } else {
                            return null;
                        }
                    }
                }
                return fs;
            }
            return new LocalFS( name );
        }

        private void connectModelToButtons( FooViewModel model, Button[] buttons )
        {
            foreach ( Button btn in buttons ) {
                btn.IsEnabled = false;
            }

            model.PropertyChanged += ( object m, PropertyChangedEventArgs ev ) => {
                if ( ev.PropertyName != "IsChecked" )
                    return;
                bool en = !model.IsChecked.HasValue || (bool)model.IsChecked;
                foreach ( Button btn in buttons ) {
                    btn.IsEnabled = en;
                }
            };
        }

        private void btnCompare_Click( object sender, RoutedEventArgs e )
        {
            // 清空列表控件
            treeAonly.DataContext = new List<FooViewModel>();
            treeAnewer.DataContext = new List<FooViewModel>();
            treeAB.DataContext = new List<FooViewModel>();
            treeBnewer.DataContext = new List<FooViewModel>();
            treeBonly.DataContext = new List<FooViewModel>();

            // 创建文件系统访问对象
            aFS = createFS( textDirA.Text );
            bFS = createFS( textDirB.Text );

            if ( aFS == null || bFS == null )
                return;

            FooViewModel rootAonly = FooViewModel.CreateRootItem( "仅在A中存在的文件", aFS );
            FooViewModel rootAnewer = FooViewModel.CreateRootItem( "在A中较新的文件", null );   //
            FooViewModel rootAB = FooViewModel.CreateRootItem( "在AB中相同的文件", null );      // 不会存在 lazy-item，所以不需要 FS 支持
            FooViewModel rootBnewer = FooViewModel.CreateRootItem( "在B中较新的文件", null );   //
            FooViewModel rootBonly = FooViewModel.CreateRootItem( "仅在B中存在的文件", bFS );

            // 根据节点的选择情况设置操作按钮的使能状态
            connectModelToButtons( rootAonly, new Button[] { btnCopy_a, btnDelete_a } );
            connectModelToButtons( rootAnewer, new Button[] { btnCopy_an, btnRCopy_an } );
            connectModelToButtons( rootBnewer, new Button[] { btnCopy_bn, btnRCopy_bn } );
            connectModelToButtons( rootBonly, new Button[] { btnCopy_b, btnDelete_b } );

            SimpleDirInfo dirA = new SimpleDirInfo( aFS );
            SimpleDirInfo dirB = new SimpleDirInfo( bFS );

            DoWorkEventHandler fnWorking = delegate( object worker, DoWorkEventArgs ev ) {
                // 这段代码将在辅助线程中执行
                _worker = (BackgroundWorker)worker;
                try {
                    this._scanCount = 0;
                    compareTree( dirA, dirB, rootAonly, rootAnewer, rootAB, rootBnewer, rootBonly );
                } catch ( Exception ex ) {
                    // 如果不是因为主动要求中止，则向外抛错
                    if ( ! _worker.CancellationPending ) {
                        throw ex;
                    }
                }
                ev.Cancel = _worker.CancellationPending;
            };

            ProcessDlg dlg = new ProcessDlg( fnWorking, this );
            bool? finished = dlg.ShowDialog();
            if ( finished.HasValue && (bool)finished ) {
                treeAonly.DataContext = new List<FooViewModel> { rootAonly };
                treeAnewer.DataContext = new List<FooViewModel> { rootAnewer };
                treeAB.DataContext = new List<FooViewModel> { rootAB };
                treeBnewer.DataContext = new List<FooViewModel> { rootBnewer };
                treeBonly.DataContext = new List<FooViewModel> { rootBonly };

                rootAonly.IsExpanded = true;
                rootAnewer.IsExpanded = true;
                rootAB.IsExpanded = true;
                rootBnewer.IsExpanded = true;
                rootBonly.IsExpanded = true;

                tabControl1.SelectedIndex = 2;
            }
        }

        private void compareTree( SimpleDirInfo dirA, SimpleDirInfo dirB, FooViewModel parentAonly, FooViewModel parentAnewer, FooViewModel parentAB, FooViewModel parentBnewer, FooViewModel parentBonly )
        {
            if ( _worker.CancellationPending ) {
                throw new Exception( "取消了操作" );
            }
            _worker.ReportProgress( 0, String.Format("扫描文件夹: {0}", ++this._scanCount) );

            // 读取 A、B 的所有子项
            SortedList<string, SimpleInfoBase> aChildren = dirA.getChildren();
            SortedList<string, SimpleInfoBase> bChildren = dirB.getChildren();

            // 分离出子目录和文件
            SortedList<string, SimpleDirInfo> subDirsA = new SortedList<string, SimpleDirInfo>();
            SortedList<string, SimpleDirInfo> subDirsB = new SortedList<string, SimpleDirInfo>();

            SortedList<string, SimpleFileInfo> subFilesA = new SortedList<string, SimpleFileInfo>();
            SortedList<string, SimpleFileInfo> subFilesB = new SortedList<string, SimpleFileInfo>();

            foreach ( KeyValuePair<string, SimpleInfoBase> item in aChildren ) {
                string name = item.Key;
                SimpleInfoBase value = item.Value;
                if ( value.GetType() == typeof( SimpleDirInfo ) ) {
                    subDirsA.Add( name, (SimpleDirInfo)value );
                } else {
                    subFilesA.Add( name, (SimpleFileInfo)value );
                }
            }

            foreach ( KeyValuePair<string, SimpleInfoBase> item in bChildren ) {
                string name = item.Key;
                SimpleInfoBase value = item.Value;
                if ( value.GetType() == typeof( SimpleDirInfo ) ) {
                    subDirsB.Add( name, (SimpleDirInfo)value );
                } else {
                    subFilesB.Add( name, (SimpleFileInfo)value );
                }
            }

            // ---- 比对子目录

            // 分拣出 A、B 共有的那些子目录
            SortedList<string, SimpleDirInfo[]> subDirsAB = new SortedList<string, SimpleDirInfo[]>();
            foreach ( KeyValuePair<string, SimpleDirInfo> item in subDirsA ) {
                string name = item.Key;
                if ( subDirsB.ContainsKey( name ) ) {
                    SimpleDirInfo[] abPair = new SimpleDirInfo[2];
                    abPair[0] = item.Value;
                    subDirsB.TryGetValue( name, out abPair[1] );
                    subDirsAB.Add( name, abPair );
                    subDirsB.Remove( name );
                }
            }
            foreach ( KeyValuePair<string, SimpleDirInfo[]> item in subDirsAB ) {
                string name = item.Key;
                subDirsA.Remove( name );
            }

            // 对 A、B 中同时存在的子目录进行遍历
            foreach ( KeyValuePair<string, SimpleDirInfo[]> item in subDirsAB ) {
                SimpleDirInfo a = item.Value[0];
                SimpleDirInfo b = item.Value[1];

                FooViewModel subAonly = parentAonly.CreateFolderItem( a );
                FooViewModel subAnewer = parentAnewer.CreateFolderItem( a );
                FooViewModel subAB = parentAB.CreateFolderItem( a );
                FooViewModel subBnewer = parentBnewer.CreateFolderItem( b );
                FooViewModel subBonly = parentBonly.CreateFolderItem( b );

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
            foreach ( KeyValuePair<string, SimpleDirInfo> item in subDirsA ) {
                SimpleDirInfo a = item.Value;
                parentAonly.Children.Add( parentAonly.CreateLazyFolderItem( a ) );
            }

            // 对仅在 B 中存在的子目录进行遍历
            foreach ( KeyValuePair<string, SimpleDirInfo> item in subDirsB ) {
                SimpleDirInfo b = item.Value;
                parentBonly.Children.Add( parentBonly.CreateLazyFolderItem( b ) );
            }

            // ---- 比对文件

            // 分拣出 A、B 共有的那些文件
            SortedList<string, SimpleFileInfo[]> subFilesAB = new SortedList<string, SimpleFileInfo[]>();
            foreach ( KeyValuePair<string, SimpleFileInfo> item in subFilesA ) {
                string name = item.Key;
                if ( subFilesB.ContainsKey( name ) ) {
                    SimpleFileInfo[] abPair = new SimpleFileInfo[2];
                    abPair[0] = item.Value;
                    subFilesB.TryGetValue( name, out abPair[1] );
                    subFilesAB.Add( name, abPair );
                    subFilesB.Remove( name );
                }
            }
            foreach ( KeyValuePair<string, SimpleFileInfo[]> item in subFilesAB ) {
                string name = item.Key;
                subFilesA.Remove( name );
            }

            // 对 A、B 中同时存在的文件进行遍历
            foreach ( KeyValuePair<string, SimpleFileInfo[]> item in subFilesAB ) {
                SimpleFileInfo a = item.Value[0];
                SimpleFileInfo b = item.Value[1];

                if ( a.LastWriteTime > b.LastWriteTime.AddSeconds( 5 ) ) {
                    // A > B
                    parentAnewer.Children.Add( parentAnewer.CreateFileItem( a ) );
                } else if ( a.LastWriteTime < b.LastWriteTime.AddSeconds( -5 ) ) {
                    // A < B
                    parentBnewer.Children.Add( parentBnewer.CreateFileItem( a ) );
                } else {
                    // A = B
                    parentAB.Children.Add( parentAB.CreateFileItem( a ) );
                }
            }

            // 对仅在 A 中存在的文件进行遍历
            foreach ( KeyValuePair<string, SimpleFileInfo> item in subFilesA ) {
                SimpleFileInfo a = item.Value;
                parentAonly.Children.Add( parentAonly.CreateFileItem( a ) );
            }

            // 对仅在 B 中存在的文件进行遍历
            foreach ( KeyValuePair<string, SimpleFileInfo> item in subFilesB ) {
                SimpleFileInfo b = item.Value;
                parentBonly.Children.Add( parentBonly.CreateFileItem( b ) );
            }
        }

        private void doCopy( FooViewModel model, ISimpleFS to )
        {
            // 收集出所有待复制的文件
            List<SimpleFileInfo> colls = TreeWalker.walk( model, this );
            if ( colls == null )
                return;

            // 逐个复制文件
            DoWorkEventHandler fnWorking = delegate( object worker, DoWorkEventArgs ev ) {
                BackgroundWorker _worker = (BackgroundWorker)worker;
                foreach ( SimpleFileInfo source in colls ) {
                    if ( _worker.CancellationPending )
                        break;
                    _worker.ReportProgress( 0 );

                    bool ok = to.copyFileIn( source );
                    if ( !ok ) {
                        // FIXME: 这里辅助线程不能正确显示 UI 对话框
                        MessageBoxResult res = MessageBox.Show(
                            this,
                            "无法复制以下文件：\r\n  " + source.FullName + "\r\n\r\n要继续复制其余的文件吗？\r\n点击“取消”将停止复制。",
                            "操作失败",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Error
                        );
                        if ( res != MessageBoxResult.OK )
                            break;
                    }
                }
                ev.Cancel = _worker.CancellationPending;
            };

            ProcessDlg dlg = new ProcessDlg( fnWorking, this );
            bool? finished = dlg.ShowDialog();
            if ( finished.HasValue && (bool)finished ) {
            }
        }

        private void doRCopy( FooViewModel model, ISimpleFS from )
        {
            // 收集出所有待复制的文件
            List<SimpleFileInfo> colls = TreeWalker.walk( model, this );
            if ( colls == null )
                return;

            // 逐个复制文件
            DoWorkEventHandler fnWorking = delegate( object worker, DoWorkEventArgs ev ) {
                BackgroundWorker _worker = (BackgroundWorker)worker;
                foreach ( SimpleFileInfo dest in colls ) {
                    if ( _worker.CancellationPending )
                        break;
                    _worker.ReportProgress( 0 );

                    // FIXME: 这里的 SimpleFileInfo 对象属性应该是“源文件”的才对（主要是最后修改时间）
                    SimpleFileInfo source = new SimpleFileInfo( dest );
                    source.rootFS = from;
                    bool ok = dest.rootFS.copyFileIn( source );
                    if ( !ok ) {
                        // FIXME: 这里辅助线程不能正确显示 UI 对话框
                        MessageBoxResult res = MessageBox.Show(
                            this,
                            "无法复制以下文件：\r\n  " + source.FullName + "\r\n\r\n要继续复制其余的文件吗？\r\n点击“取消”将停止复制。",
                            "操作失败",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Error
                        );
                        if ( res != MessageBoxResult.OK )
                            break;
                    }
                }
                ev.Cancel = _worker.CancellationPending;
            };

            ProcessDlg dlg = new ProcessDlg( fnWorking, this );
            bool? finished = dlg.ShowDialog();
            if ( finished.HasValue && (bool)finished ) {
            }
        }

        private void doDelete( FooViewModel model )
        {
            List<SimpleFileInfo> colls = TreeWalker.walk( model, this );
            if ( colls == null )
                return;

            // TODO: 删除空文件夹

            // 逐个删除文件
            DoWorkEventHandler fnWorking = delegate( object worker, DoWorkEventArgs ev ) {
                BackgroundWorker _worker = (BackgroundWorker)worker;
                foreach ( SimpleFileInfo source in colls ) {
                    if ( _worker.CancellationPending )
                        break;
                    _worker.ReportProgress( 0 );

                    source.rootFS.del( source.FullName );
                }
                ev.Cancel = _worker.CancellationPending;
            };

            ProcessDlg dlg = new ProcessDlg( fnWorking, this );
            bool? finished = dlg.ShowDialog();
            if ( finished.HasValue && (bool)finished ) {
            }
        }

        private void btnCopy_a_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAonly.DataContext ).First<FooViewModel>();
            doCopy( model, bFS );
        }

        private void btnDelete_a_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAonly.DataContext ).First<FooViewModel>();
            doDelete( model );
        }

        private void btnCopy_an_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAnewer.DataContext ).First<FooViewModel>();
            doCopy( model, bFS );
        }

        private void btnRCopy_an_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAnewer.DataContext ).First<FooViewModel>();
            doRCopy( model, bFS );
        }

        private void btnCopy_bn_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeBnewer.DataContext ).First<FooViewModel>();
            doCopy( model, aFS );
        }

        private void btnRCopy_bn_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeBnewer.DataContext ).First<FooViewModel>();
            doRCopy( model, aFS );
        }

        private void btnCopy_b_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeBonly.DataContext ).First<FooViewModel>();
            doCopy( model, aFS );
        }

        private void btnDelete_b_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeBonly.DataContext ).First<FooViewModel>();
            doDelete( model );
        }
    }

    // 遍历一个 model 以收集其全部选中的项目（只有文件，不包含目录）
    public class TreeWalker
    {
        static public List<SimpleFileInfo> walk( FooViewModel vroot, Window owner )
        {
            TreeWalker walker = new TreeWalker();
            walker._colls = new List<SimpleFileInfo>();
            DoWorkEventHandler fnWorking = delegate( object worker, DoWorkEventArgs ev ) {
                walker._worker = (BackgroundWorker)worker;
                try {
                    foreach ( FooViewModel sub in vroot.Children ) {
                        walker.walkModelRecur( sub, "/" );
                    }
                } catch ( Exception ex ) {
                    // 如果不是因为主动要求中止，则向外抛错
                    if ( !walker._worker.CancellationPending ) {
                        throw ex;
                    }
                }
                ev.Cancel = walker._worker.CancellationPending;
            };

            ProcessDlg dlg = new ProcessDlg( fnWorking, owner );
            bool? finished = dlg.ShowDialog();
            if ( finished.HasValue && (bool)finished ) {
                return walker._colls;
            }

            return null;
        }

        BackgroundWorker _worker;
        List<SimpleFileInfo> _colls;

        private TreeWalker()
        {
            _colls = new List<SimpleFileInfo>();
        }

        private void walkModelRecur( FooViewModel model, string path )
        {
            if ( _worker.CancellationPending ) {
                return;
            }
            _worker.ReportProgress( 0 );

            if ( model.IsChecked.HasValue && !(bool)model.IsChecked )
                return;
            if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FOLDER ) {
                if ( model.Fullpath.Length > 0 ) {
                    // 尚未展开的子目录，用 FS 继续递归遍历
                    walkFsRecur( path + model.Name + "/", model.Fso.rootFS );
                } else {
                    // 已经展开的子目录
                    foreach ( FooViewModel sub in model.Children ) {
                        walkModelRecur( sub, path + model.Name + "/" );
                    }
                }
            } else if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FILE ) {
                // 文件
                _colls.Add( (SimpleFileInfo)model.Fso );
            }
        }

        private void walkFsRecur( string path, ISimpleFS fs )
        {
            if ( _worker.CancellationPending ) {
                throw new Exception( "取消了操作" );
            }
            _worker.ReportProgress( 0 );

            foreach ( KeyValuePair<string, SimpleInfoBase> item in fs.getChildren( path ) ) {
                string name = item.Key;
                SimpleInfoBase value = item.Value;
                if ( value.GetType() == typeof( SimpleDirInfo ) ) {
                    // 子目录
                    walkFsRecur( path + name + "/", fs );
                } else {
                    // 文件
                    _colls.Add( (SimpleFileInfo)value );
                }
            }
        }
    }
}
