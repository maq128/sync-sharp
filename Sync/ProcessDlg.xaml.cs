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
        // 这两个接口函数只能在 _worker 的辅助线程中调用
        static public void reportMain( int percentProgress, object userState )
        {
            if ( _worker != null )
                _worker.ReportProgress( percentProgress, userState );
        }
        static public void reportFile( int percentProgress )
        {
            if ( _worker != null )
                _worker.ReportProgress( percentProgress );
        }

        static private BackgroundWorker _worker;
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
            int per = (int)progressBarMain.Minimum;
            _worker.ProgressChanged += ( sender, e ) => {
                // 这段代码将在主线程中执行

                if ( e.UserState != null ) {
                    // 此事件来自于 reportMain()
                    int value = per + 1;
                    if ( e.ProgressPercentage > 0 ) {
                        value = (int)( progressBarMain.Minimum + ( progressBarMain.Maximum - progressBarMain.Minimum ) * e.ProgressPercentage / 100 );
                    }
                    if ( value > progressBarMain.Maximum )
                        value = (int)progressBarMain.Minimum;
                    progressBarMain.Value = value;
                    per = (int)progressBarMain.Value;
                    this.info.Text = e.UserState.ToString();
                    this.progressBarFile.Visibility = Visibility.Hidden;
                } else {
                    // 此事件来自于 reportFile()
                    progressBarFile.Value = (int)( progressBarFile.Minimum + ( progressBarFile.Maximum - progressBarFile.Minimum ) * e.ProgressPercentage / 100 );
                    progressBarFile.Visibility = Visibility.Visible;
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
            _worker = null;
        }
    }
}
