using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
            myOwn.ExcelRoot = Path.Combine(myOwn.WebRootFolder, "Excel");

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
        public static int DailyTotalHC;
        public static double DailyTotalCost;
        public static int DailyAdd;
        public static int DailySub;


        static DateTime lastestDate = DateTime.Now.Date;
        public static void DailyUpdate()
        {
            while (true)
            {
                DateTime now = DateTime.Now.Date;
                if (now > lastestDate)
                {
                    if (UpdateDailyData()) lastestDate = now;
                }
                else
                {
                    Thread.Sleep(60 * 1000);
                }
            }
        }

        private static bool UpdateDailyData()
        {
            try
            {
                string excelDir = Instance.ExcelRoot;
                string adminDir = Path.Combine(excelDir, "管理员");

                List<string> companyDirList = new List<string>();

                foreach (string comp in Directory.GetDirectories(adminDir, "*", SearchOption.AllDirectories))
                {
                    DirectoryInfo di = new DirectoryInfo(comp);
                    if (!VC_ControllerBase.Plans.Contains(di.Name) && !DateTime.TryParse(di.Name, out DateTime dt)) companyDirList.Add(comp);
                }
                DataTable todayDataTable = new DataTable();
                todayDataTable.Columns.AddRange(new DataColumn[] {
                    new DataColumn("YMDDate",typeof(DateTime)),
                    new DataColumn("Company",typeof(string)),
                    new DataColumn("HeadCount",typeof(int)),
                    new DataColumn("DailyPrice",typeof(decimal)),
                    new DataColumn("Product",typeof(string)),
                });
                foreach (string company in companyDirList)
                {
                    foreach (string plan in VC_ControllerBase.Plans)
                    {
                        string planDir = Path.Combine(company, plan);
                        DirectoryInfo compInfo = new DirectoryInfo(company);
                        string summary = Path.Combine(planDir, compInfo.Name + ".xls");
                        if (!System.IO.File.Exists(summary)) continue;
                        UserInfoModel account = null;
                        var table = DatabaseService.SelectMultiPropFromTable("UserInfo", new string[] { "CompanyName", "_Plan" }, new string[] { compInfo.Name, plan });
                        if (table != null && table.Rows.Count > 0)
                        {
                            account = DatabaseService.SelectUser(table.Rows[0]["userName"].ToString());
                        }
                        else
                            continue;
                        using (ExcelTool et = new ExcelTool(Path.Combine(planDir, compInfo.Name + ".xls"), "Sheet1"))
                        {
                            DateTime dt = lastestDate;
                            string compName = compInfo.Name;
                            int headcount = et.GetEmployeeNumber();
                            double dailyPrice = headcount * 0.01 * ( Math.Floor(100 * (account.UnitPrice / DateTime.DaysInMonth(lastestDate.Year, lastestDate.Month))) + 1);
                            DataRow dr = todayDataTable.NewRow();
                            dr["YMDDate"] = dt.Date;
                            dr["Company"] = compName;
                            dr["HeadCount"] = headcount;
                            dr["DailyPrice"] = dailyPrice;
                            dr["Product"] = plan;
                            todayDataTable.Rows.Add(dr);
                        }
                    }
                }
                    DatabaseService.BulkInsert("DailyDetailData", todayDataTable);
                return true;
            }
            catch
            {
                return false;
            }


        }
    }
}
