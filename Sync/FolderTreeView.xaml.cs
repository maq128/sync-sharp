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

namespace Sync
{
    /// <summary>
    /// FolderTreeView.xaml 的交互逻辑
    /// </summary>
    public partial class FolderTreeView : UserControl
    {
        public FolderTreeView()
        {
            InitializeComponent();
        }

        private void TreeViewItem_MouseRightButtonDown( object sender, MouseButtonEventArgs e )
        {
            // 鼠标右键点击时选中指定节点，如同左键点击一样
            TreeViewItem item = sender as TreeViewItem;
            if ( item != null ) {
                item.Focus();
                e.Handled = true;
            }
        }

        private void TreeView_ContextMenuOpening( object sender, ContextMenuEventArgs e )
        {
            TreeView tv = sender as TreeView;
            if ( tv == null ) {
                e.Handled = true;
                return;
            }

            FooViewModel model = tv.SelectedItem as FooViewModel;
            if ( model == null ) {
                e.Handled = true;
                return;
            }

            if ( model.Fso.rootFS == null ) {
                e.Handled = true;
                return;
            }

            tv.ContextMenu.Items.Clear();
            if ( model.Fso.rootFS.GetType() == typeof( WebdavFS ) ) {
                if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FILE || model.Type == FooViewModel.ItemType.ITEM_TYPE_FOLDER ) {
                    MenuItem item = new MenuItem();
                    item.Header = "移动到……";
                    item.Click += new RoutedEventHandler( OnWebdavMove );
                    tv.ContextMenu.Items.Add( item );
                }
            } else if ( model.Fso.rootFS.GetType() == typeof( LocalFS ) ) {
                if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FILE ) {
                    MenuItem item = new MenuItem();
                    item.Header = "打开文件";
                    item.Click += new RoutedEventHandler( OnLocalOpenFile );
                    tv.ContextMenu.Items.Add( item );
                } else if ( model.Type == FooViewModel.ItemType.ITEM_TYPE_FOLDER ) {
                    MenuItem item = new MenuItem();
                    item.Header = "打开文件夹";
                    item.Click += new RoutedEventHandler( OnLocalOpenFolder );
                    tv.ContextMenu.Items.Add( item );
                } else {
                    e.Handled = true;
                }
            } else {
                e.Handled = true;
            }
        }

        private void OnWebdavMove( object sender, RoutedEventArgs e )
        {
        }

        private void OnLocalOpenFile( object sender, RoutedEventArgs e )
        {
        }

        private void OnLocalOpenFolder( object sender, RoutedEventArgs e )
        {
        }
    }
}
