﻿using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualCredit;
using VirtualCredit.Controllers;
using VirtualCredit.LogServices;
using VirtualCredit.Models;

namespace Insurance.Controllers
{
    public class MiniAppHomeController : HomeController
    {
        public MiniAppHomeController(IHostingEnvironment hostingEnvironment, IHttpContextAccessor httpContextAccessor) : base(hostingEnvironment, httpContextAccessor)
        {

        }

        public IActionResult MiniHistoricalList(string openId)
        {

            try
            {
                MiniSession(openId);
                var user = GetCurrentUser();
                if (user.AccessLevel != 0)
                {
                    return MiniCompanyHisitoryByMonth(user.CompanyName, openId);
                }
                HistoricalModel model = new HistoricalModel();
                model.CompanyList = GetChildAccountsCompany();
                if (model.CompanyList != null)
                    model.CompanyList = model.CompanyList.OrderBy(c => c.Name).ToList();
                ViewBag.PageInfo = "保单列表";
                return View("HistoricalListMiniApp", model);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return View("Error");
            }

        }

        public IActionResult MiniYearHistory([FromQuery] int year, string openId)
        {
            MiniSession(openId);
            if (year == From.Year)
            {
                return HistoricalList();
            }

            string dataDir = Path.Combine(ExcelRoot, "历年归档", year.ToString(), "管理员");
            if (!Directory.Exists(dataDir)) return View("Error");
            string[] companyList = Directory.GetDirectories(dataDir, "*", SearchOption.TopDirectoryOnly).Where(d =>
            {
                DirectoryInfo di = new DirectoryInfo(d);
                return !Plans.Contains(di.Name) && !DateTime.TryParse(di.Name, out DateTime dt);
            }).ToArray();
            HistoricalModel model = new HistoricalModel();
            List<Company> compList = new List<Company>();
            foreach (string company in companyList)
            {
                DirectoryInfo di = new DirectoryInfo(company);
                Company comp = new Company();
                comp.Name = di.Name;
                comp.StartDate = new DateTime(year, 6, 1);
                foreach (string plan in Plans)
                {
                    ExcelDataReader edr = new ExcelDataReader(di, year, plan);
                    comp.PaidCost += edr.GetPaidCost();
                    comp.TotalCost += edr.GetTotalCost();
                    comp.EmployeeNumber += edr.GetEmployeeNumber();
                }
                compList.Add(comp);
            }
            model.CompanyList = compList;
            ViewBag.PageInfo = $"{year}年数据";
            ViewBag.IsHistory = true;
            return View("HistoricalListMiniApp", model);
        }

        public IActionResult MiniCompanyHisitoryByMonth([FromQuery] string name, string openId)
        {
            MiniSession(openId);
            ReaderWriterLockSlim r_locker = null;
            bool isSelf = false;
            UserInfoModel currUser = GetCurrentUser();
            try
            {
                r_locker = currUser.MyLocker.RWLocker;
                r_locker.EnterReadLock();
                //获取该公司历史表单详细
                List<NewExcel> allMonthlyExcels = new List<NewExcel>();
                if (currUser.CompanyName == name)
                {
                    isSelf = true;
                }
                if (!HasAuthority(name))
                {
                    return Json("权限不足");
                }
                string companyName = name;
                string targetCompanyDir;

                string currUserDir = GetCurrentUserRootDir(currUser);

                if (isSelf)
                {
                    targetCompanyDir = Path.Combine(currUserDir, currUser._Plan);
                }
                else
                {
                    targetCompanyDir = Directory.GetDirectories(currUserDir, companyName, SearchOption.AllDirectories)[0];
                }

                for (DateTime date = From; date <= To; date = date.AddMonths(1))
                {
                    string dirName = date.ToString("yyyy-MM");
                    SearchOption so = SearchOption.AllDirectories;
                    if (isSelf)
                    {
                        so = SearchOption.TopDirectoryOnly;
                    }
                    var monthDirs = Directory.GetDirectories(targetCompanyDir, dirName, so);
                    if (monthDirs.Length == 0 || !Directory.Exists(monthDirs[0])) continue;
                    NewExcel excel = null;
                    int headcount = 0;
                    decimal cost = 0;
                    excel = new NewExcel();
                    excel.Company = name;
                    DateTime dt = date;
                    excel.StartDate = new DateTime(dt.Year, dt.Month, 1).ToShortDateString();
                    excel.EndDate = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToShortDateString();
                    foreach (var monthDir in monthDirs)
                    {
                        foreach (string fileName in Directory.GetFiles(monthDir))
                        {
                            FileInfo fi = new FileInfo(fileName);
                            ExcelTool et = new ExcelTool(fileName, "Sheet1");
                            string[] fileinfo = fi.Name.Split('@');
                            headcount += et.GetEmployeeNumber();
                            cost += decimal.Parse(fileinfo[1]);
                        }
                    }
                    if (excel != null)
                    {
                        excel.HeadCount = headcount;
                        excel.Cost = cost;
                        allMonthlyExcels.Add(excel);
                    }
                }

                DetailModel dm = new DetailModel();
                dm.Company = name;
                dm.MonthlyExcel = allMonthlyExcels;
                return View("MiniDetail", dm);
            }
            catch
            {
                DetailModel dm = new DetailModel();
                dm.Company = name;
                dm.MonthlyExcel = new List<NewExcel>();
                return View("MiniDetail", dm);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }
        }

        public IActionResult MiniYearlyHistoryData([FromQuery] string companyName, [FromQuery] int year, string openId)
        {
            MiniSession(openId);
            ReaderWriterLockSlim r_locker = null;
            bool isSelf = false;
            var currUser = GetCurrentUser();
            try
            {
                r_locker = currUser.MyLocker.RWLocker;
                r_locker.EnterReadLock();
                //获取该公司历史表单详细
                List<NewExcel> allMonthlyExcels = new List<NewExcel>();
                if (currUser.CompanyName == companyName)
                {
                    isSelf = true;
                }
                if (!HasAuthority(companyName))
                {
                    return Json("权限不足");
                }
                string targetCompanyDir;
                string yearDir = Path.Combine(ExcelRoot, "历年归档", year.ToString());
                string currUserDir = GetCurrentUserHistoryRootDir(year);

                if (isSelf)
                {
                    targetCompanyDir = Path.Combine(currUserDir, currUser._Plan);
                }
                else
                {
                    targetCompanyDir = Directory.GetDirectories(currUserDir, companyName, SearchOption.AllDirectories).FirstOrDefault();
                }
                DateTime dtFrom = new DateTime(year, 6, 1);
                DateTime dtTo = new DateTime(year + 1, 5, 31);
                for (DateTime date = dtFrom; date <= dtTo; date = date.AddMonths(1))
                {
                    string dirName = date.ToString("yyyy-MM");
                    SearchOption so = SearchOption.AllDirectories;
                    if (isSelf)
                    {
                        so = SearchOption.TopDirectoryOnly;
                    }
                    var monthDirs = Directory.GetDirectories(targetCompanyDir, dirName, so);
                    if (monthDirs.Length == 0 || !Directory.Exists(monthDirs[0])) continue;
                    NewExcel excel = null;
                    int headcount = 0;
                    decimal cost = 0;
                    excel = new NewExcel();
                    excel.Company = companyName;
                    DateTime dt = date;
                    excel.StartDate = new DateTime(dt.Year, dt.Month, 1).ToShortDateString();
                    excel.EndDate = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToShortDateString();
                    foreach (var monthDir in monthDirs)
                    {
                        foreach (string fileName in Directory.GetFiles(monthDir))
                        {
                            FileInfo fi = new FileInfo(fileName);
                            ExcelTool et = new ExcelTool(fileName, "Sheet1");
                            string[] fileinfo = fi.Name.Split('@');
                            headcount += et.GetEmployeeNumber();
                            cost += decimal.Parse(fileinfo[1]);
                        }
                    }
                    if (excel != null)
                    {
                        excel.HeadCount = headcount;
                        excel.Cost = cost;
                        allMonthlyExcels.Add(excel);
                    }
                }


                DetailModel dm = new DetailModel();
                dm.Company = companyName;
                dm.MonthlyExcel = allMonthlyExcels;
                return View("MiniDetail", dm);
            }
            catch
            {
                DetailModel dm = new DetailModel();
                dm.Company = companyName;
                dm.MonthlyExcel = new List<NewExcel>();
                return View("MiniDetail", dm);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }

            }
        }

        public IActionResult MiniSummaryByYear(string openId)
        {
            MiniSession(openId);
            HistoricalModel model = new HistoricalModel();
            DataTable dt = new DataTable();
            DataColumn year_col = new DataColumn("Year");
            DataColumn hc_col = new DataColumn();
            DataColumn totalearned_col = new DataColumn();
            DataColumn totalpaid_col = new DataColumn();

            dt.Columns.Add(year_col);
            dt.Columns.Add(hc_col);
            dt.Columns.Add(totalearned_col);
            dt.Columns.Add(totalpaid_col);
            for (int year = 2019; year <= From.Year; year++)
            {
                DataRow newrow = dt.NewRow();
                newrow[0] = year;
                var data = SummaryByYear(year);
                if (data.Keys.Count > 0)
                {
                    newrow[1] = (int)data["headCount"];
                    newrow[2] = (double)data["totalIn"];
                    newrow[3] = (double)data["totalOut"];
                    dt.Rows.Add(newrow);
                }
            }
            model.SummaryByYearTable = dt;
            ViewBag.PageInfo = "历年保单数据";
            return View("HistoricalListMiniApp", model);
        }

        public JsonResult MiniPreviewTable(string company, string fileName, string date, string openId)
        {
            try
            {
                MiniSession(openId);
                UserInfoModel currUser = GetCurrentUser();
                string[] fileinfo = new FileInfo(fileName).Name.Split("@");
                UserInfoModel uploadUser = DatabaseService.SelectUser(fileinfo[2]);
                if (!uploadUser.UserName.Equals("期初自动流转"))
                {
                    if (!IsChildCompany(currUser, company) && company != currUser.CompanyName)
                        return null;
                }

                FileInfo fi = new FileInfo(fileName);
                string downloadName = company + "_" + fi.Name.Split("@")[0] + ".xls";
                string companyDir = string.Empty;
                companyDir = ExcelRoot;
                string filePath = Directory.GetFiles(companyDir, fileName, SearchOption.AllDirectories)[0];//路径


                // string filePath = Path.Combine(Path.Combine(_hostingEnvironment.WebRootPath, "Excel", company, DateTime.Parse(date).ToString("yyyy-MM"), fileName));//路径
                ExcelTool excelTool = new ExcelTool(filePath, "Sheet1");
                return Json(excelTool.ExcelToDataTable("Sheet1", false));
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                LogService.Log(e.StackTrace);
                return null;
            }
        }


        public IActionResult MiniCompanyHistory([FromQuery] string date, [FromQuery] string name, string openId)
        {
            MiniSession(openId);
            ReaderWriterLockSlim r_locker = null;
            DetailModel dm;
            UserInfoModel currUser = GetCurrentUser();
            bool isSelf = false;
            if (currUser.CompanyName == name) isSelf = true;
            try
            {
                r_locker = currUser.MyLocker.RWLocker;
                r_locker.EnterReadLock();
                dm = new DetailModel();
                //获取该公司历史表单详细
                string currUserCompany = string.Empty;
                string currUserRootDir = GetCurrentUserRootDir(currUser);
                currUserCompany = new DirectoryInfo(currUserRootDir).Name;

                List<NewExcel> allexcels = new List<NewExcel>();
                if (!GetChildrenCompanies(currUserCompany).Contains(name) && currUser.CompanyName != name) return View("Error");
                //string date = "2020/3/1";
                DateTime dt1 = DateTime.Parse(date);
                string month = dt1.ToString("yyyy-MM");
                string companyName = name;
                List<string> excels = new List<string>();
                var dataInfo = date.Split('/');
                DateTime target = new DateTime(Convert.ToInt32(dataInfo[0]), Convert.ToInt32(dataInfo[1]), Convert.ToInt32(dataInfo[2]));
                string companyDir = string.Empty;
                if (target.Year < From.Year)
                {
                    string historyDir = Path.Combine(ExcelRoot, "历年归档", target.Year.ToString());
                    companyDir = Directory.GetDirectories(historyDir, "*", SearchOption.AllDirectories).Where(_ => new DirectoryInfo(_).Name == name).FirstOrDefault();
                }
                else
                    companyDir = GetSearchExcelsInDir(companyName);
                SearchOption so = SearchOption.AllDirectories;
                if (isSelf) so = SearchOption.TopDirectoryOnly;
                List<string> monthDirs = new List<string>();
                if (!string.IsNullOrEmpty(currUser._Plan))
                {
                    foreach (var plan in currUser._Plan.Split(' '))
                    {
                        DirectoryInfo di = new DirectoryInfo(Path.Combine(companyDir, plan));
                        if (di.Exists)
                            monthDirs.AddRange(Directory.GetDirectories(Path.Combine(companyDir, plan), month, so));
                    }
                }
                else
                {
                    monthDirs = Directory.GetDirectories(companyDir, month, so).ToList();
                }

                if (monthDirs.Count > 0)
                {
                    foreach (string monthDir in monthDirs)
                    {
                        var uploadedFiles = Directory.GetFiles(monthDir);
                        excels.AddRange(uploadedFiles);
                    }
                }
                else
                {
                    dm.Company = name;
                    dm.Excels = new List<NewExcel>();
                    return View("MiniDetail", dm);
                }

                foreach (string fileName in excels)
                {
                    FileInfo fi = new FileInfo(fileName);
                    if (fi.Name.Replace(fi.Extension, string.Empty) == companyName)
                        continue;

                    NewExcel excel = new NewExcel();
                    excel.FileName = fi.Name;
                    excel.Company = Directory.GetParent(Directory.GetParent(fileName).Parent.FullName).Name;
                    excel.Plan = Directory.GetParent(fileName).Parent.Name;
                    ExcelTool et = new ExcelTool(fileName, "Sheet1");
                    excel.EndDate = et.GetCellText(1, 5, ExcelTool.DataType.String);
                    string[] fileinfo = fi.Name.Split('@');
                    DateTime dt;
                    DateTime.TryParse(fileinfo[0] + " " + fileinfo[5].Replace('-', ':'), out dt);
                    excel.UploadDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
                    excel.HeadCount = et.GetEmployeeNumber();
                    excel.Uploader = fileinfo[2];
                    if (fileinfo[3].Equals("add", StringComparison.CurrentCultureIgnoreCase)) //若结束日期为空，则为增加人员文档
                    {
                        excel.Mode = "加保";
                        excel.StartDate = et.GetCellText(1, 4, ExcelTool.DataType.String);
                        excel.Cost = decimal.Parse(fileinfo[1]);
                    }
                    else //若结束时间不为空，则为减员文档
                    {
                        excel.Mode = "减保";
                        excel.StartDate = et.GetCellText(1, 4, ExcelTool.DataType.String);
                        excel.EndDate = et.GetCellText(1, 5, ExcelTool.DataType.String);
                        excel.Cost = decimal.Parse(fileinfo[1]);
                    }
                    allexcels.Add(excel);
                }
                allexcels.Sort((l, r) =>
                {
                    if (DateTime.Parse(l.UploadDate) != DateTime.Parse(r.UploadDate))
                    {
                        return DateTime.Parse(r.UploadDate).CompareTo(DateTime.Parse(l.UploadDate));
                    }
                    else
                    {
                        return l.Cost.CompareTo(r.Cost);
                    }
                });
                dm.Company = name;
                dm.Excels = allexcels;
                return View("MiniDetail", dm);
            }
            catch
            {
                dm = new DetailModel();
                dm.Company = name;
                dm.Excels = new List<NewExcel>();
                return View("MiniDetail", dm);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }
    }
}
