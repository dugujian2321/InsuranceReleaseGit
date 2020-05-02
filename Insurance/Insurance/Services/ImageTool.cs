using Microsoft.AspNetCore.Http;
using System;
using System.DrawingCore;
using System.DrawingCore.Imaging;
using System.IO;

namespace VirtualCredit.Services
{
    public class ImageTool
    {
        private HttpContext _context;
        public ImageTool(HttpContext context)
        {
            _context = context;
        }

        public int ValidationResult { get; set; }
        private string GetValidationFormula()
        {
            Random rnd = new Random();
            int a = rnd.Next(1, 100);
            int b = rnd.Next(1, 100);
            string met = string.Empty;
            int metIndex = rnd.Next(1, 3);
            switch (metIndex)
            {
                case 1:
                    met = "＋";
                    ValidationResult = a + b;
                    break;
                case 2:
                    met = "－";
                    ValidationResult = Math.Abs(a - b);
                    break;
                case 3:
                    met = "×";
                    ValidationResult = a * b;
                    break;
                case 4:
                    met = "÷";
                    break;
            }
            _context.Session.Set<int>("ValidationResult", ValidationResult);
            Bitmap bitmap = new Bitmap(120, 20);
            if (a > b)
            {
                return a + "  " + met + "  " + b + "＝";
            }
            else
            {
                return b + "  " + met + "  " + a + "＝";
            }

        }

        public string DrawValidationImg()
        {
            string formula = GetValidationFormula();
            string result = string.Empty;
            Random random = new Random();
            string[] fonts = { "Verdana", "Microsoft Sans Serif", "Comic Sans MS", "Arial", "宋体" };
            Color[] c = { Color.Black, Color.Red, Color.DarkBlue, Color.Green, Color.Orange, Color.Brown, Color.DarkCyan, Color.Purple };
            using (Bitmap bitmap = new Bitmap(120, 20))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);//背景设为白色  
                    int cindex = random.Next(0, 8);//随机颜色索引值  
                    int findex = random.Next(0, 5);//随机字体索引值  
                    Font f = new Font(fonts[findex], 10, FontStyle.Bold);//字体  
                    Brush b = new SolidBrush(c[cindex]);//颜色  
                    graphics.DrawString(formula, f, b, 5, 1);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, ImageFormat.Jpeg);
                        result = "data:image/jpeg;base64," + Convert.ToBase64String(ms.GetBuffer());
                    }
                }
            }
            return result;
        }

        private static string GetImgFileExtension(string bx)
        {
            foreach (var item in Enum.GetValues(typeof(ImageExtension)))
            {
                if (bx == ((int)item).ToString())
                {
                    return item.ToString();
                }
            }
            return string.Empty;
        }

        public static ImageExtension GetFileFormat(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            foreach (var item in Enum.GetValues(typeof(ImageExtension)))
            {
                if (fi.Extension == "." + item.ToString())
                {
                    return (ImageExtension)item;
                }
            }
            return ImageExtension.NUL;
        }

        public static string GetFileFormat(byte[] formFile)
        {
            string bx = string.Empty;
            byte buffer;
            buffer = formFile[0];
            bx = buffer.ToString();
            buffer = formFile[1];
            bx += buffer.ToString();
            return GetImgFileExtension(bx);
        }

        public static bool IsImgFile(string filePath)
        {
            bool result = false;
            try
            {
                byte[] imgBuffer;
                imgBuffer = ConvertImgToByte(filePath);
                if (GetFileFormat(imgBuffer) != string.Empty)
                {
                    result = true;
                }
            }
            catch
            {

            }
            return result;
        }

        public static byte[] ConvertImgToByte(string filePath)
        {
            byte[] imgBuffer;
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    imgBuffer = ms.GetBuffer();
                }
            }
            return imgBuffer;
        }

        public static string ConvertImgToBase64(string filePath)
        {
            string result = string.Empty;
            if (!(File.Exists(filePath) && IsImgFile(filePath)))
            {
                return result;
            }
            result = Convert.ToBase64String(ConvertImgToByte(filePath));

            return result;
        }
    }
}
