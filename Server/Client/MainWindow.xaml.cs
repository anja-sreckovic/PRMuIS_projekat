using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace Client
{
    public partial class MainWindow : Window
    {
        private Socket _sock;
        private TcpClient _tcpClient;
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
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts = null;
                }

                if (_sock != null)
                {
                    try { _sock.Shutdown(SocketShutdown.Both); }
                    catch { }
                    _sock.Close();
                    _sock = null;
                }

                if (_tcpClient != null)
                {
                    try { _tcpClient.Close(); }
                    catch { }
                    _tcpClient = null;
                }
            }
            catch (Exception ex)
            {
                Log("Disconnect error: " + ex.Message);
            }

            Dispatcher.Invoke(() =>
            {
                ConnectBtn.IsEnabled = true;
                DisconnectBtn.IsEnabled = false;
                SendBtn.IsEnabled = false;
                StatusText.Text = "Disconnected";
            });
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
            string msg = (MessageBox.Text ?? "").Trim();
            if (msg.Length == 0) return;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                if (_tcpClient != null && _tcpClient.Connected)
                {
                    // saljemo preko TCP konekcije
                    NetworkStream ns = _tcpClient.GetStream();
                    ns.Write(data, 0, data.Length);
                }
                else if (_sock != null)
                {
                    // dok se ne uspostavi TCP, saljemo preko UDP
                    _sock.Send(data);
                }
                MessageBox.Clear();
            }
            catch (Exception ex)
            {
                Log("Send failed: " + ex.Message);
                Disconnect();
            }
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

                    // očekujemo format: "IP: 127.0.0.1 Port: 1234"
                    TryOpenTcpFromServerResponse(text);
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

        private void TryOpenTcpFromServerResponse(string response)
        {
            // očekuje se format "IP: 127.0.0.1 Port: 1234"
            if (string.IsNullOrWhiteSpace(response)) return;

            try
            {
                var match = Regex.Match(response, @"IP:\s*(?<ip>[^\s]+)\s+Port:\s*(?<port>\d+)");
                if (!match.Success) return;

                string ipStr = match.Groups["ip"].Value;
                string portStr = match.Groups["port"].Value;

                IPAddress ip;
                int port;
                if (!IPAddress.TryParse(ipStr, out ip))
                {
                    Log("Neispravan IP u odgovoru servera: " + ipStr);
                    return;
                }
                if (!int.TryParse(portStr, out port))
                {
                    Log("Neispravan port u odgovoru servera: " + portStr);
                    return;
                }

                // zatvori prethodnu TCP konekciju ako postoji
                if (_tcpClient != null)
                {
                    try { _tcpClient.Close(); }
                    catch { }
                    _tcpClient = null;
                }

                // otvaramo novu TCP konekciju
                _tcpClient = new TcpClient();
                _tcpClient.Connect(ip, port);

                Log("Otvorena TCP konekcija na " + ip + ":" + port);

                // nakon uspostavljanja TCP konekcije, ugasimo UDP soket
                if (_sock != null)
                {
                    try { _sock.Shutdown(SocketShutdown.Both); } catch { }
                    try { _sock.Close(); } catch { }
                    _sock = null;
                }
            }
            catch (Exception ex)
            {
                Log("Ne mogu da otvorim TCP konekciju: " + ex.Message);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            base.OnClosed(e);
        }
    }
}
