// 
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
// 

using Examples.ExamplesFileTransfer.WPF.Queues;
using Microsoft.Win32;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using NetworkCommsDotNet.DPSBase;
using NetworkCommsDotNet.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Examples.ExamplesFileTransfer.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Private Fields
        /// <summary>
        /// Data context for the GUI list box
        /// </summary>
        ObservableCollection<ReceivedFile> receivedFiles = new ObservableCollection<ReceivedFile>();

        /// <summary>
        /// References to received files by remote ConnectionInfo
        /// </summary>
        Dictionary<ConnectionInfo, Dictionary<string, ReceivedFile>> receivedFilesDict = new Dictionary<ConnectionInfo, Dictionary<string, ReceivedFile>>();

        /// <summary>
        /// Incoming partial data cache. Keys are ConnectionInfo, PacketSequenceNumber. Value is partial packet data.
        /// </summary>
        Dictionary<ConnectionInfo, Dictionary<long, byte[]>> incomingDataCache = new Dictionary<ConnectionInfo, Dictionary<long, byte[]>>();

        /// <summary>
        /// Incoming sendInfo cache. Keys are ConnectionInfo, PacketSequenceNumber. Value is sendInfo.
        /// </summary>
        Dictionary<ConnectionInfo, Dictionary<long, SendInfo>> incomingDataInfoCache = new Dictionary<ConnectionInfo, Dictionary<long, SendInfo>>();

        /// <summary>
        /// Custom sendReceiveOptions used for sending files. Can be changed via GUI.
        /// </summary>
        SendReceiveOptions customOptions = new SendReceiveOptions<ProtobufSerializer>();

        /// <summary>
        /// Object used for ensuring thread safety.
        /// </summary>
        object syncRoot = new object();

        /// <summary>
        /// Boolean used for suppressing errors during GUI close
        /// </summary>
        static volatile bool windowClosing = false;

        private CancellationTokenSource _ccToken;
        private QueueManagement _queueManagement;
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            //Set the listbox data context
            lbReceivedFiles.DataContext = receivedFiles;
            //var ccToken = new CancellationTokenSource();
            //_queueManagement = new QueueManagement() { CancellationTokenSource = ccToken };
            _queueManagement = new QueueManagement();
            //_cctk = new CancellationTokenSource();
            ReceiveJob();
            //Start listening for new TCP connections
            StartListening();
            Task.Factory.StartNew(() => BtnAtSendIfHandled());
            Task.Factory.StartNew(() => BtnAtSendIfSpawned());
        }

        #region GUI Updates
        /// <summary>
        /// Adds a line to the GUI log window
        /// </summary>
        /// <param name="logLine"></param>
        private void AddLineToLog(string logLine)
        {
            logBox.Dispatcher.BeginInvoke(new Action(() => // mở thread ui riêng 
            {
                logBox.Text += DateTime.Now.ToShortTimeString() + " - " + logLine + "\n";
                scroller.ScrollToBottom(); // Kéo thanh cuộn xuống dưới cùng
            }));
        }

        /// <summary>
        /// Chạy thanh update (cái này chỉ thấy rõ khi gửi file nặng trong khi phân mảnh)
        /// </summary>
        /// <param name="percentComplete"></param>
        private void UpdateSendProgress(double percentComplete)
        {
            sendProgress.Dispatcher.BeginInvoke(new Action(() => // mở luồng ui riêng
            {
                sendProgress.Value = percentComplete;
            }));
        }

        /// <summary>
        /// thêm thông tin nhận được vào log 
        /// </summary>
        /// <param name="file"></param>
        private void AddNewReceivedItem(ReceivedFile file)
        {
            lbReceivedFiles.Dispatcher.BeginInvoke(new Action(() => // mở luồng ui riêng
            {
                receivedFiles.Add(file);
            }));
        }
        #endregion

        #region Job
        private void ReceiveJob()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    Parallel.ForEach(receivedFilesDict.Keys.ToList(), connectionInfo =>
                    {
                        lock (syncRoot)
                        {
                            if (receivedFilesDict.ContainsKey(connectionInfo))
                            {
                                List<ReceivedFile> receiverFiles = receivedFilesDict[connectionInfo].Values.ToList();
                                foreach (var file in receiverFiles)
                                {
                                    //if (file.IsCompleted) // chỉ áp dụng với các bộ byte[] bị phân mảnh trường hợp stream tính năng này không hợp động
                                    //{
                                    try
                                    {
                                        _queueManagement.TransferJobToGlobalQueue(file.Job);
                                        //AddLineToLog("-->> (GlobalQueue): '" + file.Filename + "' from '" + file.SourceInfoStr + "'" + "   " + _queueManagement.LQueueLog); // ghi log vào queue
                                        Thread.Sleep(100);
                                        AddLineToLog("-->> (GlobalQueue): '" + file.Filename + "' from '" + file.SourceInfoStr); // ghi quá tỉa log dễ bị văng
                                        AddLineToLog("---- " + file.Job.Message);
                                        receivedFilesDict[file.SourceInfo].Remove(file.Filename);
                                        AddLineToLog("-->> Delete: '" + file.Filename + "' receive from '" + file.SourceInfoStr + "'");
                                    }
                                    catch (Exception) { } // không giải nén được (Job không đúng format thì bỏ qua)
                                    //}
                                }
                            }
                        }
                    });
                    Thread.Sleep(1000);
                }
            });
        }
        private void SendJob()
        {
            //Parallel.ForEach(remoteIPs.Items.Cast<string>(), (remoteIPItem) =>
            try
            {
                foreach (string remoteIPItem in new string[] { remoteIPs.SelectedItem.ToString() })
                {
                    string[] ipPort = remoteIPItem.Split(':');
                    string remoteIP = ipPort[0];
                    string remotePort = ipPort[1];
                    UpdateSendProgress(0); //Set the send progress bar to 0
                    (string jobName, Stream jobStream) = _queueManagement.TransferJobToTCPSender_Job_Stream();
                    Task.Factory.StartNew(() => SendFileAsync(jobName, jobStream, remoteIP, remotePort)); // gửi song song không chờ đợi 
                                                                                                          //SendFileAsync(jobName, jobStream, remoteIP, remotePort).Wait();
                }
            }
            catch { } // chưa chọn IP nào trong comboBox gửi đi 
            //});
        }


        #endregion

        #region GUI Events
        /// <summary>
        /// Thiết lập tự động gửi khi xử lý hoàn tất 
        /// </summary>
        private void BtnAtSendIfHandled()
        {
            while (true)
            {
                bool bh;
                lock (btnAtSendIfHandled)
                    bh = btnAtSendIfHandled.IsChecked.Value;
                if (bh)
                {
                    //foreach (string handleJobName in _queueManagement.FindHandlerJob())
                    //    _queueManagement.SetJobToFirstInLocalQueue(handleJobName);
                    (string jobName, Stream jobStream) = _queueManagement.TransferJobToTCPSender_Job_Stream();
                    foreach (string remoteIPItem in remoteIPs.Items.Cast<string>().ToList())
                    {
                        string[] ipPort = remoteIPItem.Split(':');
                        string remoteIP = ipPort[0];
                        string remotePort = ipPort[1];
                        UpdateSendProgress(0); // Set the send progress bar to 0
                        Task.Factory.StartNew(() => SendFileAsync(jobName, jobStream, remoteIP, remotePort)); // gửi song song không chờ đợi
                    }
                }
            }
        }


        /// <summary>
        /// Thiết lập tự động gửi khi Spawn
        /// </summary>
        private void BtnAtSendIfSpawned()
        {
            while (true)
            {
                bool bh;
                bool bs;
                lock (btnAtSendIfHandled)
                    bh = btnAtSendIfHandled.IsChecked.Value;
                lock (btnAtSendIfSpawned)
                    bs = btnAtSendIfSpawned.IsChecked.Value;
                if (bs && !bh)
                    foreach (string remoteIPItem in remoteIPs.Items.Cast<string>().ToList())
                    {
                        string[] ipPort = remoteIPItem.Split(':');
                        string remoteIP = ipPort[0];
                        string remotePort = ipPort[1];
                        UpdateSendProgress(0); // Set the send progress bar to 0
                        //foreach (string spawnJobName in _queueManagement.FindSpawnerJob())
                        //    _queueManagement.SetJobToFirstInLocalQueue(spawnJobName);
                        (string jobName, Stream jobStream) = _queueManagement.TransferJobToTCPSender_Job_Stream();
                        Task.Factory.StartNew(() => SendFileAsync(jobName, jobStream, remoteIP, remotePort)); // gửi song song không chờ đợi
                    }
            }
        }


        /// <summary>
        /// Nếu nhấn xóa file thì xóa file đó khỏi listbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteFile_Clicked(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is ReceivedFile)
            {
                ReceivedFile fileToDelete = (ReceivedFile)cmd.DataContext;
                lock (syncRoot)
                {
                    //Delete the ReceivedFile from the listbox data context
                    receivedFiles.Remove(fileToDelete);

                    //Delete the ReceivedFile from the internal cache
                    if (receivedFilesDict.ContainsKey(fileToDelete.SourceInfo))
                        receivedFilesDict[fileToDelete.SourceInfo].Remove(fileToDelete.Filename);

                    fileToDelete.Close();
                }
                AddLineToLog("Deleted file '" + fileToDelete.Filename + "' from '" + fileToDelete.SourceInfoStr + "'");
            }
        }

        /// <summary>
        /// Save to disk
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveFile_Clicked(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is ReceivedFile)
            {
                //Use a SaveFileDialog to request the save location
                ReceivedFile fileToSave = (ReceivedFile)cmd.DataContext;
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.FileName = fileToSave.Filename;

                //If the user selected to save the file we write it to disk
                if (saveDialog.ShowDialog() == true)
                {
                    fileToSave.SaveFileToDisk(saveDialog.FileName);
                    AddLineToLog("Saved file '" + fileToSave.Filename + "' from '" + fileToSave.SourceInfoStr + "'");
                }
            }
        }

        ///// <summary>
        ///// Toggles the use of compression for sending files
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void UseCompression_Changed(object sender, RoutedEventArgs e)
        //{
        //    if (this.UseCompression.IsChecked == true)
        //    {
        //        //Set the customOptions to use ProtobufSerializer as a serialiser and LZMACompressor as the only data processor
        //        customOptions = new SendReceiveOptions<ProtobufSerializer, LZMACompressor>();
        //        AddLineToLog("Enabled compression.");
        //    }
        //    else if (this.UseCompression.IsChecked == false)
        //    {
        //        //Set the customOptions to use ProtobufSerializer as a serialiser without any data processors
        //        customOptions = new SendReceiveOptions<ProtobufSerializer>();
        //        AddLineToLog("Disabled compression.");
        //    }
        //}

        private void IpsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { // copy vào trong thằng clipboard giống như khi nhấn ctrl + c
            if (ipsBox.SelectedItem is ComboBoxItem selectedItem)
                Clipboard.SetText(selectedItem.Content.ToString());
        }

        private void RemoteIPs_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ComboBox comboBox = sender as ComboBox;
                if (comboBox != null && !string.IsNullOrWhiteSpace(comboBox.Text))
                {
                    if (!comboBox.Items.Contains(comboBox.Text))
                    {
                        comboBox.Items.Add(comboBox.Text);
                    }
                    comboBox.Text = string.Empty;
                }
            }
        }
        private void RemoteIPs_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                TextBlock textBlock = sender as TextBlock;
                if (textBlock != null)
                {
                    string itemToDelete = textBlock.Text;
                    if (!string.IsNullOrEmpty(itemToDelete))
                    {
                        remoteIPs.Items.Remove(itemToDelete);
                    }
                }
            }
        }



        /// <summary>
        /// Khi nào đóng thì clear hết file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            lock (syncRoot) //Close all files
            {
                foreach (ReceivedFile file in receivedFiles)
                    file.Close();
                if (_ccToken != null)
                {
                    _ccToken.Cancel();
                    _ccToken.Dispose();
                }
            }
            windowClosing = true;
            NetworkComms.Shutdown();
        }
        #endregion

        #region Comms
        /// <summary>
        /// Start listening for new TCP connections
        /// </summary>
        private void StartListening()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("PartialFileData", IncomingPartialFileData); // Kiểu file ngắn cho phép không đóng gói 
            NetworkComms.AppendGlobalIncomingPacketHandler<SendInfo>("PartialFileDataInfo", IncomingPartialFileDataInfo); // Kiểu phân mảnh 
            NetworkComms.AppendGlobalConnectionCloseHandler(OnConnectionClose); // OnConnectionClose = GC
            // Random port + IpAddress.Any (mở cổng bất kỳ để tránh trùng)
            Connection.StartListening(ConnectionType.TCP, new IPEndPoint(IPAddress.Any, 0));
            // Ghi log (mở kết nối)
            AddLineToLog("Initialised WPF file transfer example. Accepting TCP connections on:");
            foreach (IPEndPoint listenEndPoint in Connection.ExistingLocalListenEndPoints(ConnectionType.TCP))
                AddLineToLog(listenEndPoint.Address + ":" + listenEndPoint.Port);
            // thêm thông tin vào trong ipsBox
            foreach (IPEndPoint listenEndPoint in Connection.ExistingLocalListenEndPoints(ConnectionType.TCP))
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = listenEndPoint.Address + ":" + listenEndPoint.Port;
                ipsBox.Items.Add(item);
            }
        }

        /// <summary>
        /// Xử lý các bọc được gửi dưới dạng 
        /// <byte[]>("PartialFileData", IncomingPartialFileData);
        /// </summary>
        /// <param name="header">Header associated with incoming packet</param>
        /// <param name="connection">The connection associated with incoming packet</param>
        /// <param name="data">The incoming data</param>
        private void IncomingPartialFileData(PacketHeader header, Connection connection, byte[] data)
        {
            try
            {
                SendInfo info = null;
                ReceivedFile file = null;
                lock (syncRoot) // tránh lúc đang gửi có thằng nào đá vào 
                {
                    long sequenceNumber = header.GetOption(PacketHeaderLongItems.PacketSequenceNumber); // lấy thông số từ mảnh đầu vào 
                    if (incomingDataInfoCache.ContainsKey(connection.ConnectionInfo) && incomingDataInfoCache[connection.ConnectionInfo].ContainsKey(sequenceNumber))
                    {
                        info = incomingDataInfoCache[connection.ConnectionInfo][sequenceNumber]; // gộp mảnh 
                        incomingDataInfoCache[connection.ConnectionInfo].Remove(sequenceNumber);
                        // Check coi có chưa, chưa có thì tạo mới đầu mục 
                        if (!receivedFilesDict.ContainsKey(connection.ConnectionInfo))
                            receivedFilesDict.Add(connection.ConnectionInfo, new Dictionary<string, ReceivedFile>());
                        if (!receivedFilesDict[connection.ConnectionInfo].ContainsKey(info.Filename)) // đã có 
                        {
                            receivedFilesDict[connection.ConnectionInfo].Add(info.Filename, new ReceivedFile(info.Filename, connection.ConnectionInfo, info.TotalBytes));
                            AddNewReceivedItem(receivedFilesDict[connection.ConnectionInfo][info.Filename]);
                        }
                        file = receivedFilesDict[connection.ConnectionInfo][info.Filename];
                    }
                    else
                    {
                        // Đá tạm vào ram nếu chưa có đầu mục SendInfo
                        if (!incomingDataCache.ContainsKey(connection.ConnectionInfo))
                            incomingDataCache.Add(connection.ConnectionInfo, new Dictionary<long, byte[]>());
                        incomingDataCache[connection.ConnectionInfo].Add(sequenceNumber, data);
                    }
                }
                if (info != null && file != null && !file.IsCompleted) // có thể thêm dữ liệu vào trong ReceivedFile như cách làm byte[] Concat
                {
                    file.AddData(info.BytesStart, 0, data.Length, data);
                    file = null;
                    data = null;
                    GC.Collect();
                }
                else if (info == null ^ file == null)
                    throw new Exception("Either both are null or both are set. Info is " + (info == null ? "null." : "set.") + " File is " + (file == null ? "null." : "set.") + " File is " + (file.IsCompleted ? "completed." : "not completed."));
            }
            catch (Exception ex)
            {
                AddLineToLog("Exception - " + ex.ToString()); // tạo lỗi cho bọc truyền đi 
                LogTools.LogException(ex, "IncomingPartialFileDataError");
            }
        }

        /// <summary>
        /// Xử lý các bọc được gửi dưới dạng 
        /// <SendInfo>("PartialFileDataInfo", IncomingPartialFileDataInfo)
        /// </summary>
        /// <param name="header">Header associated with incoming packet</param>
        /// <param name="connection">The connection associated with incoming packet</param>
        /// <param name="data">The incoming data automatically converted to a SendInfo object</param>
        private void IncomingPartialFileDataInfo(PacketHeader header, Connection connection, SendInfo info)
        {
            try
            {
                byte[] data = null;
                ReceivedFile file = null;
                lock (syncRoot) // tránh lúc đang gửi có thằng nào đá vào 
                {
                    long sequenceNumber = info.PacketSequenceNumber; // info của thằng SendInfo đầu vào 
                    if (incomingDataCache.ContainsKey(connection.ConnectionInfo) && incomingDataCache[connection.ConnectionInfo].ContainsKey(sequenceNumber))
                    {
                        data = incomingDataCache[connection.ConnectionInfo][sequenceNumber];
                        incomingDataCache[connection.ConnectionInfo].Remove(sequenceNumber);
                        if (!receivedFilesDict.ContainsKey(connection.ConnectionInfo)) // coi mảnh trước có tồn tại không 
                            receivedFilesDict.Add(connection.ConnectionInfo, new Dictionary<string, ReceivedFile>());
                        if (!receivedFilesDict[connection.ConnectionInfo].ContainsKey(info.Filename)) // nếu có 
                        {
                            receivedFilesDict[connection.ConnectionInfo].Add(info.Filename, new ReceivedFile(info.Filename, connection.ConnectionInfo, info.TotalBytes));
                            AddNewReceivedItem(receivedFilesDict[connection.ConnectionInfo][info.Filename]);
                        }
                        file = receivedFilesDict[connection.ConnectionInfo][info.Filename]; // đá ra xong thì cho vào lại, tạo vùng cache mới 
                    }
                    else
                    {
                        // đá tam vào ram nếu chưa có đầu mục ReceiveFile cho phép truyền dần về máy 
                        if (!incomingDataInfoCache.ContainsKey(connection.ConnectionInfo))
                            incomingDataInfoCache.Add(connection.ConnectionInfo, new Dictionary<long, SendInfo>());
                        incomingDataInfoCache[connection.ConnectionInfo].Add(sequenceNumber, info);
                    }
                }
                if (data != null && file != null && !file.IsCompleted) // byte concat 
                {
                    file.AddData(info.BytesStart, 0, data.Length, data);
                    file = null;
                    data = null;
                    GC.Collect();
                }
                else if (data == null ^ file == null)
                    throw new Exception("Either both are null or both are set. Data is " + (data == null ? "null." : "set.") + " File is " + (file == null ? "null." : "set.") + " File is " + (file.IsCompleted ? "completed." : "not completed."));
            }
            catch (Exception ex)
            {
                AddLineToLog("Exception - " + ex.ToString()); // lỗi khi xử lý hoặc khởi tạo các bọc 
                LogTools.LogException(ex, "IncomingPartialFileDataInfo");
            }
        }

        /// <summary>
        /// Connect đóng thì cũng thu dọn luôn 
        /// </summary>
        /// <param name="conn">The closed connection</param>
        private void OnConnectionClose(Connection conn)
        {
            ReceivedFile[] filesToRemove = null;

            lock (syncRoot)
            {
                incomingDataCache.Remove(conn.ConnectionInfo); // xóa cache 
                incomingDataInfoCache.Remove(conn.ConnectionInfo);
                // thằng nào lỗi cũng clear luôn 
                if (receivedFilesDict.ContainsKey(conn.ConnectionInfo))
                {
                    filesToRemove = (from current in receivedFilesDict[conn.ConnectionInfo] where !current.Value.IsCompleted select current.Value).ToArray();
                    receivedFilesDict[conn.ConnectionInfo] = (from current in receivedFilesDict[conn.ConnectionInfo] where current.Value.IsCompleted select current).ToDictionary(entry => entry.Key, entry => entry.Value);
                }
            }
            //Update the GUI
            lbReceivedFiles.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (syncRoot)
                {
                    if (filesToRemove != null)
                        foreach (ReceivedFile file in filesToRemove)
                        {
                            receivedFiles.Remove(file);
                            file.Close();
                        }
                }
            }));
            AddLineToLog("Connection closed with " + conn.ConnectionInfo.ToString()); // ghi log
        }

        /// <summary>
        /// Gửi bọc cần xử lý 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            SendJob();
            ////Create an OpenFileDialog so that we can request the file to send
            //OpenFileDialog openDialog = new OpenFileDialog();
            //openDialog.Multiselect = false;
            //if (openDialog.ShowDialog() == true)
            //{
            //    //Disable the send and compression buttons
            //    sendFileButton.IsEnabled = false;
            //    //UseCompression.IsEnabled = false;
            //    string filename = openDialog.FileName;
            //    string selectedRemoteIP = string.Empty;
            //    if (string.IsNullOrEmpty(remoteIPs.SelectedItem.ToString()) && remoteIPs.Items.Count > 0)
            //        selectedRemoteIP = remoteIPs.Items[0].ToString();
            //    else if (remoteIPs.Items.Count > 0)
            //        selectedRemoteIP = remoteIPs.SelectedItem.ToString();
            //    if (!string.IsNullOrEmpty(selectedRemoteIP))
            //    {
            //        string[] ipPort = selectedRemoteIP.Split(':');
            //        string remoteIP = ipPort[0];
            //        string remotePort = ipPort[1];
            //        UpdateSendProgress(0); //Set the send progress bar to 0
            //        FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            //        //string shortFileName = System.IO.Path.GetFileName(filename);
            //        Task.Factory.StartNew(() => SendFileAsync(filename, stream, remoteIP, remotePort));
            //    }
            //}
        }
        private void SendFilesToAllRemoteIPs()
        {
            // Create an OpenFileDialog so that we can request the file to send
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Multiselect = false;
            if (openDialog.ShowDialog() == true)
            {
                string filename = openDialog.FileName;
                var remoteIPItems = remoteIPs.Items.Cast<string>().ToList();
                UpdateSendProgress(0);
                FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
                Parallel.ForEach(remoteIPItems, (selectedRemoteIP) =>
                {
                    string[] ipPort = selectedRemoteIP.Split(':');
                    string remoteIP = ipPort[0];
                    string remotePort = ipPort[1];
                    SendFileAsync(filename, stream, remoteIP, remotePort).Wait();
                });
            }
        }
        private Task SendFileAsync(string filename, Stream stream, string remoteIP, string remotePort)
        {
            UpdateSendProgress(0); // Set the send progress bar to 0
            return Task.Factory.StartNew(() => // Perform the send in a task so that we don't lock the GUI
            {
                try
                {
                    string shortFileName = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    // băm hash512 cho dtPkName
                    using (SHA512 sha512 = SHA512.Create())
                    {
                        byte[] hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(shortFileName));
                        StringBuilder sb = new StringBuilder();
                        foreach (byte b in hashBytes)
                            sb.Append(b.ToString("x2"));
                        shortFileName = sb.ToString();
                    }
                    StreamTools.ThreadSafeStream safeStream = new StreamTools.ThreadSafeStream(stream);
                    ConnectionInfo remoteInfo;
                    try { remoteInfo = new ConnectionInfo(remoteIP, int.Parse(remotePort)); }
                    catch (Exception) { throw new InvalidDataException("Failed to parse remote IP and port. Check and try again."); }
                    Connection connection = TCPConnection.GetConnection(remoteInfo); // Create connection
                    long sendChunkSizeBytes = (long)(stream.Length / 20.0) + 1; // Fragment size
                    long maxChunkSizeBytes = 500L * 1024L * 1024L; // Max fragment size
                    if (sendChunkSizeBytes > maxChunkSizeBytes) sendChunkSizeBytes = maxChunkSizeBytes;
                    long totalBytesSent = 0;
                    do
                    {
                        long bytesToSend = (totalBytesSent + sendChunkSizeBytes < stream.Length ? sendChunkSizeBytes : stream.Length - totalBytesSent);
                        StreamTools.StreamSendWrapper streamWrapper = new StreamTools.StreamSendWrapper(safeStream, totalBytesSent, bytesToSend); // Wrap data
                        long packetSequenceNumber;
                        connection.SendObject("PartialFileData", streamWrapper, customOptions, out packetSequenceNumber);
                        connection.SendObject("PartialFileDataInfo", new SendInfo(shortFileName, stream.Length, totalBytesSent, packetSequenceNumber), customOptions);
                        totalBytesSent += bytesToSend; // Amount sent
                        UpdateSendProgress((double)totalBytesSent / stream.Length);
                    } while (totalBytesSent < stream.Length);
                    GC.Collect(); // Garbage collection
                    AddLineToLog("Completed file send to '" + connection.ConnectionInfo.ToString() + "'.");
                }
                catch (CommunicationException) { AddLineToLog("Failed to complete send as connection was closed."); } // Send error due to large data
                catch (Exception ex)
                {
                    if (!windowClosing && ex.GetType() != typeof(InvalidDataException)) // Event when closing form
                    {
                        AddLineToLog(ex.Message.ToString());
                        LogTools.LogException(ex, "SendFileError");
                    }
                }
                UpdateSendProgress(0); // Once the send is finished reset the send progress bar
                sendFileButton.Dispatcher.BeginInvoke(new Action(() => // Once complete enable the send button again
                {
                    sendFileButton.IsEnabled = true;
                    // UseCompression.IsEnabled = true;
                }));
            });
        }

        #endregion
    }
}
