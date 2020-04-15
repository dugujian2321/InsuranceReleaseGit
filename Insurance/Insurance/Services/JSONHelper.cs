using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace VirtualCredit.Services
{
    public class JSONHelper
    {
        JObject obj;
        StreamReader sr;
        JsonTextReader jtr;
        public JSONHelper(string file)
        {
            try
            {
                jtr = new JsonTextReader(sr);
                obj = (JObject)JToken.ReadFrom(jtr);
            }catch(Exception e)
            {
                LogServices.LogService.Log(e.Message);

            }
        }

        public string Read(string key)
        {
            return obj[key].ToString();
        }

        public void CloseHelper()
        {
            jtr.Close();
            sr.Close();
        }
    }
}
