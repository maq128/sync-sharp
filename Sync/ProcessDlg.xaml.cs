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
using System.Threading;
using System.Diagnostics;
using System.Windows.Threading;
using System.ComponentModel;
using System.IO;

namespace Sync
{
    /// <summary>
    /// ProcessDlg.xaml 的交互逻辑
    /// </summary>
    public partial class ProcessDlg : Window
    {
        BackgroundWorker _worker;
        bool _isShown;

        public ProcessDlg( DoWorkEventHandler fnWorking, Window owner )
        {
            this.Owner = owner;
            this._isShown = false;
            InitializeComponent();

            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;

            // delegate 语法
            _worker.DoWork += fnWorking;

            // lambda 语法
            int per = (int)progressBar1.Minimum;
            _worker.ProgressChanged += ( sender, e ) => {
                // 这段代码将在主线程中执行
                progressBar1.Value = per++; // e.ProgressPercentage;
                if ( per > (int)progressBar1.Maximum ) {
                    per = (int)progressBar1.Minimum;
                }
            };

            // delegate 语法
            _worker.RunWorkerCompleted += delegate( Object sender, RunWorkerCompletedEventArgs e ) {
                // 这段代码将在主线程中执行

                if ( this._isShown ) {
                    // 这是任务正常执行完或者是点击“取消”的情形
                    this.DialogResult = e.Error == null && !e.Cancelled;
                    this.Close();
                } else {
                    // 这是直接关闭 ProcessDlg 窗口的情形
                }

                if ( e.Error != null && !e.Cancelled ) {
                    MessageBox.Show( this.Owner, e.Error.Message, "失败" );
                    return;
                }
            };
        }

        private void Window_Loaded( object sender, RoutedEventArgs e )
        {
            if ( _worker.IsBusy ) return;
            this._isShown = true;

            // 请求启动 worker
            _worker.RunWorkerAsync();
        }

        private void btn_Click( object sender, RoutedEventArgs e )
        {
            // 请求中止 worker
            _worker.CancelAsync();
        }

        private void Window_Closed( object sender, EventArgs e )
        {
            this._isShown = false;
            // 请求中止 worker
            _worker.CancelAsync();
        }
    }
}
