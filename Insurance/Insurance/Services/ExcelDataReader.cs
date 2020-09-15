using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using VirtualCredit;
using VirtualCredit.Models;
using VirtualCredit.Services;
using System.Linq;
using NPOI.OpenXmlFormats.Dml;

namespace Insurance.Services
{
    public class ExcelDataReader
    {
        const string DataFolder = "";
        string CompanyName = string.Empty;
        string CompanyDir = string.Empty;
        string ExcelRootFolder = string.Empty;
        string Plan = string.Empty;
        UserInfoModel Account;
        List<string> dirsContainExcel = new List<string>();
        int Year = 1970;
        DateTime start = new DateTime();
        DateTime end = new DateTime();

        public ExcelDataReader(string companyName, int year, string plan)
        {
            CompanyName = companyName;
            //Account = account;
            Year = year;
            Plan = plan;
            start = new DateTime(Year, 6, 1);
            end = new DateTime(Year + 1, 5, 31, 23, 59, 59);
            ExcelRootFolder = Path.Combine(Utility.Instance.WebRootFolder, "Excel");
            Initialize();
        }

        public ExcelDataReader(DirectoryInfo companyDir, int year, string plan)
        {
            CompanyName = companyDir.Name;
            //Account = account;
            Year = year;
            Plan = plan;
            start = new DateTime(Year, 6, 1);
            end = new DateTime(Year + 1, 5, 31, 23, 59, 59);
            CompanyDir = companyDir.FullName;
            ExcelRootFolder = Path.Combine(Utility.Instance.WebRootFolder, "Excel", "历年归档", year.ToString());
            Initialize();
        }

        private void Initialize()
        {
            if (string.IsNullOrEmpty(CompanyDir))
            {
                foreach (var dir in Directory.GetDirectories(ExcelRootFolder, "*", SearchOption.AllDirectories))
                {
                    if (!DateTime.TryParse(dir, out DateTime dateTime))
                    {
                        if (new DirectoryInfo(dir).Name == CompanyName)
                        {
                            CompanyDir = dir;
                            break;
                        }
                    }
                } //获取公司文件夹路径
            }

            string searchPattern = "*";
            if (!string.IsNullOrEmpty(Plan))
            {
                searchPattern = Plan;
            }
            dirsContainExcel = Directory.GetDirectories(CompanyDir, searchPattern, SearchOption.AllDirectories).ToList(); //该公司及其子公司所有指定方案的目录

        }

        /// <summary>
        /// 获取保单方案下的保单人次
        /// </summary>
        /// <returns></returns>
        public int GetEmployeeNumber()
        {
            int result = 0;
            string companyName = string.Empty;
            dirsContainExcel.ForEach(planDir =>
            {
                var di = new DirectoryInfo(planDir);
                if (di.Exists)
                {
                    foreach (string monthDir in Directory.GetDirectories(planDir))
                    {
                        DirectoryInfo info = new DirectoryInfo(monthDir);
                        if (!DateTime.TryParse(info.Name, out DateTime dateTime)) //如果不是月份文件夹
                        {
                            continue;
                        }
                        if (!(dateTime >= start && dateTime <= end))
                        {
                            continue;
                        }
                        companyName = di.Parent.Name;
                        string summaryExcel = Path.Combine(planDir, companyName + ".xls");
                        if (!File.Exists(summaryExcel)) continue;
                        ExcelTool et = new ExcelTool(summaryExcel, "Sheet1");
                        result += et.GetEmployeeNumber();
                    }
                }
            });
            return result;
        }
        /// <summary>
        /// 获取保单方案下的当前在保人数
        /// </summary>
        /// <returns></returns>
        public int GetCurrentEmployeeNumber()
        {
            int result = 0;
            string planDir = Path.Combine(CompanyDir, Plan);
            string summaryFile = Path.Combine(planDir, CompanyName + ".xls");
            using(ExcelTool et = new ExcelTool(summaryFile, "Sheet1"))
            {
                result = et.GetEmployeeNumber();
            }
            return result;
        }

        /// <summary>
        /// 已赔付金额
        /// </summary>
        /// <returns></returns>
        public double GetPaidCost()
        {
            double result = 0;
            dirsContainExcel.ForEach(planDir =>
            {
                var di = new DirectoryInfo(planDir);
                var txt = di.GetFiles().Where(_ => _.Extension.Contains("txt"));
                if (txt != null && txt.Count() > 0)
                {
                    var txtName = txt.FirstOrDefault().Name;
                    result += Convert.ToDouble(txtName.Split("_")[1].Replace(".txt", string.Empty));
                }
            });
            return Math.Round(result, 2);
        }

        public double GetTotalCost()
        {
            double result = 0;
            dirsContainExcel.ForEach(x =>
            {
                var di = new DirectoryInfo(x);
                string excel = Path.Combine(x, di.Parent.Name + ".xls");
                if (di.Exists)
                {
                    foreach (string dir in Directory.GetDirectories(x))
                    {
                        DirectoryInfo info = new DirectoryInfo(dir);
                        if (!DateTime.TryParse(info.Name, out DateTime dateTime)) //如果不是月份文件夹
                        {
                            continue;
                        }
                        if (!(dateTime >= start && dateTime <= end))
                        {
                            continue;
                        }
                        foreach (FileInfo file in info.GetFiles())
                        {
                            string[] excelinfo = file.Name.Split('@');
                            result += Convert.ToDouble(excelinfo[1]);
                        }
                    }
                }
            });
            return Math.Round(result, 2);
        }


        /// <summary>
        /// 当年6月1号至次年5月31日的客户已结算金额
        /// </summary>
        /// <returns></returns>
        public double GetCustomerAlreadyPaid()
        {
            double result = 0;
            dirsContainExcel.ForEach(planDir =>
            {
                var di = new DirectoryInfo(planDir);
                string excel = Path.Combine(planDir, di.Name + ".xls");
                if (di.Exists)
                {
                    foreach (string monthDir in Directory.GetDirectories(planDir))
                    {
                        DirectoryInfo info = new DirectoryInfo(monthDir);
                        if (!DateTime.TryParse(info.Name, out DateTime dateTime)) //如果不是月份文件夹
                        {
                            continue;
                        }
                        if (!(dateTime >= start && dateTime <= end))
                        {
                            continue;
                        }
                        foreach (FileInfo file in info.GetFiles())
                        {
                            string[] excelinfo = file.Name.Split('@');
                            result += Convert.ToDouble(excelinfo[6]);
                        }
                    }
                }
            });
            return Math.Round(result, 2);
        }
    }
}
