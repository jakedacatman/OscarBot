using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OscarBot.Classes
{
    public class BitmapColor
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public BitmapColor(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
        public BitmapColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
            A = 0;
        }
    }
}
