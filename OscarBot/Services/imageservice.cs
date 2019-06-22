using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Runtime.InteropServices;
using OscarBot.Classes;
using System.IO;

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

            var formats = typeof(ImageFormat).GetProperties().Where(x => x.Name.ToLower() == type.ToLower());
            var s = formats.Any() ? (ImageFormat)formats.First().GetValue(ImageFormat.Bmp, null) : ImageFormat.Jpeg;  //kinda hacky but beats hardcoding

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

        public string RandomColor(Bitmap b, string type, long quality)
        {
            List<IDisposable> toDispose = new List<IDisposable>
            {
                b
            };

            if (type.ToLower() == "jpg") type = "jpeg";

            var formats = typeof(ImageFormat).GetProperties().Where(x => x.Name.ToLower() == type.ToLower());
            var s = formats.Any() ? (ImageFormat)formats.First().GetValue(ImageFormat.Bmp, null) : ImageFormat.Jpeg;  //kinda hacky but beats hardcoding

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
                if (i + 2 >= vals.Length) break;

                byte rVal = vals[i];
                byte gVal = vals[i + 1];
                byte bVal = vals[i + 2];

                BitmapColor pixel = new BitmapColor(rVal, gVal, bVal);
                BitmapColor mappedClr;

                if (!mapping.ContainsKey(pixel))
                {
                    var newClr = new BitmapColor((byte)_random.Next(256), (byte)_random.Next(256), (byte)_random.Next(256));
                    mapping.Add(pixel, newClr);
                    mappedClr = newClr;
                }
                else
                    mappedClr = mapping[pixel];

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

        public string Invert(Bitmap b, string type, long quality)
        {
            List<IDisposable> toDispose = new List<IDisposable>
            {
                b
            };

            if (type.ToLower() == "jpg") type = "jpeg";

            var formats = typeof(ImageFormat).GetProperties().Where(x => x.Name.ToLower() == type.ToLower());
            var s = formats.Any() ? (ImageFormat)formats.First().GetValue(ImageFormat.Bmp, null) : ImageFormat.Jpeg;  //kinda hacky but beats hardcoding

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
                if (i + 2 >= vals.Length) break;

                byte rVal = vals[i];
                byte gVal = vals[i + 1];
                byte bVal = vals[i + 2];

                BitmapColor pixel = new BitmapColor(rVal, gVal, bVal);
                BitmapColor mappedClr;

                if (!mapping.ContainsKey(pixel))
                {
                    var newClr = new BitmapColor((byte)(255 - rVal), (byte)(255 - gVal), (byte)(255 - bVal));
                    mapping.Add(pixel, newClr);
                    mappedClr = newClr;
                }
                else
                    mappedClr = mapping[pixel];

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

        private IEnumerable<byte> GetRange(int start, int end)
        {
            for (byte i = (byte)start; i <= (byte)end; i++)
                yield return i;
        }

        private IEnumerable<BitmapColor> GetColorsFromRanges(IEnumerable<byte> rVals, IEnumerable<byte> gVals, IEnumerable<byte> bVals)
        {
            return
                from r in rVals
                from g in gVals
                from b in bVals
                select new BitmapColor(r, g, b);
        }

        public async Task<Bitmap> GetBitmapFromUrlAsync(string url)
        {
            using (var c = new WebClient())
            {
                byte[] s = await c.DownloadDataTaskAsync(url);
                Stream fs = new MemoryStream(s);
                Image i = Image.FromStream(fs);
                fs.Close();
                return new Bitmap(i);
            }
        }
    }
}