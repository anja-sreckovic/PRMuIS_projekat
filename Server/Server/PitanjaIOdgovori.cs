using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class PitanjaIOdgovori
    {
        public string tek_pitanje { get; set; }
        private bool tacan_odgovor;
        private Dictionary<string, bool> sva_pitanja = new Dictionary<string, bool>()
        {
            { "Čovek ima tri bubrega.", false },
            { "Sunce je zvezda.", true },
            { "Zemlja je ravna.", false },
            { "Ferrari je tim sa najviše titula konstruktora u F1.", true },
            { "Pas je sisar.", true },
            { "Max Verstappen je vozio za Red Bull Racing.", true },
            { "Mesec je planeta.", false },
            { "Ptice imaju zube.", false },
            { "Voda ključa na 100C.", true },
            { "Ljudi imaju četiri pluća.", false },
            { "F1 trka traje tačno 100 krugova.", false },
            { "Elektron je pozitivan.", false },
            { "Riba diše na kopnu.", false }
        };

        private int index = 0;
        private List<string> kljucevi;

        public PitanjaIOdgovori()
        {
            kljucevi = new List<string>(sva_pitanja.Keys);
        }

        public bool SledecePitanje()
        {
            if (index >= kljucevi.Count) 
                return false;

            tek_pitanje = kljucevi[index];
            tacan_odgovor= sva_pitanja[tek_pitanje];
            index++;
            return true;
        }

        public int ObradiOdgovor(char odgovor, bool kvisko)
        {
            bool da = (odgovor == 'a' || odgovor == 'A');
            bool tacno = da == tacan_odgovor;

            if (!tacno) 
                return 0;
            else
            {
                int poeni = 4;
                if (kvisko) 
                    poeni *= 2;
                return poeni;
            }
        }
    }
}
