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
    public delegate bool ProcessDlgWorkingHandler( BackgroundWorker worker );
    public delegate void ProcessDlgFinishHandler();

    /// <summary>
    /// ProcessDlg.xaml 的交互逻辑
    /// </summary>
    public partial class ProcessDlg : Window
    {
        BackgroundWorker _worker;

        public ProcessDlg( ProcessDlgWorkingHandler fnWorking, ProcessDlgFinishHandler fnFinish )
        {
            InitializeComponent();

            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;

            // delegate 语法
            _worker.DoWork += delegate( object sender, DoWorkEventArgs e ) {
                // 这段代码将在辅助线程中执行
                if ( fnWorking( (BackgroundWorker)sender ) ) {
                    e.Cancel = true;
                }
            };

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
                this.Close();

                if ( e.Error != null ) {
                    //Debug.WriteLine( "BackgroundWorker: " + e.Error.ToString() );
                    MessageBox.Show( e.Error.Message, "失败" );
                    return;
                }

                if ( e.Cancelled ) {
                    //Debug.WriteLine( "BackgroundWorker: canceled." );
                    return;
                }

                fnFinish();
            };

            _worker.RunWorkerAsync();
        }

        private void btn_Click( object sender, RoutedEventArgs e )
        {
            _worker.CancelAsync();
        }
    }
}
