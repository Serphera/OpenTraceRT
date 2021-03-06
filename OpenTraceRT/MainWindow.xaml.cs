﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading;
using System.Diagnostics;
using System.Security;
using System.Security.Permissions;
using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;

namespace OpenTraceRT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private bool started = false;

#if net20
        private bool cancel = false;
#endif

        CancellationTokenSource source;
        CancellationToken token;


        private string ip;
        private List<Thread> Threads = new List<Thread>();
        private bool IsWoundDown = true;
        private volatile bool IsTraceComplete = false;

        List<List<DataItem>> dataList = new List<List<DataItem>>();
        DataTable dataTbl = new DataTable();
        Grapher _grapher;


        bool disableIntervalPolling = true;


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

            DebugSetup();

        }

        [Conditional("DEBUG")]
        private void DebugSetup() {

            AddPollCheckbox();

            //Testing Values!
            //3 Jumps
            //ipInput.Text = "194.22.54.181";

            //13 Jumps
            //ipInput.Text = "195.82.50.45";

            ipInput.Text = "8.8.8.8";
        }


        [Conditional("DEBUG")]
        private void AddPollCheckbox() {
            CheckBox intervalPollBox = new CheckBox();
            intervalPollBox.Content = "Enable Interval Polling";
            intervalPollBox.Margin = new Thickness(5, 5, 0, 2);

            intervalPollBox.Click += intervalPollingChecked;


            menuPanel.Children.Add(intervalPollBox);
        }

        private void intervalPollingChecked(object sender, RoutedEventArgs e) {

            if (((CheckBox)e.OriginalSource).IsChecked == true) {

                disableIntervalPolling = false;
            }
            else {

                disableIntervalPolling = true;
            }
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


        private void ClearGrid() {

            traceData.Items.Clear();            
        }


        private void OnExit(object sender, EventArgs e) {
#if !net20
            source.Cancel();
#elif net20
            cancel = true;
#endif
        }




        private void GenerateSource() {

#if Release
            source.Cancel();            
            source = new CancellationTokenSource();
#elif net20
            cancel = true;
#endif
        }

        [Conditional("Release"), Conditional("DEBUG")]
        private void GenerateToken() {

            token = source.Token;
        }


        private bool CheckIfCancel() {
            bool isTrue = false;

#if Release || DEBUG
            isTrue = (token.IsCancellationRequested) ? true : false;
#elif net20
            isTrue = (cancel) ? true : false;
#endif

            return isTrue;
        }


        private void startBtn_Click(object sender, RoutedEventArgs e) {

            Console.WriteLine("1\n");
            if (PatternChecker.IsValidIP(ipInput.Text)) {
                
                if (!started && IsWoundDown) {

                    SetStart(true);
                    startStopBtn.Content = "Stop";                    
                }
                else {
                    
                    GenerateSource();
                    startStopBtn.Content = "Start";
                    SetStart(false);
                    return;
                }

                if (IsWoundDown) {
                    
                    ip = ipInput.Text;
                    GenerateToken();
                    string interval = GetInterval(pollInterval.Text);

                    Thread startThread = new Thread(() => StartPoll(interval));
                    startThread.Start();
                    IsWoundDown = false;
                }

            }
            else {

                MessageBox.Show("Not a valid IP!", "Error");
            }

        }

        private void StartPoll(string interval) {

            Thread checkActiveThreads = new Thread(() => CheckThreadsClosed());
            checkActiveThreads.Start();

            Thread pollThread = new Thread(() => Polling(interval));
            Threads.Add(pollThread);
            pollThread.Start();

            Thread isCompleteThread = new Thread(() => TraceComplete((Convert.ToInt32(interval) * 1000)));
            Threads.Add(isCompleteThread);
            isCompleteThread.Start();

            return;
        }


        private bool CheckThreadsClosed() {

            while (CheckIfCancel()) {
                Thread.Sleep(500);
            }

            for (int i = 0; i < Threads.Count; i++) {

                if (Threads[i].IsAlive) {

                    i = 0;
                    Thread.Sleep(100);
                }

            }

            IsWoundDown = true;
            Threads.Clear();
            return true;
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


        private void Polling(string interval) {
            GetTrace();
        }


        private void GetTrace() {
            
            Process cmd = SetupTraceRT();
            cmd.Start();
            cmd.BeginOutputReadLine();
            
            //cmd.StandardInput.WriteLine("tracert " + ip);

            if (CheckIfCancel()) {

                cmd.Dispose();
            }

            cmd.WaitForExit();
        }


        private Process SetupTraceRT() {

            ProcessStartInfo cmdStartInfo = new ProcessStartInfo("cmd.exe");
            cmdStartInfo.CreateNoWindow = true;
            cmdStartInfo.Arguments = "/C \" tracert " + ip;

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

            if (CheckIfCancel()) {

                return;
            }
            if (e.Data != null && e.Data.Length > 2) {

                if (e.Data[0] == ' ' || e.Data[4] == 'e') {

                    ParseData(e.Data);
                }

            }

        }


        private void ParseData(string data) {

            StringBuilder sb = new StringBuilder();
            StringReader sr = new StringReader(data);
            sb.Append(sr.ReadLine());

            if (PatternChecker.IsTraceComplete(sb.ToString()) && CheckIfCancel()) {

                Console.WriteLine("trace complete");
                Dispatcher.Invoke(() => ClearGrid());
                IsTraceComplete = true;
                return;
            }

            List<String> tempList = new List<string>();

            MatchCollection matches = Regex.Matches(sb.ToString(), @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");

            if (matches.Count > 0) {
                foreach (var item in matches) {

                    tempList.Add(item.ToString());
                }
            }
            


            if (tempList.Count > 0) {

                string tempHost = tempList[0];
                routeList.Add(tempHost);
            }
        }


        private void TraceComplete(int interval) {

            Thread.Sleep(5000);

            while (!IsTraceComplete && CheckIfCancel()) {

                Thread.Sleep(1000);

            }
#if DEBUG
            if (!disableIntervalPolling) {
#endif
                while (!CheckIfCancel()) {

                    StartRoutePing();
                    Thread.Sleep(interval);
                }
#if DEBUG
            }

            else if (!CheckIfCancel()) {
                StartRoutePing(token);
            }
#endif
            return;
        }


        private void StartRoutePing() {

            List<Thread> threadList = new List<Thread>();            
            
            var l = new object();

            for (int i = 0; i < routeList.Count; i++) {

                if (CheckIfCancel()) {
                    return;
                }

                string tempHost = routeList[i];
                LatencyTester tempLT = new LatencyTester();

                Thread pingThread = new Thread(() => {

                    DataItem tempItem = new DataItem();
                    LatencyTester latencyTester = new LatencyTester();
                    tempItem = latencyTester.MakeRequest(tempHost, token);

                    lock (l) {
                        tempData.Add(tempItem);
                    }
                } );

                threadList.Add(pingThread);
                pingThread.Start();
            }

            
            Thread checkThread = new Thread(() => CheckThreadsFinished(threadList));
            checkThread.Start();            

            return;
        }


        private void CheckThreadsFinished(List<Thread> threadList) {
            
                if (CheckIfCancel()) {

                    threadList.Clear();
                    return;
                }

                for (int i = 0; i < threadList.Count; i++) {

                    threadList[i].Join();
                }

#if DEBUG
            Console.WriteLine("\nAll threads finished");
#endif
            Console.WriteLine("\nAll threads finished");
            SortList();
            dataList.Add(tempData.ToList());
            UpdateUI(this, new PropertyChangedEventArgs("Data"));

            return;
        }


        private void SortList() {

            try {
                for (int i = 0; i < tempData.Count; i++) {

                    if (CheckIfCancel()) {

                        return;
                    }

                    //if not right place
                    if (!tempData[i].hostname.Equals(routeList[i])) {

                        for (int j = 0; j < routeList.Count; j++) {

                            if (CheckIfCancel()) {
                                return;
                            }

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
#if DEBUG
                Console.WriteLine(e.Message);
#endif
                throw;
            }
            catch (NullReferenceException e) {
#if DEBUG
                Console.WriteLine(e.Message);
#endif
                throw;
            }

            return;
        }


        private void UpdateUI(object sender, PropertyChangedEventArgs e) {

            tempData.Clear();

            List<String> latencyList = new List<String>();
            List<decimal> packetLossList = new List<decimal>();

            Dispatcher.Invoke(() => traceData.ItemsSource = null);
            
            dataTbl.Clear();

            for (int i = 0; i < dataList[dataList.Count -1].Count; i++) {

                tempData.Add(new DataItem {
                    latency = dataList[dataList.Count - 1][i].latency,
                    hostname = dataList[dataList.Count - 1][i].hostname
                });
            }

            if (CheckIfCancel()) {

                return;
            }

            for (int i = 0; i < dataList.Count; i++) {

                for (int j = 0; j < dataList[i].Count; j++) {

                    if (dataList[i].Count > j) {

                        packetLossList.Add(dataList[i][j].packetloss);
                    }

                }

            }
            
            int offset = 1;
            int tempSplit = dataList[0].Count;

            for (int i = 0; i < packetLossList.Count; i++) {

                int j = 0;

                while (j < tempSplit && (offset * routeList.Count) < dataList[0].Count) {

                    if (CheckIfCancel()) {

                        return;
                    }

                    try {
                        
                        packetLossList[j] += packetLossList[j + (offset * routeList.Count)];
                        j++;
                    }
                    catch (ArgumentOutOfRangeException ex) {
#if DEBUG
                        Console.WriteLine("i is: {0} j is: {1}", i, j);
                        Console.WriteLine("dataList: {0} packetLossList: {1}", dataList.Count, packetLossList.Count);
                        Console.WriteLine(ex.Message);
#endif
                        throw;
                    }
                }
                offset++;

            }


            if (dataList.Count > 1) {

                for (int i = 0; i < routeList.Count; i++) {

                    if (CheckIfCancel()) {

                        return;
                    }

                    packetLossList[i] = packetLossList[i] / (dataList.Count - 1);
                    tempData[i].packetloss = packetLossList[i];
                }
            }


            for (int i = 0; i < tempData.Count; i++) {

                if (CheckIfCancel()) {

                    return;
                }

                try {

                    latencyList.Add(tempData[i].latency);
                    addRow(new DataItem { jumps = (i + 1).ToString(), latency = tempData[i].latency, hostname = tempData[i].hostname, packetloss = tempData[i].packetloss });
                }
                catch (ArgumentOutOfRangeException ex) {
#if DEBUG
                    Console.WriteLine(ex.Message);
#endif
                    throw;
                }

            }

            Dispatcher.Invoke(() => traceData.ItemsSource = dataTbl.DefaultView);
            Dispatcher.Invoke(() => traceData.DataContext = dataTbl.DefaultView);

            Dispatcher.Invoke(() => _grapher.BuildGraph(
                latencyList[latencyList.Count -1],
                packetLossList[packetLossList.Count -1],
                ip)
            );
            

            tempData.Clear();
        }

        private void addRow(DataItem data) {

            DataRow newRow = dataTbl.NewRow();

            newRow[0] = data.jumps;
            newRow[1] = data.hostname;
            newRow[2] = data.latency;
            newRow[3] = data.packetloss.ToString("0.00");

            dataTbl.Rows.Add(newRow);
        }

        private void graphInterval_SelectionChanged(object sender, SelectionChangedEventArgs e) {

            if (_grapher != null) {

                switch (((ComboBox)e.Source).SelectedIndex) {

                    case 0:
                        _grapher.SetGraphSizing(30f, 12f);
                        _grapher.RebuildGraph(dataList);
                        break;

                    case 1:
                        _grapher.SetGraphSizing(10f, 4f);
                        _grapher.RebuildGraph(dataList);
                        break;

                    case 2:
                        _grapher.SetGraphSizing(5f, 2f);
                        _grapher.RebuildGraph(dataList);
                        break;

                    case 3:
                        _grapher.SetGraphSizing(2.5f, 1f);
                        _grapher.RebuildGraph(dataList);
                        break;

                    case 4:
                        _grapher.SetGraphSizing(1.5f, 0.75f);
                        _grapher.RebuildGraph(dataList);
                        break;

                    case 5:
                        _grapher.SetGraphSizing(0.5f, 0.3f);
                        _grapher.RebuildGraph(dataList);
                        break;

                    default:
                        break;
                }
            }            
        }


        private void exitBtn_Click(object sender, RoutedEventArgs e) {

            Application.Current.Shutdown();
        }


        private void saveBtn_Click(object sender, RoutedEventArgs e) {

            SaveTrace();
        }


        private void SaveTrace() {
            
            if (dataList.Count < 1) {

                MessageBox.Show("No data to save");
                return;
            }
            
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();

            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text Files (*.txt)|.txt";

            bool? result = dlg.ShowDialog();

            if (result == true) {

                CheckAccessPermission(dlg.FileName);

                if (dlg.CheckFileExists) {

                    string message = "Do you want to overwrite this file: " + dlg.SafeFileName + "?";
                    string caption = "File already exists!";
                    MessageBoxButton button = MessageBoxButton.YesNo;                    
                    MessageBoxResult msgResult = MessageBox.Show(this, message, caption, button);

                    if (msgResult == MessageBoxResult.No) {

                        return;
                    }

                }

                using (StreamWriter sw = new StreamWriter(dlg.FileName)) {

                    for (int i = 0; i < dataList.Count; i++) {

                        sw.WriteLine("\t Set {0}", (i + 1) );

                        string output = String.Format(
                            "\t\t {0}/{1} {2}/{3}/{4}",
                            dataList[i][0].Time.Hour,
                            dataList[i][0].Time.Minute,
                            dataList[i][0].Time.Day,
                            dataList[i][0].Time.Month,
                            dataList[i][0].Time.Year
                            );

                        sw.WriteLine(output);

                        for (int j = 0; j < dataList[i].Count; j++) {

                            
                            sw.WriteLine("\t\t Jump \t latency \t\t host \t\t\t packetloss \n");
                            sw.WriteLine(
                                "\t\t {0} \t {1} \t\t\t {2} \t\t\t {3}", 
                                (i + 1),
                                dataList[i][j].latency,
                                dataList[i][j].hostname,
                                dataList[i][j].packetloss
                                );

                        }

                    }

                }

                return;
            }

        }

        private bool CheckAccessPermission(string path) {

            FileIOPermission f = new FileIOPermission(FileIOPermissionAccess.Write, path);

            try {

                f.Demand();

            }
            catch (SecurityException ex) {

                MessageBox.Show("You do not have write access to destination", "Error!");

                if (!EventLog.SourceExists("OpenTraceRT")) {

                    EventLog.CreateEventSource("OpenTraceRT", "IOAccessError");
                }

                EventLog myLog = new EventLog();
                myLog.Source = "OpenTraceRT";

                string entry = "User does not have write permission for: " + path;
                myLog.WriteEntry(entry);
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
                return false;
            }

            return true;
        }


        private void traceData_LoadingRow(object sender, DataGridRowEventArgs e) {
            Style style = new Style(typeof(DataGridCell));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));

            traceData.Columns[3].CellStyle = style;
        }
    }
}
