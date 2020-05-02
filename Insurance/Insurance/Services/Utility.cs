using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using VirtualCredit.Models;

namespace VirtualCredit.Services
{
    public class Utility
    {
        private static UtilitiesModel _instance;
        public static UtilitiesModel Instance { get { return _instance; } }
        public static List<ReaderWriterLockerWithName> LockerList { get; set; }
        /// <summary>
        /// 将配置项的值赋值给属性
        /// </summary>
        /// <param name="configuration"></param>
        public void Initial(IConfiguration configuration, IHostingEnvironment environment)
        {
            UtilitiesModel myOwn = new UtilitiesModel();
            //注意：可以使用冒号来获取内层的配置项
            DatabaseService.ConnStr = configuration["ConnectionStrings:Insurance"];
            myOwn.TemplateFolder = configuration["TemplatesFolder"];
            myOwn.WebRootFolder = environment.WebRootPath;
            _instance = myOwn;
            LockerList = new List<ReaderWriterLockerWithName>();
        }

        public static ReaderWriterLockerWithName GetCompanyLocker(string companyName)
        {
            var locks = LockerList.Where(_ => _.LockerCompany == companyName);
            foreach (ReaderWriterLockerWithName item in locks)
            {
                if (item != null)
                {
                    return item;
                }
            }
            return null;
        }

        public static string ArrayToString(string[] strs, int start, int end, string connectWith)
        {
            string result = string.Empty;
            for (int i = start; i <= end; i++)
            {
                result += strs[i] + connectWith;
            }
            return result;
        }
    }
}
