using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Asocijacija
    {
        private int brojKolona = 4;
        private int brojPoljaPoKoloni = 4;

        private string[,] polja;
        private bool[,] otvoreno;
        private string[] resenjaKolona;
        private string konacnoResenje;

        public bool Zavrsena { get; set; } = false;

        public int BrojKolona => brojKolona;
        public int BrojPoljaPoKoloni => brojPoljaPoKoloni;

        public Asocijacija()
        {
            polja = new string[brojKolona, brojPoljaPoKoloni];
            otvoreno = new bool[brojKolona, brojPoljaPoKoloni];
        }

        public void UcitajIzFajla(string putanja)
        {
            string[] lines = File.ReadAllLines(putanja);

            if (lines.Length < brojPoljaPoKoloni + 2)
                throw new Exception("Fajl nema dovoljno linija za polja i resenja.");

            for (int i = 0; i < brojPoljaPoKoloni; i++)
            {
                string[] values = lines[i].Split(';');
                if (values.Length != brojKolona)
                    throw new Exception($"Red {i+1} nema tacan broj kolona.");

                for (int j = 0; j < brojKolona; j++)
                {
                    polja[j, i] = values[j].Trim();
                    otvoreno[j, i] = false;
                }
            }

            string[] resenjeLine = lines[brojPoljaPoKoloni].Split(':');
            string[] kolResenja = resenjeLine[1].Split(';');
            if (kolResenja.Length != brojKolona)
                throw new Exception("Broj resenja kolona ne odgovara broju kolona.");

            resenjaKolona = new string[brojKolona];
            for (int k = 0; k < brojKolona; k++)
                resenjaKolona[k] = kolResenja[k].Trim();

            string[] konacnoLine = lines[brojPoljaPoKoloni + 1].Split(':');
            konacnoResenje = konacnoLine[1].Trim();

            Zavrsena = false;
        }

        public string OtvoriPolje(int kolona, int red)
        {
            otvoreno[kolona, red] = true;
            return polja[kolona, red];
        }

        public bool ProveriKolonu(int kolona, string odgovor)
        {
            string tacanOdgovor = resenjaKolona[kolona].Trim();
            if (string.Equals(tacanOdgovor, odgovor.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                for (int r = 0; r < brojPoljaPoKoloni; r++)
                    otvoreno[kolona, r] = true;
                return true;
            }
            return false;
        }

        public bool ProveriKonacno(string odgovor)
        {
            if (string.Equals(konacnoResenje.Trim(), odgovor.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Zavrsena = true;
                return true;
            }
            return false;
        }

        public int PoeniZaKolonu(int kolona)
        {
            int neotvorena = 0;
            for (int r = 0; r < brojPoljaPoKoloni; r++)
                if (!otvoreno[kolona, r]) neotvorena++;
            return neotvorena + 2;
        }

        private bool otvorenoSvePoljaKolone(int kolona)
        {
            for (int r = 0; r < brojPoljaPoKoloni; r++)
                if (!otvoreno[kolona, r]) return false;
            return true;
        }

        public string PrikaziAsocijaciju()
        {
            StringBuilder sb = new StringBuilder();
            for (int k = 0; k < brojKolona; k++)
            {
                for (int r = 0; r < brojPoljaPoKoloni; r++)
                {
                    string val = otvoreno[k, r] ? polja[k, r] : "???";
                    sb.AppendLine($"{(char)('A' + k)}{r + 1}: {val}");
                }
                string kolResenje = otvorenoSvePoljaKolone(k) ? resenjaKolona[k] : "???";
                sb.AppendLine($"{(char)('A' + k)}: {kolResenje}");
            }
            sb.AppendLine();
            sb.AppendLine("K:???");
            return sb.ToString();
        }
    }
}
