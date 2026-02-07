using System;

namespace Server
{
    public class AnagramIgra
    {
        private string _original;
        private string _predlog;

        public string Original { get { return _original; } }
        public string Predlog { get { return _predlog; } }

        public void UcitajRec(string rec)
        {
            _original = rec.Trim();
        }

        public void PostaviPredlog(string predlog)
        {
            _predlog = (predlog ?? string.Empty).Trim();
        }

        public int ProveriAnagram()
        {
            if (string.IsNullOrEmpty(_original) || string.IsNullOrEmpty(_predlog))
                return 0;

            if (!IstaSlovaIstiBrojPuta(_original, _predlog))
                return 0;

            int duzina = _original.Replace(" ", string.Empty).Length;
            return duzina;
        }

        private static bool IstaSlovaIstiBrojPuta(string a, string b)
        {
            a = a.Replace(" ", string.Empty).ToLowerInvariant();
            b = b.Replace(" ", string.Empty).ToLowerInvariant();

            var cntA = new int[256];
            var cntB = new int[256];

            for (int i = 0; i < a.Length; i++)
            {
                cntA[(byte)a[i]]++;
            }

            for (int i = 0; i < b.Length; i++)
            {
                cntB[(byte)b[i]]++;
            }

            // proveravamo da se svako slovo iz predloga (b)
            // ne pojavljuje vise puta nego u originalu (a)
            for (int i = 0; i < 256; i++)
            {
                if (cntB[i] > cntA[i])
                    return false;
            }

            return true;
        }
    }
}
