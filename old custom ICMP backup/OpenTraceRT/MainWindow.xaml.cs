using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using ProtocolHeaderDefinition;

namespace OpenTraceRT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private bool started = false;

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token;

        private string ip;
        private volatile bool IsTraceComplete = false;
        private bool linesSkipped = false;
        private bool IsLineSkipOver = false;

        DataList dataList = new DataList();
        DataTable dataTbl = new DataTable();
        Grapher _grapher;
        private int listPosition = 0;

        //private LatencyTester latencyTester = new LatencyTester();
        private List<DataItem> tempData = new List<DataItem>();
        private List<String> routeList = new List<string>();

        public MainWindow() {

            InitializeComponent();

            App.Current.Exit += OnExit;

            this.SizeChanged += graphHeight;

            dataTbl.Columns.Add("Jump #", typeof(String));
            dataTbl.Columns.Add("HostName");
            dataTbl.Columns.Add("Latency");
            dataTbl.Columns.Add("Packet Loss");
            dataTbl.Columns.Add("Graph", typeof(Canvas));


            //Testing Values!
            //3 Jumps
            //ipInput.Text = "194.22.54.181";

            //13 Jumps
            //ipInput.Text = "195.82.50.45";

            ipInput.Text = "8.8.8.8";
        }

        private void SetCanvas(object sender, RoutedEventArgs e) {

            _grapher = new Grapher(e.Source as Canvas);
            _grapher.SetHeight(graphGrid.ActualHeight);
        }

        private void graphHeight(object sender, SizeChangedEventArgs e) {

            if (_grapher != null) {

                _grapher.SetHeight(graphGrid.ActualHeight);
            }            
        }

        private void UpdateUI(object sender, PropertyChangedEventArgs e) {

            tempData.Clear();

            List<String> latencyList = new List<String>();

            Dispatcher.Invoke(() => traceData.ItemsSource = null);
            Dispatcher.Invoke(() => traceData.Items.Clear());
            dataTbl.Clear();

            try {

                int index = dataList[dataList.Count - 1].Count;

                for (int i = 0; i < dataList[index].Count; i++) {

                    latencyList.Add(dataList[index][i].latency);
                    tempData.Add(new DataItem { latency = dataList[index][i].latency, hostname = dataList[index][i].hostname });
                }
            }
            catch (Exception) {

                //throw;
            }
            /*
            foreach (var item in latencyList) {
                Console.WriteLine(item);
            }
            */
            for (int i = 0; i < tempData.Count; i++) {

                    addRow( new DataItem { jumps = (i + 1).ToString(), latency = tempData[i].latency, hostname = tempData[i].hostname } );
            }

            Dispatcher.Invoke(() => traceData.ItemsSource = dataTbl.DefaultView );
            Dispatcher.Invoke(() => traceData.DataContext = dataTbl.DefaultView );
            Dispatcher.Invoke(() => _grapher.BuildGraph(latencyList.ToList(), ip) );

            tempData.Clear();
        }

        private void addRow(DataItem data) {

            DataRow newRow = dataTbl.NewRow();

            newRow[0] = data.jumps;
            newRow[1] = data.hostname;
            newRow[2] = data.latency;

            dataTbl.Rows.Add(newRow);            
        }

        private void ClearGrid() {

            //Console.WriteLine("Clearing!");
            traceData.Items.Clear();
            
        }

        private void RoutingChanged() {

            Console.WriteLine("Route has changed, information no longer correct!");
            //throw new NotImplementedException();
        }


        private void OnExit(object sender, EventArgs e) {

            source.Cancel();
        }

        private void startBtn_Click(object sender, RoutedEventArgs e) {
            //TODO: Add regex to check if input matches ip or hostname
            
            if (PatternChecker.IsValidIP(ipInput.Text)) {

                if (!started) {

                    SetStart(true);
                    startStopBtn.Content = "Stop";
                }
                else {

                    source.Cancel();
                    source = new CancellationTokenSource();
                    startStopBtn.Content = "Start";
                    SetStart(false);
                    return;
                }

                string interval = GetInterval(pollInterval.Text);

                ip = ipInput.Text;
                token = source.Token;

                Thread pollThread = new Thread(() => Polling(interval, token));
                pollThread.Start();

                Thread isCompleteThread = new Thread(() => TraceComplete((Convert.ToInt32(interval) * 1000), token) );
                isCompleteThread.Start();

            }
            else {

                MessageBox.Show("Not a valid IP!", "Error");
            }
        }

        private void TraceComplete(int interval, CancellationToken token) {

            while (!IsTraceComplete && !token.IsCancellationRequested) {

                Thread.Sleep(1000);

            }
            //while (!token.IsCancellationRequested) {

                Dispatcher.Invoke(() => StartRoutePing());
                //Thread.Sleep(interval);
            //}            
            return;
        }

        private void StartRoutePing() {

            List<Thread> threadList = new List<Thread>();
            for (int i = 0; i < routeList.Count; i++) {

                string tempHost = routeList[i];
                LatencyTester tempLT = new LatencyTester();

                Thread pingThread = new Thread(() => {
                    DataItem tempItem = new DataItem();
                    LatencyTester latencyTester = new LatencyTester();
                    tempItem = latencyTester.MakeRequest(tempHost, 4, 32, 128);
                    tempData.Add(tempItem);
                } );

                threadList.Add(pingThread);
                pingThread.Start();                
            }

            Thread checkThread = new Thread(() => CheckThreadsFinished(threadList));
            checkThread.Start();
            return;
        }

        private void SortList() {

            //Console.WriteLine("\n\nSorting\n");

            try {
                for (int i = 0; i < tempData.Count; i++) {
                    //if not right place
                    if (!tempData[i].hostname.Equals(routeList[i])) {

                        for (int j = 0; j < routeList.Count; j++) {

                            if (tempData[i].hostname.Equals(routeList[j])) {

                                DataItem tempItem = (DataItem)tempData[i].Clone();
                                tempData[i] = tempData[j];
                                tempData[j] = (DataItem)tempItem.Clone();
                                i = 0;
                                break;
                            }
                        }
                    }
                }
            }
            catch (ArgumentOutOfRangeException e) {
                Console.WriteLine(e.Message);
            }

            //Console.WriteLine("\nSorting finished\n");
            return;
        }

        private void CheckThreadsFinished(List<Thread> threadList) {

            bool finished = false;
            Thread.Sleep(3500);

            while (!finished) {

                Thread.Sleep(500);
                for (int i = 0; i < threadList.Count; i++) {

                    if (threadList[i].IsAlive == true) {

                        break;
                    }
                    if (i == threadList.Count - 1) {

                        finished = true;
                    }
                }                
            }

            SortList();
            dataList.Add(tempData.ToList());            
            UpdateUI(this, new PropertyChangedEventArgs("Data"));
            return;
        }               

        private string GetInterval(string pollInterval) {

            string interval = ""; 
            for (int i = 0; i < 3; i++) {

                if (pollInterval[i] == ' ') {

                    break;
                }
                else {

                    interval += pollInterval[i];
                }
            }
            return interval;
        }

        private void SetStart(bool set) {
            started = set;
        }

        private void Polling(string interval, CancellationToken token) {
            GetTrace(token);
        }

        private void GetTrace(CancellationToken token) {

            Process cmd = SetupTraceRT();
            cmd.Start();
            cmd.BeginOutputReadLine();
            cmd.StandardInput.WriteLine("tracert " + ip);

            if (token.IsCancellationRequested == true) {

                cmd.Dispose();
            }

            cmd.WaitForExit();
        }

        private Process SetupTraceRT() {

            ProcessStartInfo cmdStartInfo = new ProcessStartInfo("cmd.exe");
            cmdStartInfo.CreateNoWindow = true;
            cmdStartInfo.RedirectStandardInput = true;
            cmdStartInfo.RedirectStandardOutput = true;
            cmdStartInfo.UseShellExecute = false;
            cmdStartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            Process cmd = new Process();
            cmd.StartInfo = cmdStartInfo;
            cmd.OutputDataReceived += cmd_DataReceieved;
            cmd.EnableRaisingEvents = true;

            return cmd;
        }

        //Sorts through tracerts output to find lines with IP and the Trace complete message
        private void cmd_DataReceieved(object sender, DataReceivedEventArgs e) {

            if (token.IsCancellationRequested == true) {

                return;
            }
            if (e.Data != null && e.Data.Length > 2) {

                if (e.Data[0] == ' ' || e.Data[4] == 'e') {

                    ParseData(e.Data, token);
                }

            }

        }
        

        private void ParseData(string data, CancellationToken token) {

            StringBuilder sb = new StringBuilder();
            StringReader sr = new StringReader(data);
            sb.Append(sr.ReadToEnd());
            //Console.WriteLine(sb.ToString());


            if (PatternChecker.IsTraceComplete(sb.ToString())) {

                //Console.WriteLine("Trace Complete!");
                Dispatcher.Invoke(() => ClearGrid() );
                IsTraceComplete = true;
                return;
            }

            List<String> tempList = new List<string>();
            int dataValue = 0;

            //Captures IPs and excludes latency and non-numerical IPs
            for (int i = 0; i < sb.Length; i++) {

                if (token.IsCancellationRequested == true) {

                    return;
                }
                if (sb[i] != ' ' && sb[i] != '<' && sb[i] != '[') {

                    StringBuilder sb2 = new StringBuilder();
                    while (sb[i] != '*') {

                        //Console.Write(dataValue);
                        if (token.IsCancellationRequested == true) {

                            return;
                        }
                        if (sb.Length > i + 1) {

                            if (sb[i + 1] == ' ') {

                                if (dataValue == 7 || dataValue == 8) {

                                    if (sb[i] != ']') {

                                        sb2.Append(sb[i].ToString());
                                    }
                                    if (PatternChecker.IsValidIP(sb2.ToString())) {

                                        //Console.WriteLine(sb2.ToString() + " added to list");
                                        tempList.Add(sb2.ToString());
                                    }
                                }
                                dataValue += 1;
                                break;
                            }

                            if (dataValue == 7 || dataValue == 8) {

                                sb2.Append(sb[i].ToString());
                            }
                            i++;
                        }
                        
                    }
                    //TODO: Handle timeout
                    if (sb[i] == '*') {

                        if (dataValue == 1 || dataValue == 3) {

                            dataValue += 2;
                        }
                    }
                }
            }
            /*
            foreach (string item in tempList) {

                Console.WriteLine(item + " ");
            }
            Console.WriteLine();
            */

            if (tempList.Count > 0) {

                string tempHost = tempList[0];
                routeList.Add(tempHost);
            }
        }

        private void exitBtn_Click(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        private void saveBtn_Click(object sender, RoutedEventArgs e) {
            throw new NotImplementedException();
        }
    }
}
