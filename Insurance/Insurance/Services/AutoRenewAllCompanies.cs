using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.IO;
using System.Threading;
using VirtualCredit;
using VirtualCredit.LogServices;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace Insurance.Services
{
    public class AutoRenewAllCompanies
    {
        private static bool shouldRenew = false;
        string rootpath = Utility.Instance.WebRootFolder;
        static DateTime nextEndDate;
        public AutoRenewAllCompanies()
        {
            nextEndDate = DateTime.Now;
        }

        private void RenewAllCompanies()
        {
            string excelDir = Path.Combine(rootpath, "Excel", "管理员");
            foreach (string company in Directory.GetDirectories(excelDir, "*", SearchOption.AllDirectories))
            {
                DirectoryInfo di = new DirectoryInfo(company);
                if (!VC_ControllerBase.Plans.Contains(di.Name)) continue;
                string plan = di.Name;
                string companyName = di.Parent.Name;
                string summary = Path.Combine(company, companyName + ".xls");
                string summary_bk_file = Path.Combine(company, companyName + "_" + DateTime.Now.AddMonths(-1).ToString("yyyy-MM") + "_bk.xls");
                if (File.Exists(summary_bk_file))
                {
                    continue;
                }
                File.Copy(summary, summary_bk_file, true);
                ExcelTool et = new ExcelTool(summary, "Sheet1");
                DateTime currDate;

                if (DateTime.TryParse(et.GetCurrentDate(), out currDate))
                {
                    if (currDate.Date.Year == nextEndDate.AddDays(1).Date.Year && currDate.Date.Month == nextEndDate.AddDays(1).Date.Month)
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                if (et.GetEmployeeNumber() <= 0)
                {
                    continue;
                }
                
                DataTable tbl_summary = et.ExcelToDataTable("Sheet1", true);
                foreach (DataRow row in tbl_summary.Rows)
                {
                    DateTime dt = DateTime.Now.Date;
                    row["生效日期"] = new DateTime(dt.Year, dt.Month, 1).ToString("yyyy-MM-dd");
                }
                et.DatatableToExcel(tbl_summary);
                GenerateNewExcelForRenewAsync(company, plan, true);
            }
        }

        private bool GenerateNewExcelForRenewAsync(string companyFolder, string plan, bool auto = false)
        {
            string company = new DirectoryInfo(companyFolder).Parent.Name;
            LogService.Log($"{company}开始自动流转");
            string summary_bk = string.Empty;
            try
            {
                UserInfoModel currUser = InsuranceDatabaseService.SelectUserByCompanyAndPlan(company, plan);
                if (currUser == null)
                {
                    return false;
                }
                string companyDir = companyFolder;
                string summaryPath = Path.Combine(companyDir, company + ".xls");

                string monDir = Path.Combine(companyDir, DateTime.Now.ToString("yyyy-MM"));
                DateTime now = DateTime.Now;
                summary_bk = Path.Combine(companyDir, company + $"_{now.ToString("yyyy-MM")}_" + ".xls");
                DateTime startdate = DateTime.Parse(now.ToString("yyyy-MM-01"));//备份当月总表
                if (!Directory.Exists(monDir))
                {
                    Directory.CreateDirectory(monDir);
                }
                ExcelTool summary = new ExcelTool(summaryPath, "Sheet1");
                double cost = summary.GetEmployeeNumber() * currUser.UnitPrice;

                string fileName = $"{DateTime.Now.ToString("yyyy-MM-dd")}@{cost}@期初自动流转@Add@{Guid.NewGuid()}@{DateTime.Now.ToString("HH-mm-ss")}@0@.xls";
                string template = Path.Combine(rootpath, "templates", "export_employee_download.xls");
                string newfilepath = Path.Combine(monDir, fileName);

                //创建新excel文档
                System.IO.File.Copy(template, newfilepath);

                ExcelTool et = new ExcelTool(newfilepath, "Sheet1");
                DataTable tbl_summary = summary.ExcelToDataTable("Sheet1", true);
                int i = 1;
                foreach (DataRow row in tbl_summary.Rows)
                {
                    et.SetCellText(i, 0, row[2].ToString());
                    et.SetCellText(i, 1, row[3].ToString());
                    et.SetCellText(i, 2, row[4].ToString());
                    et.SetCellText(i, 3, row[5].ToString());
                    et.SetCellText(i, 4, startdate.Date.ToString("yyyy/MM/dd"));
                    i++;
                }
                et.Save();
                LogService.Log($"{company}自动流转成功");
                return true;
            }
            catch (Exception e)
            {
                System.IO.File.Delete(summary_bk);
                LogService.Log($"{company}自动流转失败");
                throw new Exception();
            }
        }

        public void StartListening(IConfiguration configuration)
        {
            do
            {
                if (configuration["AutoRenew"].ToString().Equals("False", StringComparison.CurrentCultureIgnoreCase))
                {
                    Thread.Sleep(1000);
                    continue;
                }
                if (nextEndDate.Year < 2000)
                {
                    nextEndDate = DateTime.Now;
                    //nextEndDate = new DateTime(2020, 10, 30, 23, 59, 59);
                }
                DateTime monthLastDay = new DateTime(nextEndDate.Year, nextEndDate.Month, DateTime.DaysInMonth(nextEndDate.Year, nextEndDate.Month));
                string monthlastsecond = monthLastDay.ToString("yyyy-MM-dd 23:59:59");
                nextEndDate = DateTime.Parse(monthlastsecond);
                if (DateTime.Now > nextEndDate)
                {
                    shouldRenew = true;
                    Thread.Sleep(2000);
                }
                if (shouldRenew)
                {               
                    foreach (var locker in Utility.LockerList)
                    {
                        locker.RWLocker.EnterWriteLock();
                    }
                    LogService.Log("开始自动流转");
                    RenewAllCompanies();
                    LogService.Log("自动流转完成");
                    foreach (var locker in Utility.LockerList)
                    {
                        if (locker != null && locker.RWLocker.IsWriteLockHeld)
                        {
                            locker.RWLocker.ExitWriteLock();
                        }
                    }
                    shouldRenew = false;
                    nextEndDate = new DateTime();
                }
                Thread.Sleep(1000);
            } while (1 == 1);
        }
    }
}
