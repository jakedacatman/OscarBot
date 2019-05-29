using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

namespace OscarBot.Services
{
    public class ImageService
    {
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

        ImageCodecInfo EncoderOf(ImageFormat f)
        {
            foreach (var s in ImageCodecInfo.GetImageEncoders())
            {
                if (s.FormatID == f.Guid)
                    return s;
            }

            return null;
        }
    }
}
