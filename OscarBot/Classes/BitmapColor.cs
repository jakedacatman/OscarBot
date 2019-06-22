using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OscarBot.Classes
{
    // 'BitmapColor' overrides Object.Equals(object o) but does not override Object.GetHashCode()
    #pragma warning disable CS0659
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
        public override bool Equals(object obj)
        {
            if (!(obj is BitmapColor clr)) return false;
            return clr.R == R && clr.G == G && clr.B == B && clr.A == A;
        }
    }
    #pragma warning restore CS0659
}
