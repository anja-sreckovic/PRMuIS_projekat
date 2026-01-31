using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Client
{
    public partial class MainWindow : Window
    {
        private Socket _sock;
        private CancellationTokenSource _cts;
        private Task _rxTask;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if (!int.TryParse(PortBox.Text, out port) || port < 1 || port > 65535)
            {
                MessageBox.AppendText("Neispravan port.");
                return;
            }

            IPAddress ip;
            if (!IPAddress.TryParse((HostBox.Text ?? "").Trim(), out ip))
            {
                MessageBox.AppendText("Neispravan host/IP.");
                return;
            }

            try
            {
                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _sock.Connect(new IPEndPoint(ip, port));

                _cts = new CancellationTokenSource();
                _rxTask = Task.Run(() => ReceiveLoop(_cts.Token));


                ConnectBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = true;
                SendBtn.IsEnabled = true;
                StatusText.Text = "Connected";

                Log("Povezan na " + ip + ":" + port);
            }
            catch (Exception ex)
            {
                MessageBox.AppendText("Ne mogu da se povežem: " + ex.Message);
            }
        }

        private void DisconnectBtn_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            
        }

        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            SendCurrentMessage();
        }

        private void MessageBox_KeyDown(object sender, KeyEventArgs e)
        {
            
        }

        private void SendCurrentMessage()
        {
            
        }

        private void ReceiveLoop(CancellationToken token)
        {
            byte[] buf = new byte[4096];

            while (!token.IsCancellationRequested)
            {
                Socket s = _sock;
                if (s == null) break;

                try
                {
                    int n = s.Receive(buf);
                    if (n == 0)
                    {
                        Log("Server je zatvorio konekciju.");
                        Dispatcher.Invoke(() => Disconnect());
                        break;
                    }

                    string text = Encoding.UTF8.GetString(buf, 0, n);
                    Log(text);
                }
                catch (SocketException ex)
                {
                    Log("SocketException: " + ex.SocketErrorCode);
                    Dispatcher.Invoke(() => Disconnect());
                    break;
                }
                catch (Exception ex)
                {
                    Log("Receive error: " + ex.Message);
                    Dispatcher.Invoke(() => Disconnect());
                    break;
                }
            }
        }

        private void Log(string line)
        {
            Dispatcher.Invoke(() =>
            {
                ChatBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + "\n");
                ChatBox.ScrollToEnd();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            base.OnClosed(e);
        }
    }
}
