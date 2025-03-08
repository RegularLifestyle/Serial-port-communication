using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
using System.IO.Ports;
using System.Collections.ObjectModel;
using System.Threading;
namespace 串口通信WPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 变量
        //服务器相关
        private TcpListener server;
        private TcpClient connectedClient;
        private bool IsServerRunning = false;

        //客户端相关
        private TcpClient client;
        private NetworkStream clientStream;
        private bool IsClientConnected = false;

        //串口相关
        private SerialPort SerialPort;
        private bool isSerialOpen;
        public ObservableCollection<SerialPort> AvailablePorts { get; } = new ObservableCollection<SerialPort>();
        #endregion
        public MainWindow()
        {
            InitializeComponent();
            LoadSerialPorts();
            DataContext = this;
        }
        #region 服务器端逻辑

        #region 启动服务器
        //启动服务器
        private async void BtnStartServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ip = IPAddress.Parse(txtServerIP.Text);
                var port = int.Parse(txtServerPort.Text);
                server = new TcpListener(ip, port);
                server.Start();
                IsServerRunning = true;
                tbServerStatus.Text = "服务器已启动，等待连接...";
                _ = Task.Run(() => ListenForClients());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动服务器失败：{ex.Message}");
            }
        }
        #endregion

        #region 异步监听方法
        private async Task ListenForClients()
        {
            try
            {
                while (IsServerRunning)
                {
                    connectedClient = await server.AcceptTcpClientAsync();
                    Dispatcher.Invoke(new Action(() =>
                    {
                        lbServerLog.Items.Add($"客户端已连接;{connectedClient.Client.RemoteEndPoint}");
                    }));
                    _ = Task.Run(() => Handleclient(connectedClient));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"监听错误：{ex.Message}"));
            }
        }
        #endregion

        #region 处理客户端信息
        //处理客户端信息
        private async Task Handleclient(TcpClient client)
        {
            try
            {
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[1024 * 3];
                    while (true)
                    {
                        //ReadAsync方法中缓冲区重用机制，每次读取都会覆盖buffer中的数据，就是自动清空
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) { break; }
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Dispatcher.Invoke(() =>
                        {
                            lbServerLog.Items.Add($"收到消息:{message}");
                            //转发到串口
                            if (isSerialOpen)
                            {
                                byte[]data=Encoding.ASCII.GetBytes(message+"\n");
                                SerialPort.Write(data, 0, data.Length);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"客户端处理错误: {ex.Message}"));
            }
        }
        #endregion
        #endregion

        #region 客户端逻辑

        #region 客户端接收数据处理
        //客户端接收数据处理
        private async Task ReceiveClientMessages()
        {
            try
            {
                var buffer = new byte[1024];


                while (IsClientConnected)
                {
                    int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) { break; }//？哪里清空？
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Dispatcher.Invoke(() =>
                    {
                        lbClientLog.Items.Add($"收到消息{message}");
                    });
                }

            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"客户端处理错误: {ex.Message}"));
            }
        }
        #endregion

        #endregion

        #region 发送数据方法
        private async void SendMessage(NetworkStream stream, TextBox textBox, ListBox logList)
        {
            try
            {
                try
                {
                    string message = textBox.Text;
                    byte[] date = Encoding.UTF8.GetBytes(textBox.Text);
                    await stream.WriteAsync(date, 0, date.Length);
                    logList.Items.Add($"已发送：{message}");
                    textBox.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"发送失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }
        #endregion

        #region TCP客户端服务器通讯事件
        private void BtnServerSend_Click(object sender, RoutedEventArgs e)
        {
            if (connectedClient?.Connected == true)
            {
                SendMessage(connectedClient.GetStream(), txtServerMessage, lbServerLog);
            }
        }

        private void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {
            IsServerRunning = false;
            server?.Stop();
            tbServerStatus.Text = "服务器已停止";
        }
        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            IsClientConnected = false;
            client?.Close();
            tbClientStatus.Text = "已断开连接";
        }
        #region 建立连接
        //建立连接
        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                client = new TcpClient();
                await client.ConnectAsync(IPAddress.Parse(txtClientIP.Text), int.Parse(txtClientPort.Text));
                clientStream = client.GetStream();
                IsClientConnected = true;
                tbClientStatus.Text = $"已连接服务器：{server.Server.AddressFamily}";
                _ = Task.Run(() => ReceiveClientMessages());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}");
            }
        }


        #endregion

        private async void BtnClientSend_Click(object sender, RoutedEventArgs e)
        {
            if (client?.Connected == true)
            {
                SendMessage(clientStream, txtClientMessage, lbClientLog);
            }
        }
        #endregion

        #region 串口通信逻辑
        #region 加载可用串口
        private void LoadSerialPorts()
        {
            AvailablePorts.Clear();
            foreach (var item in SerialPort.GetPortNames())
            {
                AvailablePorts.Add(new SerialPort(item));
            }
        }
        #endregion

        #region 打开串口
        private void btnOpenPort_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbPorts.SelectedItem == null) return;

                SerialPort = cmbPorts.SelectedItem as SerialPort;
                SerialPort.BaudRate = int.Parse(txtBaudRate.Text);
                SerialPort.Parity = (Parity)cmbParity.SelectedIndex;
                SerialPort.DataBits = int.Parse((cmbDataBits.SelectedItem as ComboBoxItem).Content.ToString());
                SerialPort.StopBits = (StopBits)(cmbStopBits.SelectedIndex + 1);//停止位从1开始
                SerialPort.DataReceived += SerialPort_DataReceived;//订阅接收数据事件
                SerialPort.Open();
                isSerialOpen = true;
                lbSerialLog.Items.Add($"{DateTime.Now} 串口已打开");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开串口失败: {ex.Message}");
            }
        }
        //接收数据事件
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    byte[] buffer = new byte[SerialPort.BytesToRead];
                    SerialPort.Read(buffer, 0, buffer.Length);
                    Dispatcher.Invoke(() =>
                    {
                        string message = Encoding.ASCII.GetString(buffer);
                        lbSerialLog.Items.Add($"[RX] {message}");
                    });
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() => lbSerialLog.Items.Add("接收超时"));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"接收错误: {ex.Message}"));
                }
            }

        }

        #endregion
        //关闭串口
        private void btnClosePort_Click(object sender, RoutedEventArgs e)
        {
            if (SerialPort?.IsOpen == true)
            {
                SerialPort.Close();
                lbSerialLog.Items.Add($"{DateTime.Now} 串口已关闭");
                isSerialOpen = false;
            }
        }
        //窗口关闭时释放资源
        protected override void OnClosed(EventArgs e)
        {
            SerialPort?.Close();
            base.OnClosed(e);
        }
        #endregion
        //自定义协议处理（示例：以换行符为结束标记）
        private StringBuilder serialBuffer = new StringBuilder();
        private void ProcessSerialData(string rawData)
        {
            serialBuffer.Append(rawData);
            while (serialBuffer.ToString().Contains('\n'))
            {
                int index = serialBuffer.ToString().IndexOf('\n');
                string message = serialBuffer.ToString(0, index);
                serialBuffer.Remove(0, index + 1);

                Dispatcher.Invoke(() =>
                {
                    lbSerialLog.Items.Add($"[完整消息] {message}");
                });
            }
        }
    }
}
