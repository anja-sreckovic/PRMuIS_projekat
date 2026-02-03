using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Server
{
    public partial class MainWindow : Window
    {
        private Socket _listener;
        private Socket _igra;
        private Task _serverTask;
        private readonly List<Socket> _clients = new List<Socket>();
        private readonly Dictionary<Socket, bool> _ready = new Dictionary<Socket, bool>();
        private readonly object _lock = new object();
        private int port;

        private readonly Dictionary<Socket, Igrac> _igraci = new Dictionary<Socket, Igrac>();
        private string _pendingNadimak;
        private int _pendingBrojIgara;
        private List<string> _pendingIgre;
        private int _nextId = 1;

        // trening rezim: true ako je povezan samo jedan klijent
        private bool _trening;

        private CancellationTokenSource _cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PortBox.Text, out port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Neispravan port.");
                return;
            }

            try
            {
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _igra = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _igra.Bind(new IPEndPoint(IPAddress.Any, port + 1));
                _igra.Listen(2);
                _listener.Bind(new IPEndPoint(IPAddress.Any, port));
                _listener.Blocking = false;

                _cts = new CancellationTokenSource();
                _serverTask = Task.Run(() => ServerLoop(_cts.Token));

                StartBtn.IsEnabled = false;
                StopBtn.IsEnabled = true;
                StatusText.Text = "Running on " + port;

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

                SafeClose(_listener);
                _listener = null;

                SafeClose(_igra);
                _igra = null;

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

        private void ReceiveFromClient(Socket client)
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int received = client.Receive(buffer);
                    if (received <= 0)
                    {
                        Log("TCP klijent se odjavio: " + client.RemoteEndPoint);
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, received).Trim();
                    Log("TCP poruka od " + client.RemoteEndPoint + ": " + message);

                    Igrac igrac;
                    lock (_lock)
                    {
                        if (!_igraci.TryGetValue(client, out igrac))
                            continue;
                    }

                    // komande za anagram igru
                    if (message.StartsWith("REC:", StringComparison.OrdinalIgnoreCase))
                    {
                        // proverimo da li je igrac prijavljen za igru anagrama ("an")
                        if (igrac.Igre == null || !igrac.Igre.Contains("an"))
                        {
                            string info = $"Igrac {igrac.Nadimak} je poslao komandu za anagram, ali se nije prijavio za tu igru.";
                            Log(info);

                            // posaljemo istu poruku i klijentu
                            try
                            {
                                byte[] resp = Encoding.UTF8.GetBytes(info);
                                client.Send(resp);
                            }
                            catch (SocketException)
                            {
                                // ignorisemo gresku pri slanju ka klijentu
                            }
                            continue;
                        }

                        string rec = message.Substring(4).Trim();

                        // u trening rezimu dozvoljavamo vise poruka,
                        // u suprotnom prihvatamo samo prvu rec za ovog igraca
                        bool dozvoljeno = true;
                        if (!_trening && !string.IsNullOrEmpty(igrac.Anagram.Original))
                        {
                            dozvoljeno = false;
                        }

                        if (dozvoljeno)
                        {
                            try
                            {
                                igrac.Anagram.UcitajRec(rec);
                                Log($"Igracu {igrac.Nadimak} postavljena rec za anagram: {rec}");
                            }
                            catch (Exception ex)
                            {
                                Log("Greska kod UcitajRec: " + ex.Message);
                            }
                        }
                        else
                        {
                            Log($"Igrac {igrac.Nadimak} je pokusao da promeni rec za anagram, ali to nije dozvoljeno u ovom rezimu.");
                        }
                    }
                    else if (message.StartsWith("ANAGRAM:", StringComparison.OrdinalIgnoreCase))
                    {
                        // proverimo da li je igrac prijavljen za igru anagrama ("an")
                        if (igrac.Igre == null || !igrac.Igre.Contains("an"))
                        {
                            Log($"Igrac {igrac.Nadimak} je poslao komandu ANAGRAM, ali se nije prijavio za igru anagrama.");
                            continue;
                        }

                        string predlog = message.Substring(8).Trim();

                        // u trening rezimu dozvoljavamo vise predloga,
                        // u suprotnom prihvatamo samo prvi predlog (ako je vec neki poen upisan, ignorisemo dalje)
                        bool dozvoljeno = true;
                        if (!_trening && igrac.poeni.Count > 0 && igrac.poeni[0] > 0)
                        {
                            dozvoljeno = false;
                        }

                        if (dozvoljeno)
                        {
                            igrac.Anagram.PostaviPredlog(predlog);
                            int poeni = igrac.Anagram.ProveriAnagram();
                            // upisujemo poene za igru anagrama u prvu igru (indeks 0)
                            if (igrac.poeni.Count > 0)
                            {
                                igrac.poeni[0] += poeni;
                            }

                            string info = $"Igrac {igrac.Nadimak} predlozio anagram '{predlog}', dobija {poeni} poena, ukupno { (igrac.poeni.Count > 0 ? igrac.poeni[0] : 0) }";
                            Log(info);

                            // posaljemo istu poruku i klijentu
                            try
                            {
                                byte[] resp = Encoding.UTF8.GetBytes(info);
                                client.Send(resp);
                            }
                            catch (SocketException)
                            {
                                // ignorisemo gresku pri slanju ka klijentu
                            }
                        }
                        else
                        {
                            Log($"Igrac {igrac.Nadimak} je poslao vise predloga za anagram, ali je dozvoljen samo jedan u ovom rezimu.");
                        }
                    }

                    // ako klijent posalje START, oznacavamo ga kao spremnog
                    if (string.Equals(message.Trim(), "START", StringComparison.OrdinalIgnoreCase))
                    {
                        lock (_lock)
                        {
                            if (_ready.ContainsKey(client))
                                _ready[client] = true;

                            bool allReady = _igraci.Count > 0;
                            foreach (var kvp in _igraci)
                            {
                                Socket c = kvp.Key;

                                bool isReady;
                                if (!_ready.TryGetValue(c, out isReady) || !isReady)
                                {
                                    allReady = false;
                                    break;
                                }
                            }

                            if (allReady)
                            {
                                Log("IGRA POCINJE!");
                            }
                        }
                    }
                }
            }
            catch (SocketException ex)
            {
                Log("TCP greška za klijenta " + client.RemoteEndPoint + ": " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("Greška u TCP klijentu " + client.RemoteEndPoint + ": " + ex.Message);
            }
            finally
            {
                lock (_lock)
                {
                    _igraci.Remove(client);
                    _ready.Remove(client);

                    // azuriramo trening rezim posle diskonekcije
                    _trening = _igraci.Count == 1;
                }
                SafeClose(client);
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
                    try
                    {
                        // prihvati nove TCP klijente ako postoje
                        AcceptTcpClientsIfPending();

                        int received = _listener.ReceiveFrom(buffer, ref remoteEP);
                        string message = Encoding.UTF8.GetString(buffer, 0, received);

                        Log($"Primljeno od {remoteEP}: {message}");

                        string response;

                        string pattern = @"^PRIJAVA:\s+([^,]+),\s*(an|po|as)(,\s*(an|po|as))*$";

                        if (Regex.IsMatch(message, pattern))
                        {
                            string[] delovi = message.Split(',');

                            _pendingNadimak = delovi[0].Replace("PRIJAVA:", "").Trim();
                            _pendingBrojIgara = delovi.Length - 1;

                            // sacuvamo igre iz prijave (an|po|as)
                            _pendingIgre = new List<string>();
                            for (int i = 1; i < delovi.Length; i++)
                            {
                                _pendingIgre.Add(delovi[i].Trim());
                            }

                            response = "IP: 127.0.0.1" + " Port: " + (port + 1);
                        }
                        else
                        {
                            response = "Neispravan format prijave.";
                        }
                        byte[] respBytes = Encoding.UTF8.GetBytes(response);
                        _listener.SendTo(respBytes, remoteEP);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                    {
                        // nema trenutno podataka, kratko sačekaj da ne trošiš CPU
                        Thread.Sleep(10);
                    }
                }
            }
            catch (SocketException ex)
            {
                Log("Socket greška: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("Greška: " + ex.Message);
            }
        }

        private void AcceptTcpClientsIfPending()
        {
            if (_igra == null) return;

            try
            {
                while (_igra.Poll(100, SelectMode.SelectRead))
                {
                    Socket client = _igra.Accept();

                    Igrac igrac = new Igrac(_nextId++, _pendingNadimak, _pendingBrojIgara, _pendingIgre);

                    lock (_lock)
                    {
                        _igraci.Add(client, igrac);
                        _clients.Add(client);
                        _ready[client] = false;

                        // azuriramo trening rezim: true ako je tacno jedan klijent povezan
                        _trening = _igraci.Count == 1;
                    }

                    Log("TCP klijent povezan: " + client.RemoteEndPoint);

                    // pokreni prijem za ovog klijenta u pozadini
                    Task.Run(() => ReceiveFromClient(client));
                }
            }
            catch (SocketException)
            {
                // ignorisi povremene greske pri Accept
            }
        }

        private static void SafeClose(Socket s)
        {
            if (s == null) return;
            try { s.Shutdown(SocketShutdown.Both); } catch { }
            try { s.Close(); } catch { }
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
