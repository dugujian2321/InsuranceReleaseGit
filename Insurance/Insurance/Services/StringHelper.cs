using System.Text.RegularExpressions;

namespace VirtualCredit.Services
{
    public class StringHelper
    {
        public static string CStringToHtmlStr(string str)
        {
            str = str.Replace("<div>", "\n");
            str = str.Replace("<br />", "\n");
            str = str.Replace("</div>", string.Empty);
            str = str.Replace("&nbsp", " ");
            RemoveBr(ref str);
            RemoveHyperLink(ref str);
            RemoveSpan(ref str);
            RemoveImg(ref str);
            RemoveFont(ref str);
            RemoveHeader(ref str);
            return str;
        }

        private static string RemoveHeader(ref string str)
        {
            Regex regex = new Regex(@"<h.*?>");
            MatchCollection res = regex.Matches(str);
            if (res != null && res.Count > 0)
            {
                foreach (Match item in res)
                {
                    str = str.Replace(item.Value, string.Empty);
                }
                str = str.Replace("</h1>", string.Empty);
                str = str.Replace("</h2>", string.Empty);
                str = str.Replace("</h3>", string.Empty);
                str = str.Replace("</h4>", string.Empty);
            }
            return str;
        }

        public static string RemoveHyperLink(ref string str)
        {
            Regex regex = new Regex(@"<a.*?>");
            MatchCollection res = regex.Matches(str);
            if(res!=null && res.Count > 0)
            {
                foreach (Match item in res)
                {
                    str = str.Replace(item.Value, string.Empty);            
                }
                str = str.Replace("</a>", string.Empty);
            }           
            return str;
        }

        public static string RemoveSpan(ref string str)
        {
            Regex regex = new Regex(@"<span.*?>");
            MatchCollection res = regex.Matches(str);
            if (res != null && res.Count > 0)
            {
                foreach (Match item in res)
                {
                    str = str.Replace(item.Value, string.Empty);
                }
                str = str.Replace("</span>", string.Empty);
            }
            return str;
        }

        public static string RemoveImg(ref string str)
        {
            Regex regex = new Regex(@"<img.*?>");
            MatchCollection res = regex.Matches(str);
            if (res != null && res.Count > 0)
            {
                foreach (Match item in res)
                {
                    str = str.Replace(item.Value, string.Empty);
                }
            }
            return str;
        }

        public static string RemoveBr(ref string str)
        {
            Regex regex = new Regex(@"<br.*?>");
            MatchCollection res = regex.Matches(str);
            if (res != null && res.Count > 0)
            {
                foreach (Match item in res)
                {
                    str = str.Replace(item.Value, "\n");
                }
                    str = str.Replace("</br>", string.Empty);
            }
            return str;
        }

        public static string RemoveFont(ref string str)
        {
            Regex regex = new Regex(@"<font.*?>");
            MatchCollection res = regex.Matches(str);
            if (res != null && res.Count > 0)
            {
                foreach (Match item in res)
                {
                    str = str.Replace(item.Value, string.Empty);
                }
                str = str.Replace("</font>", string.Empty);
            }
            return str;
        }
    }
}
