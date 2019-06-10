using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using OscarBot.Classes;

namespace OscarBot.Services
{
    public class ImageService
    {
        private readonly Random _random;

        public ImageService(Random random)
        {
            _random = random;
        }

        public string Compress(Bitmap b, string type, long quality)
        {
            if (type.ToLower() == "jpg") type = "jpeg";

            var s = (ImageFormat)typeof(ImageFormat).GetProperties().Where(x => x.Name.ToLower() == type.ToLower()).First().GetValue(ImageFormat.Bmp, null); //kinda hacky but beats hardcoding

            ImageCodecInfo codecBlack = EncoderOf(s) ?? ImageCodecInfo.GetImageEncoders().Where(x => x.FormatID == ImageFormat.Jpeg.Guid).First();

            EncoderParameters eParams = new EncoderParameters(1);
            eParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            string e = type.ToLower() == "jpeg" ? "jpg" : s.ToString().ToLower();
            var path = $"temp_{new Random().Next()}.{e}";
            b.Save(path, codecBlack, eParams);
            b.Dispose();

            return path;
        }

        private ImageCodecInfo EncoderOf(ImageFormat f)
        {
            foreach (var s in ImageCodecInfo.GetImageEncoders())
            {
                if (s.FormatID == f.Guid)
                    return s;
            }

            return null;
        }

        public string RandomColor(Bitmap b, string type, long quality, int tolerance)
        {
            List<IDisposable> toDispose = new List<IDisposable>
            {
                b
            };

            if (type.ToLower() == "jpg") type = "jpeg";

            var s = (ImageFormat)typeof(ImageFormat).GetProperties().Where(x => x.Name.ToLower() == type.ToLower()).First().GetValue(ImageFormat.Bmp, null); //kinda hacky but beats hardcoding

            ImageCodecInfo codecBlack = EncoderOf(s) ?? ImageCodecInfo.GetImageEncoders().Where(x => x.FormatID == ImageFormat.Jpeg.Guid).First();

            EncoderParameters eParams = new EncoderParameters(1);
            eParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            string e = type.ToLower() == "jpeg" ? "jpg" : s.ToString().ToLower();
            var path = $"temp_{new Random().Next()}.{e}";

            if (b.PixelFormat != PixelFormat.Format24bppRgb)
            {
                var clone = new Bitmap(b.Width, b.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(clone))
                {
                    g.DrawImage(b, new Rectangle(0, 0, b.Width, b.Height));
                    b.Dispose();
                    b = clone;
                    toDispose.Add(clone);
                }
            }

            var bData = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, b.PixelFormat);
            var amnt = Math.Abs(bData.Stride) * bData.Height;
            var vals = new byte[amnt];
            var p = bData.Scan0;

            Marshal.Copy(p, vals, 0, amnt);

            Dictionary<BitmapColor, BitmapColor> mapping = new Dictionary<BitmapColor, BitmapColor>();

            for (int i = 0; i < vals.Length; i += 3)
            {
                byte rVal = vals[i];
                byte gVal = vals[i + 1];
                byte bVal = vals[i + 2];

                BitmapColor pixel = new BitmapColor(rVal, gVal, bVal);

                if (!mapping.ContainsKey(pixel))
                {
                    var newClr = new BitmapColor((byte)_random.Next(256), (byte)_random.Next(256), (byte)_random.Next(256));
                    mapping.Add(pixel, newClr);
                }
                else
                {
                    int lowerR = rVal - tolerance;
                    int upperR = rVal + tolerance;

                    if (lowerR < byte.MinValue) lowerR = byte.MinValue;
                    if (upperR > byte.MaxValue) upperR = byte.MaxValue;

                    int lowerG = gVal - tolerance;
                    int upperG = gVal + tolerance;

                    if (lowerG < byte.MinValue) lowerG = byte.MinValue;
                    if (upperG > byte.MaxValue) upperG = byte.MaxValue;

                    int lowerB = bVal - tolerance;
                    int upperB = bVal + tolerance;

                    if (lowerB < byte.MinValue) lowerB = byte.MinValue;
                    if (upperB > byte.MaxValue) upperB = byte.MaxValue;

                    var rangeR = Enumerable.Range(lowerR, upperR).ToArray();
                    var rangeG = Enumerable.Range(lowerG, upperG).ToArray();
                    var rangeB = Enumerable.Range(lowerB, upperB).ToArray();

                    List<BitmapColor> allowedVals = new List<BitmapColor>();

                    for (int f = 0; f < rangeR.Last(); f++)
                    {
                        var clr = new BitmapColor((byte)rangeR[i], (byte)rangeG[i], (byte)rangeB[i]);
                        allowedVals.Add(clr);
                    }
                    var newClr = new BitmapColor((byte)_random.Next(256), (byte)_random.Next(256), (byte)_random.Next(256));
                    for (int q = 0; q < allowedVals.Count; q++)
                    {
                        if (mapping.ContainsKey(allowedVals[q]))
                            mapping.Add(allowedVals[q], newClr);
                    }
                }


                var mappedClr = mapping[pixel];
                vals[i] = mappedClr.R;
                vals[i + 1] = mappedClr.G;
                vals[i + 2] = mappedClr.B;
            }

            Marshal.Copy(vals, 0, p, amnt);

            b.UnlockBits(bData);
            b.Save(path, codecBlack, eParams);
            foreach (var i in toDispose)
                i.Dispose();

            return path;
        }
    }
}
