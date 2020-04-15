using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NPOI.SS.UserModel;

namespace VirtualCredit
{
    public static class SessionExtensions
    {
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);

            return value == null ? default(T) :
                JsonConvert.DeserializeObject<T>(value);
        }
    }

    public static class RequestExtensions
    {
        public static bool IsAjaxRequest(this HttpRequest request)
        {
            bool result = false;
            var xreq = request.Headers.ContainsKey("x-requested-with");
            if (xreq)
            {
                result = request.Headers["x-requested-with"] == "XMLHttpRequest";
            }
            return result;
        }
    }
}

namespace Insurance
{
    public static class NOPIExtension
    {
        public static int GetLastRow(this ISheet sheet)
        {
            int lastRow = sheet.LastRowNum;
            bool found = false;
            while (!found)
            {
                IRow row = sheet.GetRow(lastRow);
                if (string.IsNullOrEmpty(row.Cells[0].ToString()))
                {
                    lastRow--;
                }
                else
                {
                    found = true;
                }
            }
            return lastRow;
        }
    }
}