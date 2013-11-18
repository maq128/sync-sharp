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
        bool _cancel;
        int _progress;

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
                    } catch ( WebDav.Client.Exceptions.UnauthorizedException ) {
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

        private void btnCompare_Click( object sender, RoutedEventArgs e )
        {
            // TODO: 所有按钮状态复位

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

                treeAonly.DataContext = new List<FooViewModel> { rootAonly };
                treeAnewer.DataContext = new List<FooViewModel> { rootAnewer };
                treeAB.DataContext = new List<FooViewModel> { rootAB };
                treeBnewer.DataContext = new List<FooViewModel> { rootBnewer };
                treeBonly.DataContext = new List<FooViewModel> { rootBonly };

                tabControl1.SelectedIndex = 2;
            };

            ProcessDlg dlg = new ProcessDlg( fnWorking, fnFinish );
            dlg.Owner = this;
            dlg.ShowDialog();

            // 根据节点的选择情况设置操作按钮的使能状态
            connectModelToButtons( rootAonly, new Button[] { btnCopy_a, btnDelete_a } );
            connectModelToButtons( rootAnewer, new Button[] { btnCopy_an, btnRCopy_an } );
            connectModelToButtons( rootAB, new Button[] { btnDelete_ab } );
            connectModelToButtons( rootBnewer, new Button[] { btnCopy_bn, btnRCopy_bn } );
            connectModelToButtons( rootBonly, new Button[] { btnCopy_b, btnDelete_b } );
        }

        private void connectModelToButtons( FooViewModel model, Button[] buttons )
        {
            PropertyChangedEventHandler h = delegate( object m, PropertyChangedEventArgs ev ) {
                if ( ev.PropertyName != "IsChecked" )
                    return;
                bool en = !model.IsChecked.HasValue || (bool)model.IsChecked;
                foreach ( Button btn in buttons ) {
                    btn.IsEnabled = en;
                }
            };
            model.PropertyChanged += h;
        }

        private void compareTree( SimpleDirInfo dirA, SimpleDirInfo dirB, FooViewModel parentAonly, FooViewModel parentAnewer, FooViewModel parentAB, FooViewModel parentBnewer, FooViewModel parentBonly )
        {
            if ( _worker.CancellationPending || _cancel ) {
                _cancel = true;
                return;
            }

            _worker.ReportProgress( _progress++ );
            if ( _progress > 100 ) {
                _progress = 0;
            }

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
            foreach ( KeyValuePair<string, SimpleDirInfo> item in subDirsA ) {
                SimpleDirInfo a = item.Value;
                parentAonly.Children.Add( parentAonly.CreateLazyFolderItem( a.Name, a.FullName ) );
            }

            // 对仅在 B 中存在的子目录进行遍历
            foreach ( KeyValuePair<string, SimpleDirInfo> item in subDirsB ) {
                SimpleDirInfo b = item.Value;
                parentBonly.Children.Add( parentBonly.CreateLazyFolderItem( b.Name, b.FullName ) );
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
            foreach ( KeyValuePair<string, SimpleFileInfo> item in subFilesA ) {
                SimpleFileInfo a = item.Value;
                parentAonly.Children.Add( parentAonly.CreateFileItem( a.Name ) );
            }

            // 对仅在 B 中存在的文件进行遍历
            foreach ( KeyValuePair<string, SimpleFileInfo> item in subFilesB ) {
                SimpleFileInfo b = item.Value;
                parentBonly.Children.Add( parentBonly.CreateFileItem( b.Name ) );
            }
        }

        // 遍历一个 model 以收集其全部选中的项目（子目录、文件）
        private void walkModelTree( FooViewModel model, List<string> colls, string path )
        {
            if ( model.IsChecked.HasValue && !(bool)model.IsChecked )
                return;
            if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FOLDER ) {
                if ( model.Fullpath.Length > 0 ) {
                    // 尚未展开的子目录（以 / 结尾代表其下游全部内容）
                    colls.Add( path + model.Name + "/" );
                } else {
                    // 已经展开的子目录
                    foreach ( FooViewModel sub in model.Children ) {
                        walkModelTree( sub, colls, path + model.Name + "/" );
                    }
                }
            } else if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FILE ) {
                // 文件
                colls.Add( path + model.Name );
            }
        }

        private void doCopy( FooViewModel model, ISimpleFS from, ISimpleFS to )
        {
            List<string> colls = new List<string>();
            foreach ( FooViewModel sub in model.Children ) {
                walkModelTree( sub, colls, "/" );
            }
            System.Console.WriteLine( "copy from: " + from.ToString() );
            System.Console.WriteLine( "copy to  : " + to.ToString() );
            foreach ( string path in colls ) {
                System.Console.WriteLine( "    " + path );
            }
        }

        private void doDelete( FooViewModel model, ISimpleFS from )
        {
            List<string> colls = new List<string>();
            foreach ( FooViewModel sub in model.Children ) {
                walkModelTree( sub, colls, "/" );
            }
            System.Console.WriteLine( "delete from: " + from.ToString() );
            foreach ( string path in colls ) {
                System.Console.WriteLine( "    " + path );
            }
        }

        private void btnCopy_a_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAonly.DataContext ).First<FooViewModel>();
            doCopy( model, aFS, bFS );
        }

        private void btnDelete_a_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAonly.DataContext ).First<FooViewModel>();
            doDelete( model, aFS );
        }

        private void btnCopy_an_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAnewer.DataContext ).First<FooViewModel>();
            doCopy( model, aFS, bFS );
        }

        private void btnRCopy_an_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAnewer.DataContext ).First<FooViewModel>();
            doCopy( model, bFS, aFS );
        }

        private void btnDelete_ab_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeAB.DataContext ).First<FooViewModel>();
            doDelete( model, aFS );
            doDelete( model, bFS );
        }

        private void btnCopy_bn_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeBnewer.DataContext ).First<FooViewModel>();
            doCopy( model, bFS, aFS );
        }

        private void btnRCopy_bn_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeBnewer.DataContext ).First<FooViewModel>();
            doCopy( model, aFS, bFS );
        }

        private void btnCopy_b_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeBonly.DataContext ).First<FooViewModel>();
            doCopy( model, bFS, aFS );
        }

        private void btnDelete_b_Click( object sender, RoutedEventArgs e )
        {
            FooViewModel model = ( (List<FooViewModel>)treeBonly.DataContext ).First<FooViewModel>();
            doDelete( model, bFS );
        }
    }
}
