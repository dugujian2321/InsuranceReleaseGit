using System;
using System.Security.Cryptography;

namespace VirtualCredit
{
    public class MD5Helper
    {
        private string BackendSalt { get; set; }
        public static string FrontendSalt { get; set; }
        MD5 str = new MD5CryptoServiceProvider();

        static MD5Helper()
        {
            FrontendSalt = "Q$i%2r6A$&-pY)nD:[].uAnVfsgfdaS$hg%?*+../aLt";
        }
        public MD5Helper()
        {
            BackendSalt = "H@u*Xi(_o.O+*1^6%72?/;]-Xyz.,QFmATi..";
        }

        public string MD5Encrypt(string pwd, string salt)
        {
            byte[] tar = System.Text.Encoding.Default.GetBytes(pwd + salt);
            byte[] res = str.ComputeHash(tar);
            return BitConverter.ToString(res).Replace("-", string.Empty);
        }

        public string EncryptNTimes(string str, int iteration, string salt)
        {
            string result = str;
            for (int i = 1; i <= iteration; i++)
            {
                result = MD5Encrypt(result, salt);
            }
            return result;
        }

        public string EncryptNTimesWithBackendSalt(string str, int iteration)
        {
            string result = str;
            for (int i = 1; i <= iteration; i++)
            {
                result = MD5Encrypt(result, BackendSalt);
            }
            return result;
        }

        //public bool Verify()
        //{

        //}
    }
}
