using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
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

        private readonly Dictionary<Socket, PitanjaIOdgovori> _quizIgre = new Dictionary<Socket, PitanjaIOdgovori>();
        // broj tacnih pogodaka po reci u igri anagram (redni broj tacnog odgovora)
        private int _anagramCorrectOrder = 0;
        // reci za igru anagrama (svaka duzine 10 slova)
        private readonly string[] _anagramReci = new string[]
        {
            "PROGRAMERI",
            "KOMPJUTER",
            "TASTATURA",
            "MONTIRATI", // prilagoditi reci po potrebi
            "ALGORITMIK",
            "SOFTVERSKI",
            "INFORMATIKA",
            "APLIKACIJA",
            "KRIPTOVANJE",
            "INTERNETPROTOKOL"
        };
        private readonly Random _rnd = new Random();
        string izabranaIgra = "";
        private bool kvisko_iskoriscen = false;

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

                    if (message.Equals("an", StringComparison.OrdinalIgnoreCase) || message.Equals("po", StringComparison.OrdinalIgnoreCase) || message.Equals("as", StringComparison.OrdinalIgnoreCase))
                    {
                        izabranaIgra = message.ToLower();
                        Log($"Igrac {igrac.Nadimak} izabrao igru: {izabranaIgra}");

                        // osiguravamo da lista poena ima 3 elementa (an, po, as)
                        while (igrac.poeni.Count < 3)
                            igrac.poeni.Add(0);

                        if (izabranaIgra == "as")
                        {
                            igrac.Asoc = new Asocijacija();
                            igrac.Asoc.UcitajIzFajla("asocijacije.txt");

                            client.Send(Encoding.UTF8.GetBytes(
                                "ASOCIJACIJE POCINJU\n" +
                                "Otvori: A1 B3\n" +
                                "Kolona: A:ODGOVOR\n" +
                                "Konacno: K:ODGOVOR" +
                                "\n\nIzaberite kolonu! (A-D)(1-4)"
                            ));
                            continue;
                        }

                        if (izabranaIgra == "po")
                        {
                            if (igrac.Igre == null || !igrac.Igre.Contains("po"))
                            {
                                Log($"Igrac {igrac.Nadimak} je poslao komandu po, ali se nije prijavio za igru pitanja i odgovori.");
                                continue;
                            }
                            // inicijalizujemo kviz ako ne postoji
                            if (!_quizIgre.ContainsKey(client))
                                _quizIgre[client] = new PitanjaIOdgovori();

                            var quiz = _quizIgre[client];

                            // šaljemo prvo pitanje
                            if (quiz.SledecePitanje())
                            {
                                string tekst = quiz.tek_pitanje + "\n a) DA\n b) NE\n (poslati: PO:a ili PO:b)\n (Kvisko: PO*:a)";
                                client.Send(Encoding.UTF8.GetBytes(tekst));
                            }
                        }
                        else if (izabranaIgra == "an")
                        {
                            if (igrac.Igre == null || !igrac.Igre.Contains("an"))
                            {
                                Log($"Igrac {igrac.Nadimak} je poslao komandu an, ali se nije prijavio za igru anagrama.");
                                continue;
                            }
                            // izaberi nasumicnu rec za anagram iz niza
                            string rec = _anagramReci[_rnd.Next(_anagramReci.Length)];

                            try
                            {
                                igrac.Anagram.UcitajRec(rec);
                                // nova rec -> resetujemo redosled tacnih odgovora
                                _anagramCorrectOrder = 0;
                            }
                            catch (Exception ex)
                            {
                                Log("Greska kod UcitajRec (anagram start): " + ex.Message);
                            }

                            // obavestavamo klijenta koja rec je izabrana
                            client.Send(Encoding.UTF8.GetBytes("Igra anagram pocinje! Rec za anagram je: " + rec));
                        }
                        else if (izabranaIgra == "as")
                        {
                            if (igrac.Igre == null || !igrac.Igre.Contains("as"))
                            {
                                Log($"Igrac {igrac.Nadimak} je poslao komandu as, ali se nije prijavio za igru asocijacije.");
                                continue;
                            }
                            igrac.Asoc = new Asocijacija();
                            igrac.Asoc.UcitajIzFajla("asocijacije.txt");
                            igrac.PogresniPokusaji = 0;

                            string stanje = igrac.Asoc.PrikaziAsocijaciju();
                            client.Send(Encoding.UTF8.GetBytes("Igra asocijacije počinje!\n" + stanje));
                        }

                        continue;
                    }
                    else if (message.StartsWith("ANAGRAM:", StringComparison.OrdinalIgnoreCase))
                    {
                        // proverimo da li je igrac prijavljen za igru anagrama ("an")
                        if (igrac.Igre == null || !igrac.Igre.Contains("an"))
                        {
                            Log($"Igrac {igrac.Nadimak} je poslao komandu ANAGRAM, ali se nije prijavio za igru anagrama.");
                            continue;
                        }

                        // podrzavamo i kvisko za anagram: ANAGRAM*:predlog
                        bool kvisko = message.StartsWith("ANAGRAM*:", StringComparison.OrdinalIgnoreCase);
                        string predlog = message.Substring(kvisko ? 9 : 8).Trim();

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
                            int bazniPoeni = igrac.Anagram.ProveriAnagram();

                            int poeni = 0;
                            if (bazniPoeni > 0)
                            {
                                // prvi tacan odgovor dobija pun broj poena,
                                // svaki sledeci tacan -15% u odnosu na prethodnog
                                double faktor = Math.Pow(0.85, _anagramCorrectOrder);
                                poeni = (int)Math.Round(bazniPoeni * faktor);
                                // kvisko u anagram igri: jednom dozvoljen dupli broj poena
                                if (kvisko && !igrac.Kvisko)
                                {
                                    poeni *= 2;
                                    igrac.Kvisko = true;
                                }
                                _anagramCorrectOrder++;

                                // upisujemo poene za igru anagrama u prvu igru (indeks 0)
                                if (igrac.poeni.Count > 0)
                                {
                                    if (igrac.poeni[0] == -1)
                                        igrac.poeni[0] = 0; // prvi put igra ovu igru
                                    igrac.poeni[0] += poeni;
                                }
                            }

                            string info = $"Igrac {igrac.Nadimak}, dobija {poeni} poena, ukupno { (igrac.poeni.Count > 0 ? igrac.poeni[0] : 0) }";
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

                            // nakon sto igrac zavrsi pokusaj u igri anagrama,
                            // ponovo ponudimo izbor igara
                            string sel2 = "Izaberite igru:\n\"an\" -> anagram\n\"po\" -> pitanja i odgovori\n\"as\"->asocijacije\n";
                            string sel3 = "NAPOMENA: igre koje niste naveli tokom prijave NECETE MOCI IGRATI.";
                            try
                            {
                                byte[] data = Encoding.UTF8.GetBytes(sel2 + "\n" + sel3 + "\n");
                                client.Send(data);
                            }
                            catch (SocketException)
                            {
                                // ignorisemo gresku pri slanju
                            }

                            // proverimo da li je zavrsena cela partija (sve igre) i proglasimo pobednika
                            ProveriIKompletirajIgruAkoGotova();
                        }
                        else
                        {
                            Log($"Igrac {igrac.Nadimak} je poslao vise predloga za anagram, ali je dozvoljen samo jedan u ovom rezimu.");
                        }
                    }
                    else if (message.StartsWith("PO", StringComparison.OrdinalIgnoreCase))
                    {
                        if (igrac.Igre == null || !igrac.Igre.Contains("po"))
                        {
                            Log($"Igrac {igrac.Nadimak} je poslao komandu PO, ali se nije prijavio za igru pitanja i odgovori.");
                            continue;
                        }
                        if (izabranaIgra != "po")
                            continue;

                        bool kvisko = message.StartsWith("PO*");
                        char odgovor = message.Last(); // a ili b

                        var quiz = _quizIgre[client];

                        int poeni = quiz.ObradiOdgovor(odgovor, kvisko);

                        if (kvisko && !kvisko_iskoriscen)
                        {
                            poeni = quiz.ObradiOdgovor(odgovor, kvisko);
                            kvisko_iskoriscen = true;
                        }
                        else
                            poeni = quiz.ObradiOdgovor(odgovor, !kvisko);

                        // osiguravamo da lista poena ima najmanje 3 elementa
                        while (igrac.poeni.Count < 3)
                                igrac.poeni.Add(0);

                        int idx = 1; // poeni za PO igru
                        if (igrac.poeni[idx] == -1)
                            igrac.poeni[idx] = 0; // prvi put igra ovu igru
                        igrac.poeni[idx] += poeni;

                        string info = $"Odgovor {(poeni > 0 ? "TAČAN" : "NETAČAN")} | +{poeni} poena\n";
                        client.Send(Encoding.UTF8.GetBytes(info));

                        // sledeće pitanje
                        if (quiz.SledecePitanje())
                        {
                            string tekst = quiz.tek_pitanje + "\n a) DA\n b) NE\n (PO:a ili PO:b)\n (Kvisko: PO*:a)";
                            client.Send(Encoding.UTF8.GetBytes(tekst));
                        }
                        else
                        {
                            client.Send(Encoding.UTF8.GetBytes("Kraj igre pitanja."));
                            kvisko_iskoriscen = false;

                            // nakon sto igrac zavrsi igru pitanja i odgovori,
                            // ponovo ponudimo izbor igara
                            string sel2 = "Izaberite igru:\n\"an\" -> anagram\n\"po\" -> pitanja i odgovori\n\"as\"->asocijacije\n";
                            string sel3 = "NAPOMENA: igre koje niste naveli tokom prijave NECETE MOCI IGRATI.";
                            try
                            {
                                byte[] data = Encoding.UTF8.GetBytes(sel2 + "\n" + sel3 + "\n");
                                client.Send(data);
                            }
                            catch (SocketException)
                            {
                                // ignorisemo gresku pri slanju
                            }

                            // proverimo da li je zavrsena cela partija (sve igre) i proglasimo pobednika
                            ProveriIKompletirajIgruAkoGotova();
                        }
                    }
                    else if (izabranaIgra == "as" && igrac.Asoc != null)
                    {
                        if (igrac.Igre == null || !igrac.Igre.Contains("as"))
                        {
                            Log($"Igrac {igrac.Nadimak} je poslao komandu as, ali se nije prijavio za igru asocijacije.");
                            continue;
                        }
                        string msg = message.Trim().ToUpper();

                        // --- Otvaranje polja ---
                        if (msg.Length == 2 && char.IsLetter(msg[0]) && char.IsDigit(msg[1]))
                        {
                            int kolona = msg[0] - 'A';
                            int red = int.Parse(msg[1].ToString()) - 1;

                            if (kolona < 0 || kolona >= igrac.Asoc.BrojKolona ||
                                red < 0 || red >= igrac.Asoc.BrojPoljaPoKoloni)
                            {
                                client.Send(Encoding.UTF8.GetBytes("Nevazce polje!"));
                                return;
                            }

                            string vrednost = igrac.Asoc.OtvoriPolje(kolona, red);
                            string stanje = igrac.Asoc.PrikaziAsocijaciju();
                            client.Send(Encoding.UTF8.GetBytes($"Otvoreno polje {msg}: {vrednost}\n{stanje}"));
                        }
                        // --- Pogadjanje kolone ---
                        else if (msg.Contains(":") && msg[0] >= 'A' && msg[0] < 'A' + igrac.Asoc.BrojKolona)
                        {
                            int kolona = msg[0] - 'A';
                            string odgovor = msg.Substring(2).Trim();

                            while (igrac.poeni.Count <= 2)
                                igrac.poeni.Add(0);

                            if (igrac.Asoc.ProveriKolonu(kolona, odgovor))
                            {
                                int poeni = igrac.Asoc.PoeniZaKolonu(kolona);
                                if (igrac.poeni[2] == -1)
                                    igrac.poeni[2] = 0; // prvi put igra ovu igru
                                igrac.poeni[2] += poeni;
                                igrac.PogresniPokusaji = 0;

                                string stanje = igrac.Asoc.PrikaziAsocijaciju();
                                client.Send(Encoding.UTF8.GetBytes($"TACNO! Kolona {msg[0]} resena, +{poeni} poena.\n{stanje}"));
                            }
                            else
                            {
                                igrac.PogresniPokusaji++;
                                client.Send(Encoding.UTF8.GetBytes($"NETACNO! Pokusaji zaredom: {igrac.PogresniPokusaji}/5"));
                            }
                        }
                        // --- Pogadjanje konacnog resenja ---
                        else if (msg.StartsWith("K:"))
                        {
                            string odgovor = msg.Substring(2).Trim();

                            while (igrac.poeni.Count <= 2)
                                igrac.poeni.Add(0);

                            if (igrac.Asoc.ProveriKonacno(odgovor))
                            {
                                if (igrac.poeni[2] == -1)
                                    igrac.poeni[2] = 0; // prvi put igra ovu igru
                                igrac.poeni[2] += 10;
                                igrac.PogresniPokusaji = 0;

                                client.Send(Encoding.UTF8.GetBytes($"KONACNO RESENJE TACNO! +10 poena"));
                                izabranaIgra = "";
                                ProveriIKompletirajIgruAkoGotova();
                            }
                            else
                            {
                                igrac.PogresniPokusaji++;
                                client.Send(Encoding.UTF8.GetBytes($"NETACNO konacno resenje! Pokusaji zaredom: {igrac.PogresniPokusaji}/5"));
                            }
                        }

                        // --- Kraj igre zbog 5 gresaka ---
                        if (igrac.PogresniPokusaji >= 5)
                        {
                            client.Send(Encoding.UTF8.GetBytes("Igra asocijacije zavrsena zbog 5 gresaka!"));
                            izabranaIgra = "";

                            // nakon zavrsetka igre asocijacije, ponovo ponudimo izbor igara
                            string sel2 = "Izaberite igru:\n\"an\" -> anagram\n\"po\" -> pitanja i odgovori\n\"as\"->asocijacije\n";
                            string sel3 = "NAPOMENA: igre koje niste naveli tokom prijave NECETE MOCI IGRATI.";
                            try
                            {
                                byte[] data = Encoding.UTF8.GetBytes(sel2 + "\n" + sel3 + "\n");
                                client.Send(data);
                            }
                            catch (SocketException)
                            {
                                // ignorisemo gresku pri slanju
                            }

                            // proverimo da li je zavrsena cela partija (sve igre) i proglasimo pobednika
                            ProveriIKompletirajIgruAkoGotova();
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
                                string l1 = "IGRA POCINJE!";
                                string l2 = "Izaberite igru:\n\"an\" -> anagram\n\"po\" -> pitanja i odgovori\n\"as\"->asocijacije\n";
                                string l3 = "NAPOMENA: igre koje niste naveli tokom prijave NECETE MOCI IGRATI.";

                                // posaljemo poruke svim klijentima
                                foreach (var c in _clients)
                                {
                                    try
                                    {
                                        byte[] data = Encoding.UTF8.GetBytes(l1 + "\n" + l2 + "\n" + l3 + "\n");
                                        c.Send(data);
                                    }
                                    catch (SocketException)
                                    {
                                        // ignorisemo greske slanja prema pojedinacnim klijentima
                                    }
                                }
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

        // Proverava da li su svi igraci odigrali sve igre za koje su se prijavili
        // i ako jesu, salje svima poruku o pobedniku.
        private void ProveriIKompletirajIgruAkoGotova()
        {
            if (_trening)
                return; // u trening rezimu nema pobednika

            lock (_lock)
            {
                if (_igraci.Count == 0)
                    return;

                // proveri da li su svi igraci zavrsili sve igre za koje su se prijavili
                foreach (var kvp in _igraci)
                {
                    Igrac igrac = kvp.Value;

                    // za svaku igru u listi Igre proveri da li postoji slot u listi poena
                    foreach (var igraKod in igrac.Igre)
                    {
                        int idx = -1;
                        if (string.Equals(igraKod, "an", StringComparison.OrdinalIgnoreCase)) idx = 0;
                        else if (string.Equals(igraKod, "po", StringComparison.OrdinalIgnoreCase)) idx = 1;
                        else if (string.Equals(igraKod, "as", StringComparison.OrdinalIgnoreCase)) idx = 2;

                        if (idx < 0)
                            continue;

                        if (igrac.poeni.Count <= idx)
                            return; // igrac jos nema slot za ovu igru -> partija jos nije gotova

                        // -1 znaci da igra jos nije odigrana; 0 je validan rezultat
                        if (igrac.poeni[idx] == -1)
                            return; // igrac jos nije odigrao ovu prijavljenu igru
                    }
                }

                // svi igraci imaju poene slotove za sve prijavljene igre -> racunamo pobednika
                Igrac pobednik = null;
                int maxPoena = int.MinValue;

                foreach (var kvp in _igraci)
                {
                    Igrac igrac = kvp.Value;
                    int ukupno = 0;
                    if (igrac.poeni != null)
                    {
                        foreach (int p in igrac.poeni)
                            ukupno += p;
                    }

                    if (ukupno > maxPoena)
                    {
                        maxPoena = ukupno;
                        pobednik = igrac;
                    }
                }

                if (pobednik == null)
                    return;

                string poruka = "KRAJ IGRE! Pobednik je " + pobednik.Nadimak + " sa " + maxPoena + " poena.";
                byte[] dataBytes = Encoding.UTF8.GetBytes(poruka);

                foreach (var c in _clients)
                {
                    try
                    {
                        c.Send(dataBytes);
                    }
                    catch (SocketException)
                    {
                        // ignorisemo greske slanja
                    }
                }
            }
        }
    }
}
