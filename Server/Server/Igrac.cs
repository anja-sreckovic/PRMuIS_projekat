using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Igrac
    {
        public int ID_igraca { get; }
        public string Nadimak { get; }
        public List<int> poeni;
        // lista igara za koje se igrac prijavio (vrednosti: "an", "po", "as")
        public List<string> Igre { get; }
        public AnagramIgra Anagram { get; }
        public bool Kvisko { get; set; }
        public Asocijacija Asoc { get; set; } = null;
        public int PogresniPokusaji { get; set; } = 0;

        public Igrac(int ID, string nadimak,int broj_Igara, IEnumerable<string> igre)
        {
            this.ID_igraca = ID;
            this.Nadimak = nadimak;
            // inicijalno -1 znaci da igra jos nije igrana (0 je validan broj poena)
            poeni = new List<int>();
            for (int i = 0; i < broj_Igara; i++)
                poeni.Add(-1);
            Igre = new List<string>(igre ?? new List<string>());
            Kvisko = false;
            Anagram = new AnagramIgra();
        }
    }
}
