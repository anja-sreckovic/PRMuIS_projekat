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
        public bool Kvisko { get; set; }

        public Igrac(int ID, string nadimak,int broj_Igara)
        {
            this.ID_igraca = ID;
            this.Nadimak = nadimak;
            poeni = new List<int>(new int[broj_Igara]);
            Kvisko = false;
        }
    }
}
