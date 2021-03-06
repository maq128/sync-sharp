﻿using System;
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

            // 点击按钮操作时，禁用所有按钮和树
            connectButtonsToTree( new Button[] { btnCopy_a, btnDelete_a }, treeAonly );
            connectButtonsToTree( new Button[] { btnCopy_an, btnRCopy_an }, treeAnewer );
            connectButtonsToTree( new Button[] { btnCopy_bn, btnRCopy_bn }, treeBnewer );
            connectButtonsToTree( new Button[] { btnCopy_b, btnDelete_b }, treeBonly );
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

        private void connectButtonsToTree( Button[] buttons, FolderTreeView tree )
        {
            foreach ( Button btn in buttons ) {
                btn.Click += ( object sender, RoutedEventArgs e ) => {
                    tree.IsEnabled = false;
                    foreach ( Button btn2 in buttons ) {
                        btn2.IsEnabled = false;
                    }
                };
            }
        }

        private void connectTreeToButtons( FolderTreeView tree, Button[] buttons )
        {
            // 按钮和树的状态初始化
            foreach ( Button btn in buttons ) {
                btn.IsEnabled = false;
            }
            FooViewModel model = ( (List<FooViewModel>)tree.DataContext ).First<FooViewModel>();
            model.IsExpanded = true;
            tree.IsEnabled = model.Children.Count > 0;

            // 树上有选择时，按钮状态联动
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

                tabControl1.SelectedIndex = 2;

                // 在树上做选择时，设置按钮状态
                connectTreeToButtons( treeAonly, new Button[] { btnCopy_a, btnDelete_a } );
                connectTreeToButtons( treeAnewer, new Button[] { btnCopy_an, btnRCopy_an } );
                connectTreeToButtons( treeAB, new Button[] {} );
                connectTreeToButtons( treeBnewer, new Button[] { btnCopy_bn, btnRCopy_bn } );
                connectTreeToButtons( treeBonly, new Button[] { btnCopy_b, btnDelete_b } );
            }
        }

        private void compareTree( SimpleDirInfo dirA, SimpleDirInfo dirB, FooViewModel parentAonly, FooViewModel parentAnewer, FooViewModel parentAB, FooViewModel parentBnewer, FooViewModel parentBonly )
        {
            if ( _worker.CancellationPending ) {
                throw new Exception( "取消了操作" );
            }
            ProcessDlg.reportMain( 0, String.Format("扫描文件夹: {0}", ++this._scanCount) );

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

                a.opposite = b;
                b.opposite = a;

                if ( a.LastWriteTime > b.LastWriteTime.AddSeconds( 5 ) ) {
                    // A > B
                    parentAnewer.Children.Add( parentAnewer.CreateFileItem( a ) );
                } else if ( a.LastWriteTime < b.LastWriteTime.AddSeconds( -5 ) ) {
                    // A < B
                    parentBnewer.Children.Add( parentBnewer.CreateFileItem( b ) );
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
            ModelWalker walker = new ModelWalker();
            walker.PassFile += ( SimpleFileInfo file ) => {
                // 这段代码将在辅助线程中执行
                ProcessDlg.reportMain( 0, "复制: " + file.FullName );
                bool ok = to.copyFileIn( file );
            };
            walker.walk( model, this );
        }

        private void doRCopy( FooViewModel model, ISimpleFS from )
        {
            ModelWalker walker = new ModelWalker();
            walker.PassFile += ( SimpleFileInfo file ) => {
                // 这段代码将在辅助线程中执行
                ProcessDlg.reportMain( 0, "反向复制: " + file.FullName );
                bool ok = file.rootFS.copyFileIn( file.opposite );
            };
            walker.walk( model, this );
        }

        private void doDelete( FooViewModel model )
        {
            ModelWalker walker = new ModelWalker();
            walker.PassFile += ( SimpleFileInfo file ) => {
                // 这段代码将在辅助线程中执行
                ProcessDlg.reportMain( 0, "删除: " + file.FullName );
                bool ok = file.rootFS.delFile( file.FullName );
            };
            walker.LeaveDir += ( SimpleDirInfo dir ) => {
                // 这段代码将在辅助线程中执行
                ProcessDlg.reportMain( 0, "删除: " + dir.FullName );
                bool ok = dir.rootFS.delDir( dir.FullName );
            };
            walker.walk( model, this );
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

    // 遍历一个 model 中选中的项目
    public class ModelWalker
    {
        BackgroundWorker _worker;

        public ModelWalker()
        {
        }

        public delegate void EnterDirEventHandler( SimpleDirInfo dir );
        public delegate void LeaveDirEventHandler( SimpleDirInfo dir );
        public delegate void PassFileEventHandler( SimpleFileInfo file );

        public event EnterDirEventHandler EnterDir;
        public event LeaveDirEventHandler LeaveDir;
        public event PassFileEventHandler PassFile;

        public void walk( FooViewModel model, Window owner )
        {
            DoWorkEventHandler fnWorking = delegate( object obj, DoWorkEventArgs ev ) {
                _worker = (BackgroundWorker)obj;
                try {
                    walkModel( model );
                } catch ( Exception ex ) {
                    // 如果不是因为主动要求中止，则向外抛错
                    if ( !_worker.CancellationPending ) {
                        throw ex;
                    }
                }
                ev.Cancel = _worker.CancellationPending;
            };

            ProcessDlg dlg = new ProcessDlg( fnWorking, owner );
            dlg.ShowDialog();
        }

        public void walkModel( FooViewModel model )
        {
            if ( model.IsChecked.HasValue && !(bool)model.IsChecked )
                return;

            if ( _worker.CancellationPending ) {
                throw new Exception( "取消了操作" );
            }
            _worker.ReportProgress( 0 );

            if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_VIRTUALROOT ) {
                foreach ( FooViewModel sub in model.Children ) {
                    walkModel( sub );
                }
            } else if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FOLDER ) {
                if ( model.Fullpath.Length > 0 ) {
                    // 尚未展开的子目录，用 FS 继续递归遍历
                    walkFs( (SimpleDirInfo)model.Fso );
                } else {
                    // 已经展开的子目录
                    if ( EnterDir != null ) {
                        EnterDir( (SimpleDirInfo)model.Fso );
                    }

                    foreach ( FooViewModel sub in model.Children ) {
                        walkModel( sub );
                    }

                    if ( LeaveDir != null ) {
                        LeaveDir( (SimpleDirInfo)model.Fso );
                    }
                }
            } else if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FILE ) {
                // 文件
                if ( PassFile != null ) {
                    PassFile( (SimpleFileInfo)model.Fso );
                }
            }
        }

        public void walkFs( SimpleDirInfo dir )
        {
            if ( _worker.CancellationPending ) {
                throw new Exception( "取消了操作" );
            }
            _worker.ReportProgress( 0 );

            if ( EnterDir != null ) {
                EnterDir( dir );
            }

            foreach ( KeyValuePair<string, SimpleInfoBase> item in dir.getChildren() ) {
                string name = item.Key;
                SimpleInfoBase value = item.Value;
                if ( value.GetType() == typeof( SimpleDirInfo ) ) {
                    // 子目录
                    SimpleDirInfo subDir = (SimpleDirInfo)value;
                    walkFs( subDir );
                } else if ( value.GetType() == typeof( SimpleFileInfo ) ) {
                    // 文件
                    if ( _worker.CancellationPending ) {
                        throw new Exception( "取消了操作" );
                    }
                    _worker.ReportProgress( 0 );

                    if ( PassFile != null ) {
                        SimpleFileInfo file = (SimpleFileInfo)value;
                        PassFile( file );
                    }
                }
            }

            if ( LeaveDir != null ) {
                LeaveDir( dir );
            }
        }
    }
}
