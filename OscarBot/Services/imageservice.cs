using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

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
            if (type.ToLower() == "jpg") type = "jpeg";

            var s = (ImageFormat)typeof(ImageFormat).GetProperties().Where(x => x.Name.ToLower() == type.ToLower()).First().GetValue(ImageFormat.Bmp, null); //kinda hacky but beats hardcoding

            ImageCodecInfo codecBlack = EncoderOf(s) ?? ImageCodecInfo.GetImageEncoders().Where(x => x.FormatID == ImageFormat.Jpeg.Guid).First();

            EncoderParameters eParams = new EncoderParameters(1);
            eParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            string e = type.ToLower() == "jpeg" ? "jpg" : s.ToString().ToLower();
            var path = $"temp_{new Random().Next()}.{e}";

            var bData = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, b.PixelFormat);
            var amnt = Math.Abs(bData.Stride) * bData.Height;
            var vals = new byte[amnt];
            var p = bData.Scan0;

            Marshal.Copy(p, vals, 0, amnt);

            Dictionary<byte, byte> randColors = new Dictionary<byte, byte>();

            foreach (byte by in vals)
                if (!randColors.Keys.Where(x => x == by).Any())
                    randColors.Add(by, (byte)_random.Next(256));
                else
                {
                    int lower = by - tolerance;
                    int upper = by + tolerance;

                    if (lower < byte.MinValue) lower = byte.MinValue;
                    if (upper > byte.MaxValue) upper = byte.MaxValue;

                    var range = Enumerable.Range(lower, upper);
                    if (!randColors.Keys.Where(x => range.Contains(x)).Any())
                    {
                        var rand = (byte)_random.Next(256);
                        foreach (byte byt in range)
                            randColors.Add(byt, rand);
                    }
                }

            byte[] newVals = new byte[vals.Length];

            for (int i = 0; i < vals.Length; i++)
                newVals[i] = randColors[vals[i]];

            Marshal.Copy(newVals, 0, p, amnt);

            b.UnlockBits(bData);
            b.Save(path, codecBlack, eParams);
            b.Dispose();

            return path;
        }
    }
}
