using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Server
{
    public partial class MainWindow : Window
    {
        private Socket _listener;
        private Task _serverTask;
        private readonly List<Socket> _clients = new List<Socket>();
        private readonly object _lock = new object();

        private CancellationTokenSource _cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if (!int.TryParse(PortBox.Text, out port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Neispravan port.");
                return;
            }

            try
            {
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _listener.Bind(new IPEndPoint(IPAddress.Any, port));
                _listener.Blocking = false;

                _cts = new CancellationTokenSource();
                _serverTask = Task.Run(() => ServerLoop(_cts.Token));

                Log("Server START na portu " + port);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Greška pri startu servera: " + ex.Message);
            }

        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            try
            {
                if (_cts != null) _cts.Cancel();

                lock (_lock)
                {
                    for (int i = 0; i < _clients.Count; i++)
                        SafeClose(_clients[i]);
                    _clients.Clear();
                }

                SafeClose(_listener);
                _listener = null;

                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                StatusText.Text = "Stopped";

                Log("Server STOP");
            }
            catch (Exception ex)
            {
                Log("Stop error: " + ex.Message);
            }
        }

        private void BroadcastBtn_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void ServerLoop(CancellationToken token)
        {
            byte[] buffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    int received = _listener.ReceiveFrom(buffer, ref remoteEP);
                    string message = Encoding.UTF8.GetString(buffer, 0, received);

                    Console.WriteLine($"Primljeno od {remoteEP}: {message}");

                    string response = "Server je primio: " + message;
                    byte[] respBytes = Encoding.UTF8.GetBytes(response);
                    _listener.SendTo(respBytes, remoteEP);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Socket greška: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greška: " + ex.Message);
            }
        }

        private void AcceptClient(Socket listener)
        {
            
        }

        private void ReceiveFromClient(Socket client)
        {
            
        }

        private void Broadcast(string message)
        {
            
        }

        private void SendToClient(Socket client, string msg)
        {
            
        }

        private void RemoveClient(Socket client, string reason)
        {
            
        }

        private static void SafeClose(Socket s)
        {
            
        }

        private void Log(string line)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + "\n");
                LogBox.ScrollToEnd();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            StopServer();
            base.OnClosed(e);
        }

        private void BroadcastBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}
