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
        List<string> directDirsContainExcel = new List<string>();
        List<string> dirsContainExcel = new List<string>();
        int Year = 1970;
        DateTime start = new DateTime();
        DateTime end = new DateTime();
        static string[] CachedDirectories;

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

        void UpdateCachedDirs()
        {
            CachedDirectories = Directory.GetDirectories(ExcelRootFolder, "*", SearchOption.AllDirectories);
        }

        private void Initialize()
        {
            if (CachedDirectories == null)
            {
                UpdateCachedDirs();
            }
            if (string.IsNullOrEmpty(CompanyDir))
            {
                foreach (var dir in CachedDirectories)
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
                if (string.IsNullOrEmpty(CompanyDir))
                {
                    UpdateCachedDirs();
                    foreach (var dir in CachedDirectories)
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
            }

            string searchPattern = "*";
            if (!string.IsNullOrEmpty(Plan))
            {
                searchPattern = Plan;
            }
            dirsContainExcel = Directory.GetDirectories(CompanyDir, searchPattern, SearchOption.AllDirectories).ToList(); //该公司及其子公司所有指定方案的目录
            directDirsContainExcel = Directory.GetDirectories(CompanyDir, searchPattern, SearchOption.TopDirectoryOnly).ToList(); //该公司所有指定方案的目录
        }

        /// <summary>
        /// 获取保单方案下的保单人次
        /// </summary>
        /// <returns></returns>
        public int GetEmployeeNumber()
        {
            int result = 0;
            string companyName = string.Empty;
            string summaryExcel = string.Empty;
            dirsContainExcel.ForEach(planDir =>
            {
                var di = new DirectoryInfo(planDir);
                if (di.Exists)
                {
                    companyName = di.Parent.Name;
                    summaryExcel = Path.Combine(planDir, companyName + ".xls");
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

                        if (!File.Exists(summaryExcel)) continue;
                    }
                    ExcelTool et = new ExcelTool(summaryExcel, "Sheet1");
                    result += et.GetEmployeeNumber();
                }
            });
            return result;
        }
        /// <summary>
        /// 获取保单方案下的当前在保人数,包含后代公司
        /// </summary>
        /// <returns></returns>
        public int GetCurrentEmployeeNumber()
        {
            int result = 0;
            string[] planDirs = Directory.GetDirectories(CompanyDir, Plan, SearchOption.AllDirectories);
            string compName = string.Empty;
            foreach (var planDir in planDirs)
            {
                DirectoryInfo di = new DirectoryInfo(planDir);
                compName = di.Parent.Name;
                string summaryFile = Path.Combine(planDir, compName + ".xls");
                using (ExcelTool et = new ExcelTool(summaryFile, "Sheet1"))
                {
                    result += et.GetEmployeeNumber();
                }
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

        public double GetTotalCostExcludeChildren()
        {
            double result = 0;
            directDirsContainExcel.ForEach(x =>
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
            return result;
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
                        DateTime thisMonth = DateTime.Parse(info.Name);
                        int daysInMonth = DateTime.DaysInMonth(thisMonth.Year, thisMonth.Month);
                        string companyName = info.Parent.Parent.Name;
                        foreach (FileInfo file in info.GetFiles())
                        {
                            if (!file.Extension.Contains("xls")) continue;
                            string[] excelinfo = file.Name.Split('@');
                            result += Convert.ToDouble(excelinfo[1]);
                        }
                    }
                }
            });
            return Math.Round(result, 2);
        }
        public double GetTotalCost(UserInfoModel currUser)
        {
            double result = 0;

            //性能优化,todo: 解除注释：
            var displayMonthPriceAcc = VC_ControllerBase.AccountTreeRoot.Children.Where(x => x.Descendant.Any(y => y.Data.CompanyName == CompanyName && y.Data._Plan == Plan)).FirstOrDefault();
            if (displayMonthPriceAcc == null)
            {
                displayMonthPriceAcc = VC_ControllerBase.AccountTreeRoot.Children.Where(y => y.Data.CompanyName == CompanyName && y.Data._Plan == Plan).FirstOrDefault();
            }
            //var displayMonthPriceAcc = currUser.ChildAccounts.Where(x => x.SpringAccounts.Any(y => y.CompanyName == CompanyName && y._Plan == Plan)).FirstOrDefault();
            //if (displayMonthPriceAcc == null)
            //{
            //    displayMonthPriceAcc = currUser.ChildAccounts.Where(y => y.CompanyName == CompanyName && y._Plan == Plan).FirstOrDefault();
            //}
            

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
                        DateTime thisMonth = DateTime.Parse(info.Name);
                        int daysInMonth = DateTime.DaysInMonth(thisMonth.Year, thisMonth.Month);
                        string companyName = info.Parent.Parent.Name;
                        var actualMonthPriceAcc = currUser.SpringAccounts.Where(xx => xx.CompanyName == companyName && xx._Plan == Plan).FirstOrDefault();
                        if (actualMonthPriceAcc == null)
                            continue;
                        double actualMonthPrice = actualMonthPriceAcc.UnitPrice;
                        foreach (FileInfo file in info.GetFiles())
                        {
                            if (!file.Extension.Contains("xls")) continue;
                            string[] excelinfo = file.Name.Split('@');
                            result += (Convert.ToDouble(excelinfo[1]) / (actualMonthPrice / daysInMonth)) * (displayMonthPriceAcc.Data.UnitPrice / daysInMonth);
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
