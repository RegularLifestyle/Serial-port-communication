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
        #endregion
        public MainWindow()
        {
            InitializeComponent();
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
                    var buffer = new byte[1024*3];
                    while (true)
                    {
                        //ReadAsync方法中缓冲区重用机制，每次读取都会覆盖buffer中的数据，就是自动清空
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) { break; }
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Dispatcher.Invoke(() =>
                        {
                            lbServerLog.Items.Add($"收到消息:{message}");
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
    }
}
