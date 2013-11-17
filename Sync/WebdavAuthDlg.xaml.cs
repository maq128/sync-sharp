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
using System.Windows.Shapes;

namespace Sync
{
    /// <summary>
    /// WebdavAuthDlg.xaml 的交互逻辑
    /// </summary>
    public partial class WebdavAuthDlg : Window
    {
        public WebdavAuthDlg()
        {
            InitializeComponent();
        }

        private void btnOk_Click( object sender, RoutedEventArgs e )
        {
            this.DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click( object sender, RoutedEventArgs e )
        {
            this.Close();
        }
    }
}
