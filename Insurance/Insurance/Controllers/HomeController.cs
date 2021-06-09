using ICSharpCode.SharpZipLib.Zip;
using Insurance;
using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using NPOI.HSSF.Extractor;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit.Controllers
{
    public class HomeController : VC_ControllerBase
    {
        private static readonly int idCol = 3;
        private static readonly int nameCol = 2;

        public HomeController(IHostingEnvironment hostingEnvironment, IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor.HttpContext)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        [UserLoginFilters]
        public IActionResult Index(HomePageModel model)
        {
            try
            {
                ViewBag.UserName = GetCurrentUser().UserName;
                Response.Cookies.Append("invalidFlag", DateTime.UtcNow.ToString(),
                new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddDays(7) });
                return View("Index", model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }

        }
        [UserLoginFilters]
        public IActionResult HistoricalList()
        {
            try
            {
                HistoricalModel model = new HistoricalModel();
                model.CompanyList = GetChildAccountsCompany();
                if (model.CompanyList != null)
                    model.CompanyList = model.CompanyList.OrderBy(c => c.Name).ToList();
                ViewBag.PageInfo = "保单列表";
                return View("HistoricalList", model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }

        }


        /// <summary>
        /// 获取某年的公司数据
        /// </summary>
        /// <param name="year"></param>
        /// <returns></returns>
        [UserLoginFilters]
        public IActionResult YearHistory([FromQuery] int year)
        {
            string backUpDir = string.Empty;
            var currUser = GetCurrentUser();
            if (year == From.Year)
            {
                return HistoricalList();
            }
            else
            {
                backUpDir = Path.Combine(ExcelRoot, "历年归档", year.ToString());
            }
            //string dataDir = Path.Combine(ExcelRoot, "历年归档", year.ToString(), "管理员");
            string dataDir = Directory.GetDirectories(backUpDir, currUser.CompanyName, SearchOption.AllDirectories).FirstOrDefault(); //当前账号所属公司的文件夹

            if (!Directory.Exists(dataDir)) return View("Error");
            //string[] companyList = Directory.GetDirectories(dataDir, "*", SearchOption.TopDirectoryOnly).Where(d =>
            //  {
            //      DirectoryInfo di = new DirectoryInfo(d);
            //      return !Plans.Contains(di.Name) && !DateTime.TryParse(di.Name, out DateTime dt);
            //  }).ToArray();

            List<string> subCompanyList = new List<string>();
            foreach (string plan in currUser._Plan.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var accessibleDirs = Directory.GetDirectories(dataDir, plan, SearchOption.AllDirectories);
                foreach (string dir in accessibleDirs)
                {
                    DirectoryInfo di = new DirectoryInfo(dir);
                    if (!subCompanyList.Contains(di.Parent.FullName))
                        subCompanyList.Add(di.Parent.FullName);
                }
            }


            HistoricalModel model = new HistoricalModel();
            List<Company> compList = new List<Company>();
            foreach (string company in subCompanyList)
            {
                DirectoryInfo di = new DirectoryInfo(company);
                Company comp = new Company();
                comp.Name = di.Name;
                comp.StartDate = new DateTime(year, 6, 1);
                foreach (string plan in currUser._Plan.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    ExcelDataReader edr = new ExcelDataReader(di, year, plan);
                    comp.PaidCost += edr.GetPaidCost();
                    comp.TotalCost += edr.GetTotalCostExcludeChildren();
                    comp.EmployeeNumber += edr.GetEmployeeNumber();
                }
                compList.Add(comp);
            }
            model.CompanyList = compList;
            ViewBag.PageInfo = $"{year}年数据";
            ViewBag.IsHistory = true;
            return View("HistoricalList", model);
        }

        [UserLoginFilters]
        public ActionResult AllProofs(HistoricalModel historicalModel)
        {
            List<string> files = new List<string>();
            var currUser = GetCurrentUser();
            if (historicalModel.ProofDate.Year < 2000)
            {
                Response.StatusCode = 400;
                return Json("请选择日期后再试");
            }
            string date = historicalModel.ProofDate.ToString("yyyy-MM");

            var childAccounts = currUser.ChildAccounts;
            foreach (var account in childAccounts)
            {
                string company = account.CompanyName;
                files.Add(GenerateProofFile(company, date, currUser, account._Plan));
            }
            files.RemoveAll(x => string.IsNullOrEmpty(x));
            string zipFile = Path.Combine(ExcelRoot, "zip_temp.zip");
            if (System.IO.File.Exists(zipFile))
            {
                System.IO.File.Delete(zipFile);
            }
            using (ZipFile zip = ZipFile.Create(zipFile))
            {
                zip.BeginUpdate();
                foreach (var file in files)
                {
                    zip.Add(file, new FileInfo(file).Name);
                }
                zip.CommitUpdate();
                zip.Close();
            }
            FileStream downloadStream = new FileStream(zipFile, FileMode.Open, FileAccess.Read);
            return File(downloadStream, "application/zip", $"{date}_保险凭证.zip");
        }

        /// <summary>
        /// 获取历史归档文件夹中的公司数据
        /// </summary>
        /// <returns></returns>
        [UserLoginFilters]
        public IActionResult YearlyHistoryData([FromQuery] string companyName, [FromQuery] int year)
        {
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
                return View("Detail", dm);
            }
            catch
            {
                DetailModel dm = new DetailModel();
                dm.Company = companyName;
                dm.MonthlyExcel = new List<NewExcel>();
                return View("Detail", dm);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }
        }

        [UserLoginFilters]
        public IActionResult CompanyHisitoryByMonth([FromQuery] string name)
        {
            ReaderWriterLockSlim r_locker = null;
            bool isSelf = false;

            try
            {
                r_locker = GetCurrentUser().MyLocker.RWLocker;
                r_locker.EnterReadLock();
                //获取该公司历史表单详细
                List<NewExcel> allMonthlyExcels = new List<NewExcel>();
                var currUser = GetCurrentUser();
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
                return View("Detail", dm);
            }
            catch
            {
                DetailModel dm = new DetailModel();
                dm.Company = name;
                dm.MonthlyExcel = new List<NewExcel>();
                return View("Detail", dm);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }

        [UserLoginFilters]
        [AdminFilters]
        public FileStreamResult ExportAllEmployees()
        {
            ReaderWriterLockSlim r_locker = null;
            string allemployees = string.Empty;
            try
            {
                r_locker = GetCurrentUser().MyLocker.RWLocker;
                if (!r_locker.IsReadLockHeld)
                    r_locker.EnterReadLock();
                DataTable tbl_summary = new DataTable();
                string targetDirectory = _hostingEnvironment.WebRootPath;
                string template = Path.Combine(targetDirectory, "Excel", "AllEmployeeTemplate.xls");
                allemployees = Path.Combine(targetDirectory, "Excel", "all_employees.xls");
                System.IO.File.Copy(template, allemployees, true);
                foreach (string company in Directory.GetDirectories(Path.Combine(targetDirectory, "Excel", "管理员"), "*", SearchOption.AllDirectories))
                {
                    DirectoryInfo di = new DirectoryInfo(company);
                    if (!Plans.Contains(di.Name)) continue;
                    string companyName = di.Parent.Name;
                    string comp_summary = Path.Combine(company, companyName + ".xls");
                    ExcelTool et = new ExcelTool(comp_summary, "Sheet1");
                    DataTable tbl_companySummary = et.ExcelToDataTable("Sheet1", true);
                    if (tbl_summary.Rows.Count <= 0)
                    {
                        tbl_summary = tbl_companySummary.Clone();
                    }
                    tbl_summary.Merge(tbl_companySummary);
                }
                ExcelTool summary = new ExcelTool(allemployees, "Sheet1");
                for (int row = 0; row < tbl_summary.Rows.Count; row++)
                {
                    int excel_row = row + 1;
                    summary.m_main.CreateRow(excel_row);
                    summary.m_main.GetRow(excel_row).CreateCell(0);
                    summary.m_main.GetRow(excel_row).GetCell(0).SetCellValue(excel_row);
                    for (int column = 1; column <= 6; column++) // 列：公司，姓名，ID，职业类别，工种，生效日期
                    {
                        summary.m_main.GetRow(excel_row).CreateCell(column);
                        summary.m_main.GetRow(excel_row).GetCell(column).SetCellValue(tbl_summary.Rows[row][column].ToString());
                    }

                }
                summary.Save();
                FileStream fs = new FileStream(allemployees, FileMode.Open, FileAccess.Read);
                return File(fs, "text/plain", "在保人员名单.xls");
            }
            catch
            {
                FileStream fs = new FileStream(allemployees, FileMode.Open, FileAccess.Read);
                return File(fs, "text/plain", "在保人员名单.xls");
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }

        private static readonly object costLocker = new object();

        [UserLoginFilters]
        public bool SaveCost(string company, string cost)
        {
            double c;
            if (!double.TryParse(cost, out c))
            {
                return false;
            }
            string targetDirectory = _hostingEnvironment.WebRootPath;
            string companyFolder = Path.Combine(targetDirectory, "Excel", company);
            var txt = Directory.GetFiles(companyFolder).Where(_ => new FileInfo(_).Name.Contains(".txt"));
            foreach (string item in txt)
            {
                lock (costLocker)
                {
                    FileInfo fi = new FileInfo(item);
                    fi.MoveTo(Path.Combine(fi.DirectoryName, company + $"_{cost}.txt"));
                    return true;
                }
            }
            return false;
        }

        [UserLoginFilters]
        public IActionResult CompanyHistory([FromQuery] string date, [FromQuery] string name)
        {
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
                List<string> planDirs = new List<string>();

                if (!string.IsNullOrEmpty(currUser._Plan)) //plan!=""
                {
                    foreach (var plan in currUser._Plan.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        planDirs.AddRange(Directory.GetDirectories(companyDir, plan, so));
                    }
                    foreach (var planDir in planDirs)
                    {
                        monthDirs.AddRange(Directory.GetDirectories(planDir, month, so));
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
                    return View("Detail", dm);
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
                    if (l.Company != r.Company)
                    {
                        return r.Company.CompareTo(l.Company);
                    }
                    else
                    {
                        return DateTime.Parse(l.UploadDate).CompareTo(DateTime.Parse(r.UploadDate));
                    }
                });
                dm.Company = name;
                dm.Excels = allexcels;
                return View("Detail", dm);
            }
            catch
            {
                dm = new DetailModel();
                dm.Company = name;
                dm.Excels = new List<NewExcel>();
                return View("Detail", dm);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }

        private double CalculateAddPrice(DateTime dt, int headCount)
        {
            //对于添加新的保费信息，按生效日期至本月底的天数收费
            double result = 0;
            int year_now = DateTime.Now.Year;
            int month_now = DateTime.Now.Month;
            int day_now = DateTime.Now.Day;

            int year_specified = dt.Year;
            int month_specified = dt.Month;
            int day_sepecified = dt.Day;
            double totalPrice = GetCurrentUser().UnitPrice;
            int monthDays = DateTime.DaysInMonth(year_specified, month_specified); //计算生效日期所在月份的天数
            double unitPrice = (double)totalPrice / monthDays;
            int totalNumber = headCount;
            double pricedDays = monthDays - day_sepecified + 1; //收费天数
            result = pricedDays * unitPrice * totalNumber;
            return Math.Round(result, 2);
        }

        [HttpGet]
        [UserLoginFilters]
        public IActionResult EmployeeChange(EmployeeChangeModel model)
        {
            var currUser = GetCurrentUser();
            try
            {
                int advanceDays = currUser.DaysBefore;
                DateTime dt = DateTime.Now.Date.AddDays(-1d * advanceDays);
                model.AllowedStartDate = dt.ToString("yyyy-MM-dd");
                model.CompanyNameList = GetSpringCompaniesName(true);
                //if (currUser.AccessLevel != 0)
                //    model.CompanyList.Add(new Company() { Name = currUser.CompanyName });
                model.Plans = currUser.AccessLevel == 0 ? "all" : currUser._Plan;
                return View(nameof(EmployeeChange), model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                Response.StatusCode = 404;
                return View("Error");
            }
        }

        [HttpPost]
        [UserLoginFilters]
        public JsonResult StartRenew([FromForm] RenewModel test)
        {
            ReaderWriterLockerWithName locker = null;
            string summary = string.Empty;
            string summary_backup = string.Empty;
            DateTime now = DateTime.Now.Date;
            UserInfoModel currUser = GetCurrentUser();
            try
            {
                if (now.AddDays(4).Month != now.AddMonths(1).Month)
                {
                    return Json("每月的最后三天才能进行该操作");
                }
                DateTime nextMonth = DateTime.Parse(test.CurrentMonth).AddMonths(1);
                DateTime nextMonthLastDay = new DateTime(nextMonth.Year, nextMonth.Month, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                if (test.NextMonthEndDay != nextMonthLastDay.ToString("yyyy-MM-dd"))
                {
                    return Json("只可续保至" + test.NextMonthEndDay);
                }

                if (currUser == null)
                {
                    return Json("请先登录");
                }

                if (currUser.AccessLevel != 0 && currUser.CompanyName != test.CompanyName)
                {
                    return Json("当前账号无权进行该操作");
                }
                //==================写锁====================
                if (currUser.AllowCreateAccount == "1")
                {
                    locker = Utility.GetCompanyLocker(test.CompanyName);
                }
                if (locker != null)
                {
                    locker.RWLocker.EnterWriteLock();
                }
                //备份当前summary文件，生成新的summary,生效日期为每月1号
                string compDir = GetSearchExcelsInDir(test.CompanyName);
                summary = Path.Combine(compDir, test.Plan, test.CompanyName + ".xls");
                summary_backup = Path.Combine(compDir, test.Plan, test.CompanyName + "_" + DateTime.Now.ToString("yyyy-MM") + "_bk.xls");
                System.IO.File.Copy(summary, summary_backup, true);
                using (ExcelTool et = new ExcelTool(summary, "Sheet1"))
                {
                    System.IO.File.Copy(summary, summary_backup, true);
                    if (et.GetEmployeeNumber() <= 0)
                    {
                        return Json("无人参保");
                    }
                    DataTable tbl_summary = et.ExcelToDataTable("Sheet1", true);
                    foreach (DataRow row in tbl_summary.Rows)
                    {
                        DateTime dt = DateTime.Now.Date.AddMonths(1);
                        row["生效日期"] = new DateTime(dt.Year, dt.Month, 1).ToString("yyyy-MM-dd");
                    }
                    et.DatatableToExcel(tbl_summary);
                }

                GenerateNewExcelForRenew(test.CompanyName, test.Plan);
                return Json("续保成功");
            }
            catch (Exception e)
            {
                RevertSummaryFile(summary, summary_backup);
                return Json("续保失败");
            }
            finally
            {
                if (locker != null && locker.RWLocker.IsWriteLockHeld)
                {
                    locker.RWLocker.ExitWriteLock();
                }
            }

        }

        private bool GenerateNewExcelForRenew(string company, string plan)
        {
            UserInfoModel currUser = null;
            string summary_bk = string.Empty;
            currUser = GetCurrentUser();
            var locker = currUser.MyLocker.RWLocker;
            try
            {
                //=========================================进入读锁=====================================================//

                if (!locker.IsWriteLockHeld)
                    locker.EnterWriteLock();

                string companyDir = GetSearchExcelsInDir(company);
                string summaryPath = Path.Combine(companyDir, plan, company + ".xls");

                string monDir = Path.Combine(companyDir, plan, DateTime.Now.AddMonths(1).ToString("yyyy-MM"));
                DateTime now = DateTime.Now;
                summary_bk = Path.Combine(companyDir, plan, company + $"_{now.ToString("yyyy-MM")}_" + ".xls");
                DateTime startdate = DateTime.Parse(now.AddMonths(1).ToString("yyyy-MM-01"));
                //System.IO.File.Copy(summaryPath, summary_bk, true); //备份当月总表
                if (!Directory.Exists(monDir))
                {
                    Directory.CreateDirectory(monDir);
                }
                ExcelTool summary = new ExcelTool(summaryPath, "Sheet1");
                double cost = summary.GetEmployeeNumber() * currUser.UnitPrice;

                string fileName = $"{DateTime.Now.ToString("yyyy-MM-dd")}@{cost}@{currUser.UserName}@Add@{Guid.NewGuid()}@{DateTime.Now.ToString("HH-mm-ss")}@0@.xls";
                string template = Path.Combine(_hostingEnvironment.WebRootPath, "templates", "export_employee_download.xls");
                string newfilepath = Path.Combine(monDir, fileName);

                //创建新excel文档
                System.IO.File.Copy(template, newfilepath);

                using (ExcelTool et = new ExcelTool(newfilepath, "Sheet1"))
                {
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
                }

                return true;
            }
            catch (Exception e)
            {
                System.IO.File.Delete(summary_bk);
                throw new Exception();
            }
            finally
            {
                if (locker != null && locker.IsWriteLockHeld)
                    locker.ExitWriteLock(); //退出写锁
            }

        }

        private void RevertSummaryFile(string path, string backup)
        {
            //删除原文件，重命名备份文件
            if (System.IO.File.Exists(path) && System.IO.File.Exists(backup))
            {
                System.IO.File.Delete(path);
                System.IO.File.Move(backup, path);
            }
        }

        [UserLoginFilters]
        public IActionResult AutoRenew()
        {
            UserInfoModel currUser = GetCurrentUser();
            string company = string.Empty;
            DataTable result = new DataTable();
            result.Columns.AddRange(
                new DataColumn[] {
                    new DataColumn("公司"),
                    new DataColumn("方案"),
                    new DataColumn("当前保单月份"),
                    new DataColumn("续保人数"),
                    new DataColumn("总保费"),
                    new DataColumn("1-4类人数"),
                    new DataColumn("1-4类保费"),
                    new DataColumn("续保后到期日"),
                }
                );
            if (currUser != null)
            {
                company = currUser.CompanyName;
            }
            RenewModel rm = new RenewModel();
            string companyDir = GetSearchExcelsInDir(company);
            string summary = Path.Combine(companyDir, currUser._Plan, company + ".xls");
            if (!System.IO.File.Exists(summary))
            {
                DataRow errorrow = result.NewRow();
                errorrow["公司"] = company;
                errorrow["方案"] = currUser._Plan;
                errorrow["当前保单月份"] = "无人参保";
                errorrow["续保人数"] = 0;
                errorrow["总保费"] = 0;
                errorrow["1-4类人数"] = 0;
                errorrow["1-4类保费"] = 0;
                errorrow["续保后到期日"] = string.Empty;
                result.Rows.Add(errorrow);
                rm.MonthInfo = result;
                return View(nameof(AutoRenew), rm);
            }
            ExcelTool et = new ExcelTool(summary, "Sheet1");
            int headcount = et.GetEmployeeNumber(); //总人数
            double unitprice = currUser.UnitPrice;
            double totalcost = Math.Round(headcount * unitprice, 2); //总保费
            DataTable tbl = et.ExcelToDataTable("Sheet1", true);
            int headcount_1_4 = 0;//1-4类人数
            int headcount_4 = 0;
            DateTime outDT;
            string currentMonth;
            if (DateTime.TryParse(et.GetCellText(1, 6), out outDT))
            {
                currentMonth = outDT.ToString("yyyy-MM");
            }
            else
            {
                DataRow errorrow = result.NewRow();
                errorrow["公司"] = company;
                errorrow["方案"] = currUser._Plan;
                errorrow["当前保单月份"] = "无人参保";
                errorrow["续保人数"] = 0;
                errorrow["总保费"] = 0;
                errorrow["1-4类人数"] = 0;
                errorrow["1-4类保费"] = 0;
                errorrow["续保后到期日"] = string.Empty;
                result.Rows.Add(errorrow);
                rm.MonthInfo = result;
                return View(nameof(AutoRenew), rm);
            }

            foreach (DataRow row in tbl.Rows)
            {
                if (row["职业类别"].ToString() == "1-4类")
                {
                    headcount_1_4++;
                }
                else if (row["职业类别"].ToString() == "4类以上")
                {
                    headcount_4++;
                }
            }
            double cost_1_4 = Math.Round(headcount_1_4 * unitprice); //1-4类 保费
            DateTime now = DateTime.Now.Date;
            DateTime nextMonth = new DateTime(now.AddMonths(1).Year, now.AddMonths(1).Month, DateTime.DaysInMonth(now.AddMonths(1).Year, now.AddMonths(1).Month));

            DataRow newrow = result.NewRow();
            newrow["公司"] = company;
            newrow["方案"] = currUser._Plan;
            newrow["当前保单月份"] = currentMonth;
            newrow["续保人数"] = headcount;
            newrow["总保费"] = totalcost;
            newrow["1-4类人数"] = headcount_1_4;
            newrow["1-4类保费"] = cost_1_4;
            newrow["续保后到期日"] = nextMonth.ToString("yyyy-MM-dd");
            result.Rows.Add(newrow);
            rm.MonthInfo = result;
            return View(nameof(AutoRenew), rm);
        }

        [HttpGet]
        [UserLoginFilters]
        public IActionResult SearchPeople(SearchPeopleModel model)
        {
            CurrentSession.Set<List<Employee>>("searchResult", null);
            try
            {
                return View(nameof(SearchPeople), model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }
        }

        [HttpGet]
        [UserLoginFilters]
        public JsonResult ShowPersonalDetail([FromQuery] string comp, [FromQuery] string id)
        {
            DataTable res = new DataTable();
            ReaderWriterLockSlim r_locker = null;
            try
            {
                r_locker = GetCurrentUser().MyLocker.RWLocker;
                r_locker.EnterReadLock();

                DataColumn[] cols = {
                new DataColumn("mode"),
                new DataColumn("uploadDate"),
                new DataColumn("start"),
                new DataColumn("end"),
                };
                res.Columns.AddRange(cols);
                string companyDir = Path.Combine(_hostingEnvironment.WebRootPath, "Excel", comp);
                string[] dirs = Directory.GetDirectories(companyDir);
                foreach (string dir in dirs)
                {
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        ExcelTool et = new ExcelTool(file, "Sheet1");
                        Employee em = et.SelectByID(id);
                        if (em != null)
                        {
                            DirectoryInfo fi = new DirectoryInfo(file);
                            string[] fileinfo = fi.Name.Split('@');
                            DataRow dr = res.NewRow();
                            dr[0] = fileinfo[3];
                            dr[1] = $"{fileinfo[0]} {fileinfo[5].Replace('-', ':')}";
                            dr[2] = em.StartDate;
                            dr[3] = em.EndDate;
                            res.Rows.Add(dr);
                        }
                    }
                }
                return Json(res);
            }
            catch
            {
                return Json(res);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }

        [HttpGet]
        [UserLoginFilters]
        public IActionResult Search(string em_name, string em_id)
        {
            var currUser = GetCurrentUser();
            SearchPeopleModel model = new SearchPeopleModel();
            model.People = new Employee();
            model.People.Name = em_name;
            model.People.ID = em_id;
            DataTable res = new DataTable();
            string company = currUser.CompanyName;
            string debug = "";
            try
            {
                currUser.MyLocker.RWLocker.EnterReadLock();
                string targetDir = GetSearchExcelsInDir(company);
                foreach (var file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                {
                    debug = file;
                    FileInfo fi = new FileInfo(file);
                    if (!DateTime.TryParse(fi.Directory.Name, out DateTime date)) continue;
                    DataTable temp = new DataTable();
                    string[] excelInfo = fi.Name.Split("@");
                    string mode = excelInfo[3].Equals("Add", StringComparison.CurrentCultureIgnoreCase) ? "加保" : "减保";
                    string uploadtime = excelInfo[0] + " " + excelInfo[5].Replace('-', ':');
                    string comp = fi.Directory.Parent.Parent.Name;
                    string uploader = excelInfo[2];
                    using (ExcelTool et = new ExcelTool(file, "Sheet1"))
                    {
                        temp = et.SelectPeopleByNameAndID(em_name, nameCol, em_id, idCol);
                    }
                    if (temp != null && temp.Rows.Count > 0)
                    {
                        if (res.Columns.Count <= 0)
                        {
                            res = temp.Clone(); //拷贝表结构
                            DataColumn dc = new DataColumn("History");
                            res.Columns.Add(dc);
                        }
                        if (temp.Rows[0][1].ToString() == "未找到符合条件的人员")
                        {
                            continue;
                        }
                        foreach (DataRow row in temp.Rows)
                        {
                            string history = string.Join('%', uploadtime, mode, row["start_date"], row["end_date"], comp, uploader);
                            DataRow[] t = res.Select($"id = '{row["id"]}'");
                            if (t != null && t.Length > 0)
                            {
                                t[0]["History"] = string.Join("+", t[0]["History"], history);
                            }
                            else
                            {
                                DataRow newRow = res.NewRow();
                                newRow.ItemArray = row.ItemArray;
                                newRow["History"] = history;
                                res.Rows.Add(newRow);
                            }

                        }
                    }

                }

                model.Result = res;
                CacheSearchResult(res);
                return View("SearchPeople", model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }
            finally
            {
                currUser.MyLocker.RWLocker.ExitReadLock();
            }
        }

        protected void CacheSearchResult(DataTable table)
        {
            List<Employee> employees = new List<Employee>();
            foreach (DataRow row in table.Rows)
            {
                Employee e = new Employee();
                e.Company = row["company"].ToString();
                e.Name = row["name"].ToString();
                e.ID = row["id"].ToString();
                employees.Add(e);
            }
            CurrentSession.Set("searchResult", employees);
        }

        /// <summary>
        /// 列出所有公司的保费汇总信息
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [UserLoginFilters]
        public IActionResult GetAllRecipeSummary(int start)
        {
            try
            {
                var currUser = GetCurrentUser();
                SummaryModel sm = new SummaryModel();
                sm.PlanList = new List<Plan>();
                foreach (var plan in Plans)
                {
                    Plan p = new Plan();
                    p.Name = plan;
                    var comp = GetChildrenCompanies(currUser, plan, start);
                    p.TotalCost = comp.Sum(x => x.TotalCost);
                    p.TotalPaid = comp.Sum(x => x.CustomerAlreadyPaid);
                    p.HeadCount = comp.Sum(x => x.EmployeeNumber);
                    sm.PlanList.Add(p);
                }

                CurrentSession.Set("plan", string.Empty);

                ViewBag.Start = start;

                return View("RecieptPlans", sm);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }
        }


        /// <summary>
        /// 列出所有公司的保费汇总信息
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [UserLoginFilters]
        public IActionResult GetTargetPlanData(string plan, int year)
        {
            try
            {
                int dataYear = year == 0 ? From.Year : year;
                ViewBag.Plan = plan;
                RecipeSummaryModel model = new RecipeSummaryModel();
                var currUser = GetCurrentUser();
                model.CompanyList = GetChildrenCompanies(currUser, plan, dataYear).ToList();
                model.CompanyList = model.CompanyList.OrderBy(x => x.Name).ToList();
                CurrentSession.Set("plan", plan);
                ViewBag.DataYear = year;
                return View("RecipeSummary", model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }
        }

        [UserLoginFilters]
        [AdminFilters]
        public IActionResult BanlanceAccount([FromQuery] string companyName, [FromQuery] int dataYear)
        {
            string plan = CurrentSession.Get<string>("plan");
            ViewBag.Plan = plan;
            ViewBag.Company = companyName;

            if (string.IsNullOrEmpty(plan)) return View("Error");
            DateTime now = DateTime.Now;
            int year = 0;
            if (now.Month >= 6)
            {
                year = now.Year;
            }
            else
            {
                year = now.Year - 1;
            }
            DateTime from = new DateTime(year, 6, 1);
            DateTime to = new DateTime(year + 1, 5, 31, 23, 59, 59);

            if (dataYear != 0)
            {
                from = new DateTime(dataYear, 6, 1);
                to = new DateTime(dataYear + 1, 5, 31, 23, 59, 59);
            }
            var allDirs = Directory.GetDirectories(GetSearchExcelsInDir(companyName), "*", SearchOption.AllDirectories);
            List<string> targetDirs = new List<string>();
            allDirs.ToList().ForEach(x =>
            {
                DirectoryInfo di = new DirectoryInfo(x);
                if (DateTime.TryParse(di.Name, out DateTime date))
                {
                    var slices = di.Name.Split("-");
                    int folderYear = Convert.ToInt32(slices[0]);
                    int folderMonth = Convert.ToInt32(slices[1]);
                    if ((folderYear > from.Year && folderMonth <= to.Month) ||
                        (folderYear == from.Year && folderMonth >= from.Month))
                    {
                        targetDirs.Add(x);
                    }
                }
            });
            DetailModel detailModel = new DetailModel();
            var currUser = GetCurrentUser();
            foreach (var dir in targetDirs)
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    var excel = GetExcelInfo(file, companyName, plan, currUser);
                    if (excel != null && excel.Cost != excel.Paid)
                    {
                        detailModel.Excels.Add(excel);
                    }
                }
            }
            detailModel.Company = companyName;
            ViewBag.Page = "未结算汇总";
            return View("RecipeSummaryDetail", detailModel);
        }

        [UserLoginFilters]
        public IActionResult RecipeSummary([FromQuery] string date, [FromQuery] string name)
        {
            string plan = string.Empty;
            var currUser = GetCurrentUser();
            if (currUser.ChildAccounts.Count == 0)
            {
                plan = currUser._Plan;
            }
            else
            {
                plan = CurrentSession.Get<string>("plan");
            }
            ViewBag.Plan = plan;
            ViewBag.Company = name;

            if (string.IsNullOrEmpty(plan)) return View("Error");
            //获取该公司历史表单详细
            DetailModel dm = new DetailModel();
            List<NewExcel> allexcels = new List<NewExcel>();
            bool isSelf = false;
            if (currUser.ChildAccounts.Count == 0)
            {
                if (currUser.CompanyName != name)
                {
                    return View("Error");
                }
            }
            if (currUser.CompanyName == name) isSelf = true;
            //string date = "2020/3/1";
            if (!DateTime.TryParse(date, out DateTime dt1))
            {
                return View("Error");
            }
            string month = dt1.ToString("yyyy-MM");
            ViewBag.Date = month;
            ViewBag.DataYear = dt1.Year;
            string companyName = name;
            string targetDirectory = GetSearchExcelsInDir(companyName);
            List<string> excels = new List<string>();
            string[] monthDirs;
            SearchOption so = SearchOption.AllDirectories;
            if (isSelf)
            {
                so = SearchOption.TopDirectoryOnly;
                monthDirs = Directory.GetDirectories(Path.Combine(targetDirectory, plan), month, so);
            }
            else
            {
                monthDirs = Directory.GetDirectories(targetDirectory, month, so);
            }

            foreach (var monthDir in monthDirs)
            {
                DirectoryInfo di = new DirectoryInfo(monthDir);
                if (di.Parent.Name != plan) continue;
                excels.AddRange(Directory.GetFiles(monthDir));
            }

            if (excels == null || excels.Count <= 0)
            {
                dm.Company = name;
                dm.Excels = allexcels;
                return View("RecipeSummaryDetail", dm);
            }
            foreach (string fileName in excels)
            {
                NewExcel excel = GetExcelInfo(fileName, companyName, plan, currUser);
                if (excel != null)
                {
                    allexcels.Add(excel);
                }
            }
            dm.Company = name;
            //allexcels.Sort((a, b) =>
            //{
            //    return a.Cost - a.Paid != b.Cost - b.Paid ? Math.Abs(b.Cost - b.Paid).CompareTo(Math.Abs(a.Cost - a.Paid)) :
            //            DateTime.Parse(a.UploadDate).CompareTo(DateTime.Parse(b.UploadDate)); //先按结算状态排序，再按时间排序
            //});
            allexcels = allexcels.OrderBy(x => x.Status).ThenBy(x => x.UploadDate).ToList();
            dm.Excels = allexcels;
            return View("RecipeSummaryDetail", dm);
        }


        private Dictionary<string, UserInfoModel> m_CachedUser = new Dictionary<string, UserInfoModel>();
        protected NewExcel GetExcelInfo(string fileFullName, string companyName, string plan, UserInfoModel user = null)
        {
            var currUser = user == null ? GetCurrentUser() : user;
            FileInfo fi = new FileInfo(fileFullName);
            if (fi.Name.Replace(fi.Extension, string.Empty) == companyName)
                return null;

            string _month = fi.Directory.Name;
            string _plan = fi.Directory.Parent.Name;
            string _childCompanyName = fi.Directory.Parent.Parent.Name;
            UserInfoModel companyAccount = null;
            if (!m_CachedUser.ContainsKey(_childCompanyName))
            {
                companyAccount = DatabaseService.SelectUserByCompanyAndPlan(_childCompanyName, plan);
                m_CachedUser.Add(_childCompanyName, companyAccount);
            }
            else
            {
                companyAccount = m_CachedUser[_childCompanyName];
            }
            bool bHasChildren = companyAccount.ChildAccounts.Count > 0;
            double unitPrice = 0;
            if (_childCompanyName != companyName)
            {
                unitPrice = currUser.ChildAccounts.Where(ac => ac.SpringAccounts.Any(cac => cac.CompanyName == _childCompanyName)).FirstOrDefault().UnitPrice;
            }
            else
            {
                unitPrice = companyAccount.UnitPrice;
            }
            NewExcel excel = new NewExcel();
            excel.FileName = fi.Name;
            excel.Company = companyName;


            ExcelTool et = new ExcelTool(fileFullName, "Sheet1");
            excel.EndDate = et.GetCellText(1, 5, ExcelTool.DataType.String);
            string[] fileinfo = fi.Name.Split('@');
            excel.Submitter = fileinfo[2];
            DateTime dt;
            DateTime.TryParse(fileinfo[0] + " " + fileinfo[5].Replace('-', ':'), out dt);
            excel.UploadDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            excel.HeadCount = et.GetEmployeeNumber();
            if (fileinfo[3].Equals("add", StringComparison.CurrentCultureIgnoreCase))
            {
                excel.Mode = "加保";
                excel.StartDate = et.GetCellText(1, 4, ExcelTool.DataType.String);
                excel.Cost = decimal.Parse(fileinfo[1]) * decimal.Parse(unitPrice.ToString()) / decimal.Parse(companyAccount.UnitPrice.ToString());
            }
            else
            {
                excel.Mode = "减保";
                excel.EndDate = et.GetCellText(1, 5, ExcelTool.DataType.String);
                excel.Cost = decimal.Parse(fileinfo[1]) * decimal.Parse(unitPrice.ToString()) / decimal.Parse(companyAccount.UnitPrice.ToString());
            }
            excel.Paid = decimal.Parse(fileinfo[6]);
            return excel;
        }

        [UserLoginFilters]
        public FileStreamResult ExportStaffsByMonth(string date, string company)
        {
            DateTime exportStart = DateTime.Parse(date);
            DateTime exportEnd = new DateTime(exportStart.Year, exportStart.Month, DateTime.DaysInMonth(exportStart.Year, exportStart.Month));
            if (exportStart.Year == 1 || exportEnd.Year == 1)
                return null;
            string temperaryDir = Path.Combine(Utility.Instance.WebRootFolder, "Temp");
            string templateDir = Path.Combine(Utility.Instance.WebRootFolder, "templates");
            string temp_file = "export_employee_download.xls";
            string summary_file = Path.Combine(temperaryDir, DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss") + Guid.NewGuid() + ".xls");
            string summary_file_temp = Path.Combine(temperaryDir, DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss") + Guid.NewGuid() + "_temp.xls");
            System.IO.File.Copy(Path.Combine(templateDir, temp_file), summary_file_temp);

            ExcelTool summary = new ExcelTool(summary_file_temp, "Sheet1");
            string companyDir = GetSearchExcelsInDir(company);
            DataTable summaryTbl = new DataTable();
            foreach (string monthDir in Directory.GetDirectories(companyDir, Convert.ToDateTime(date).ToString("yyyy-MM"), SearchOption.AllDirectories))
            {
                if (Directory.Exists(monthDir))
                {
                    foreach (string excel in Directory.GetFiles(monthDir))
                    {
                        FileInfo fi = new FileInfo(excel);
                        string str_uploadDate = fi.Name.Split('@')[0];
                        //int rowBeforeMerge = summary.ExcelToDataTable("sheet1", true).Rows.Count;
                        if (!DateTime.TryParse(str_uploadDate, out DateTime uploadDate))
                        {
                            continue;
                        }
                        //DataTable dt = summary.ExcelToDataTable("sheet1", true);
                        string plan = fi.Directory.Parent.Name;
                        string account = fi.Directory.Parent.Parent.Name;
                        bool hasPlanColumn = false;
                        bool hasAccountColumn = false;

                        ExcelTool et = new ExcelTool(excel, "sheet1");
                        DataTable dt = et.ExcelToDataTable("sheet1", true);
                        foreach (DataColumn column in dt.Columns)
                        {
                            if (column.ColumnName == "方案")
                            {
                                hasPlanColumn = true;
                            }
                            else if (column.ColumnName == "所属账号")
                            {
                                hasAccountColumn = true;
                            }
                        }
                        if (!hasPlanColumn)
                        {
                            dt.Columns.Add(new DataColumn("方案"));
                        }
                        if (!hasAccountColumn)
                        {
                            dt.Columns.Add(new DataColumn("所属账号"));
                        }
                        for (int i = 0; i < dt.Rows.Count; i++)
                        {
                            dt.Rows[i]["方案"] = plan;
                            dt.Rows[i]["所属账号"] = account;
                        }

                        if (summaryTbl.Columns.Count == 0)
                        {
                            summaryTbl = dt.Clone();
                        }
                        summaryTbl.Merge(dt);
                    }
                }
            }
            summary.RawDatatableToExcel(summaryTbl);
            summary.RemoveDuplicate();
            summary.CreateAndSave(summary_file);
            System.IO.File.Delete(summary_file_temp);
            return File(new FileStream(summary_file, FileMode.Open, FileAccess.Read), "text/plain", $"{company}_{exportStart.ToString("yyyy-MM")}_入离职汇总表格.xls");
        }

        public IActionResult DetailData([FromQuery] string date)
        {
            var currUser = GetCurrentUser();
            List<string> plans = currUser._Plan.Split(' ').ToList();
            DailyDetailModel ddm = new DailyDetailModel();
            DataTable dataTable = DatabaseService.SelectPropFromTable("DailyDetailData", "YMDDate", date);
            ddm.DetailTableByDate = dataTable.Clone();
            foreach (DataRow row in dataTable.Rows)
            {
                if ((currUser.CompanyName == row["Company"].ToString() || currUser.SpringAccounts.Any(x => x.CompanyName == row["Company"].ToString()))
                    && plans.Contains(row["Product"].ToString().Trim())
                    )
                {
                    DataRow newrow = ddm.DetailTableByDate.NewRow();
                    newrow.ItemArray = row.ItemArray;
                    ddm.DetailTableByDate.Rows.Add(newrow);
                }
            }
            DataView dv = ddm.DetailTableByDate.DefaultView;
            dv.Sort = "DailyPrice DESC, Company";
            ddm.DetailTableByDate = dv.ToTable();
            return View("DailyDetail", ddm);
        }

        public IActionResult DailyDetail()
        {
            var currUser = GetCurrentUser();
            List<string> plans = currUser._Plan.Split(' ').ToList();
            List<DateTime> dateList = new List<DateTime>();
            for (int i = 14; i >= 0; i--)
            {
                dateList.Add(DateTime.Now.Date.AddDays(i * -1));
            }
            List<string> companies = new List<string>();
            foreach (var acc in currUser.SpringAccounts)
            {
                if (!companies.Contains(acc.CompanyName))
                    companies.Add(acc.CompanyName);
            }
            if (!companies.Contains(currUser.CompanyName))
                companies.Add(currUser.CompanyName);
            DataTable dataTable = DatabaseService.SelectDailyDetailByDatetime(dateList, companies, plans);
            DailyDetailModel ddm = new DailyDetailModel();
            ddm.DetailTable = dataTable;
            return View("DailyDetail", ddm);
        }

        [HttpGet]
        public IActionResult SummaryByYear()
        {
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
            return View("HistoricalList", model);

        }

        /// <summary>
        /// 统计每年所有参保公司的人数，保费，赔款
        /// </summary>
        /// <param name="year"></param>
        /// <returns></returns>
        protected Dictionary<string, ValueType> SummaryByYear(int year)
        {
            int headCount = 0;
            double totalIn = 0;
            double totalOut = 0;
            Dictionary<string, ValueType> result = new Dictionary<string, ValueType>();
            UserInfoModel currUser = GetCurrentUser();

            string backUpDir = string.Empty;
            if (year == From.Year)
            {
                backUpDir = Path.Combine(ExcelRoot);
            }
            else
            {
                backUpDir = Path.Combine(ExcelRoot, "历年归档", year.ToString());
            }
            if (!Directory.Exists(backUpDir))
            {
                result.Add("headCount", headCount);
                result.Add("totalIn", totalIn);
                result.Add("totalOut", totalOut);
                return result;
            }
            string dataDir = Directory.GetDirectories(backUpDir, currUser.CompanyName, SearchOption.AllDirectories).FirstOrDefault(); //当前账号所属公司的文件夹
            if (string.IsNullOrEmpty(dataDir)) return result;
            //if (currUser.AllowCreateAccount != "1") return new DataTable(); //如果当前公司没有子账号，则无法查看合计信息
            //var subCompanyList = Directory.GetDirectories(dataDir, "*", SearchOption.AllDirectories).Where(d =>
            //    {
            //        DirectoryInfo di = new DirectoryInfo(d);
            //        return !Plans.Contains(di.Name) && !DateTime.TryParse(di.Name, out DateTime dt);
            //    }
            //).ToList();


            List<string> subCompanyList = new List<string>();
            foreach (string plan in currUser._Plan.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var accessibleDirs = Directory.GetDirectories(dataDir, plan, SearchOption.AllDirectories);
                foreach (string dir in accessibleDirs)
                {
                    DirectoryInfo di = new DirectoryInfo(dir);
                    if (!subCompanyList.Contains(di.Parent.FullName))
                        subCompanyList.Add(di.Parent.FullName);
                }
            }

            if (currUser.AccessLevel != 0)
            {
                if (!subCompanyList.Contains(dataDir))
                    subCompanyList.Add(dataDir);
            }
            foreach (string comp in subCompanyList)
            {
                DirectoryInfo di = new DirectoryInfo(comp);
                var data = SummaryByCompany(di.FullName, year, currUser._Plan);
                headCount += (int)data["headCount"];
                totalIn += (double)data["totalIn"];
                totalOut += (double)data["totalOut"];
            }
            result.Add("headCount", headCount);
            result.Add("totalIn", totalIn);
            result.Add("totalOut", totalOut);
            return result;
        }

        /// <summary>
        /// 在给定的公司文件夹中统计该公司信息，不统计其子公司信息
        /// </summary>
        /// <param name="dataDir"></param>
        /// <returns></returns>
        private Dictionary<string, ValueType> SummaryByCompany(string companyDataDir, int year, string currUserPlan)
        {
            string companyName = new DirectoryInfo(companyDataDir).Name;
            int headCount = 0;
            double totalIn = 0;
            double totalOut = 0;
            foreach (var plan in currUserPlan.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string summaryFile = Path.Combine(companyDataDir, plan, companyName + ".xls");
                if (!new FileInfo(summaryFile).Exists) continue;
                ExcelTool et = new ExcelTool(summaryFile, "Sheet1");
                headCount += et.m_main.GetLastRow();
                totalIn = et.GetCostFromJuneToMay(companyDataDir, year, plan);
                string txtFile = Directory.GetFiles(Path.Combine(companyDataDir, plan), "*", SearchOption.TopDirectoryOnly)
                    .Where(file => file.Contains(".txt", StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                if (txtFile == null) throw new Exception("txt file not found");
                FileInfo fi = new FileInfo(txtFile);
                totalOut += Convert.ToDouble(fi.Name.Split("_")[1].Replace(".txt", string.Empty));
            }
            Dictionary<string, ValueType> dic = new Dictionary<string, ValueType>();
            dic.Add("headCount", headCount);
            dic.Add("totalIn", totalIn);
            dic.Add("totalOut", totalOut);
            return dic;
        }

        /// <summary>
        /// 获取公司所有汇总保单，recursive控制是否包括子公司
        /// </summary>
        /// <param name="companyDataDir"></param>
        /// <returns></returns>
        private List<string> GetSummaryFiles(string companyDataDir, bool recursive = false)
        {
            List<string> summaryList = new List<string>();
            DirectoryInfo di = new DirectoryInfo(companyDataDir);
            foreach (var plan in Plans)
            {
                string planDir = Path.Combine(companyDataDir, plan);
                var summaries = Path.Combine(planDir, di.Name + ".xls");
                summaryList.Add(summaries);
            }
            if (recursive)
            {
                var subCompanyList = Directory.GetDirectories(companyDataDir).Where(d =>
                {
                    DirectoryInfo info = new DirectoryInfo(d);
                    return !Plans.Contains(info.Name) && !DateTime.TryParse(info.Name, out DateTime dt);
                });
                foreach (var subComp in subCompanyList)
                {
                    summaryList.AddRange(GetSummaryFiles(companyDataDir, true));
                }
            }
            return summaryList;
        }

        private static readonly object doclocker = new object();


        //[UserLoginFilters]
        //public string ProofTableSingle(string company, string date)
        //{
        //    UserInfoModel currUser = GetCurrentUser();
        //    string companyDir = GetSearchExcelsInDir(company);
        //    List<string> summaries = new List<string>();
        //    DateTime month = DateTime.Parse(date);
        //    bool isCurrentMonth = month.Month == DateTime.Now.Month;
        //    foreach (var plan in Plans)
        //    {
        //        string planDir = Path.Combine(companyDir, plan);
        //        if (!Directory.Exists(planDir)) continue;

        //        //compDir = Directory.GetDirectories(companyDir, comp, SearchOption.AllDirectories).FirstOrDefault();
        //        if (isCurrentMonth)
        //            summaries.AddRange(Directory.GetFiles(planDir, company + ".xls"));
        //        else
        //            summaries.AddRange(Directory.GetFiles(planDir, company + "_" + month.ToString("yyyy-MM") + "_bk.xls"));
        //    }
        //    DataTable dt = new DataTable();
        //    foreach (var summary in summaries)
        //    {
        //        dt.Merge(new ExcelTool(summary, "Sheet1").ExcelToDataTable("Sheet1", true));
        //    }
        //    return JsonConvert.SerializeObject(dt);
        //}

        [UserLoginFilters]
        public string ProofTable(string company, string date, string plan)
        {
            UserInfoModel currUser = GetCurrentUser();
            List<string> plans = new List<string>();
            if (string.IsNullOrEmpty(plan))
            {
                foreach (var item in currUser._Plan.Split(" ", StringSplitOptions.RemoveEmptyEntries))
                {
                    plans.Add(item);
                }
            }
            else
            {
                plans.Add(plan);
            }
            string companyDir = GetSearchExcelsInDir(company);
            List<string> summaries = new List<string>();
            DateTime month = DateTime.Parse(date);
            bool isCurrentMonth = month.Month == DateTime.Now.Month;
            foreach (var p in plans)
            {
                string planDir = Path.Combine(companyDir, p);
                if (!Directory.Exists(planDir)) continue;

                //compDir = Directory.GetDirectories(companyDir, comp, SearchOption.AllDirectories).FirstOrDefault();
                if (isCurrentMonth)
                    summaries.AddRange(Directory.GetFiles(planDir, company + ".xls"));
                else
                    summaries.AddRange(Directory.GetFiles(planDir, company + "_" + month.ToString("yyyy-MM") + "_bk.xls"));
            }
            DataTable dt = new DataTable();
            foreach (var summary in summaries)
            {
                dt.Merge(new ExcelTool(summary, "Sheet1").ExcelToDataTable("Sheet1", true));
            }
            return JsonConvert.SerializeObject(dt);
        }

        [UserLoginFilters]
        public string GenerateProofFile(string company, string date, UserInfoModel currUser, string plan)
        {
            string monthDir = DateTime.Parse(date).ToString("yyyy-MM");
            string template = "";
            template = Path.Combine(_hostingEnvironment.WebRootPath, "templates", $"Insurance_recipet_{plan.Replace("万", "") }.docx");
            string newdoc = Path.Combine(_hostingEnvironment.WebRootPath, "Word", company + "_" + plan + "_" + DateTime.Now.ToString("yyyy-MM-dd-HH-hh-ss-mm") + ".docx");
            XWPFDocument document = null;

            lock (doclocker)
            {
                using (FileStream input = new FileStream(template, FileMode.Open, FileAccess.Read))
                {
                    document = new XWPFDocument(input);
                }
            }

            var ruleTable = document.Tables[0];
            foreach (var row in ruleTable.Rows)
            {
                if (row.GetCell(2).GetText() == "beibaoxianren")
                {
                    row.GetCell(2).Paragraphs[0].ReplaceText("beibaoxianren", company);
                }

                if (row.GetCell(2).GetText() == "shengxiaoriqi")
                {
                    row.GetCell(2).Paragraphs[0].ReplaceText("shengxiaoriqi", $"{DateTime.Parse(date).ToString("yyyy年MM月dd日")}");
                    break;
                }
            }
            string companyName_Abb = string.Empty;
            if (currUser.AccessLevel == 0)
            {
                DataTable comps = DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", company);
                DataRow row = comps.Rows[0];
                companyName_Abb = row["CompanyNameAbb"].ToString();
            }
            else
            {
                companyName_Abb = currUser.CompanyNameAbb;
            }
            //读取表格

            currUser.MyLocker.RWLocker.EnterReadLock();
            try
            {
                int index = 1;
                DataTable dt = new DataTable();
                dt = JsonConvert.DeserializeObject<DataTable>(ProofTable(company, date, plan));
                //foreach (XWPFTable table in document.Tables)
                if (dt == null || dt.Rows.Count <= 0)
                {
                    return string.Empty;
                }
                var table = document.Tables[1];
                foreach (DataRow row in dt.Rows)
                {
                    XWPFTableRow newrow = table.CreateRow();
                    newrow.GetCell(0).SetText(index++.ToString());
                    newrow.GetCell(1).SetText(companyName_Abb);
                    newrow.GetCell(2).SetText(row[2].ToString());
                    newrow.GetCell(3).SetText(row[3].ToString());
                    newrow.GetCell(4).SetText(row[4].ToString());
                    newrow.GetCell(5).SetText(row[5].ToString());
                    newrow.GetCell(6).SetText(row[6].ToString());
                }
                table.SetBottomBorder(XWPFTable.XWPFBorderType.SINGLE, 1, 1, "0 0 0");
                table.SetTopBorder(XWPFTable.XWPFBorderType.SINGLE, 1, 1, "0 0 0");
                table.SetLeftBorder(XWPFTable.XWPFBorderType.SINGLE, 1, 1, "0 0 0");
                table.SetRightBorder(XWPFTable.XWPFBorderType.SINGLE, 1, 1, "0 0 0");
                table.SetInsideHBorder(XWPFTable.XWPFBorderType.SINGLE, 1, 1, "0 0 0");
                table.SetInsideVBorder(XWPFTable.XWPFBorderType.SINGLE, 1, 1, "0 0 0");

                currUser.MyLocker.RWLocker.ExitReadLock();
                using (FileStream output = new FileStream(newdoc, FileMode.Create, FileAccess.ReadWrite))
                {
                    document.Write(output);
                }
                return newdoc;
            }
            finally
            {
                if (currUser.MyLocker.RWLocker.IsReadLockHeld)
                {
                    currUser.MyLocker.RWLocker.ExitReadLock();
                }
            }
        }

        [UserLoginFilters]
        public FileStreamResult GenerateInsuranceRecipet(string company, string date)
        {
            string monthDir = DateTime.Parse(date).ToString("yyyy-MM");
            var currUser = GetCurrentUser();
            List<string> files = new List<string>();
            foreach (var plan in currUser._Plan.Split(" ", StringSplitOptions.RemoveEmptyEntries))
            {
                files.Add(GenerateProofFile(company, date, currUser, plan));
            }
            files.RemoveAll(x => string.IsNullOrEmpty(x));
            string zipFile = Path.Combine(ExcelRoot, "zip_temp.zip");
            if (System.IO.File.Exists(zipFile))
            {
                System.IO.File.Delete(zipFile);
            }
            using (ZipFile zip = ZipFile.Create(zipFile))
            {
                zip.BeginUpdate();
                foreach (var file in files)
                {
                    zip.Add(file, new FileInfo(file).Name);
                }
                zip.CommitUpdate();
                zip.Close();
            }
            FileStream downloadStream = new FileStream(zipFile, FileMode.Open, FileAccess.Read);
            return File(downloadStream, "application/zip", $"{date}_保险凭证.zip");
        }

        [HttpPost]
        public bool MarkAsPaid([FromForm] string ids)
        {
            string company = string.Empty;
            List<string> changedFiles = new List<string>();
            if (ids is null)
            {
                return false;
            }
            try
            {
                string[] info = ids.Split(',');

                foreach (string file in info)
                {
                    string[] excelInfo = file.Split('@');
                    if (excelInfo[1] == excelInfo[6])
                    {
                        return false;
                    }
                }
                foreach (string file in info)
                {
                    if (string.IsNullOrEmpty(file))
                    {
                        continue;
                    }
                    string[] excelInfo = file.Split('#')[1].Split('@');
                    string date = file.Split('#')[0];
                    company = excelInfo[7];
                    string userFolder = GetSearchExcelsInDir(company);
                    string fileName = file.Split('#')[1].Substring(0, file.Split('#')[1].Length - company.Length) + ".xls";
                    string monthDir = Directory.GetDirectories(userFolder, "*", SearchOption.AllDirectories)
                        .Where(_ => Directory.GetFiles(_, fileName, SearchOption.TopDirectoryOnly).Length > 0)
                        .FirstOrDefault();
                    excelInfo[6] = excelInfo[1];
                    string newPath = Utility.ArrayToString(excelInfo, 0, 6, "@");
                    newPath += ".xls";
                    newPath = Path.Combine(monthDir, newPath);
                    string oldFile = Path.Combine(monthDir, fileName);
                    System.IO.File.Move(oldFile, newPath);
                    changedFiles.Add(newPath);
                }
                return true;
            }
            catch (Exception e)
            {
                foreach (var file in changedFiles)
                {
                    var fileInfo = new FileInfo(file);
                    string fileName = fileInfo.Name;
                    string dirPath = fileInfo.Directory.FullName;
                    string[] excelInfo = fileName.Split('@');
                    excelInfo[6] = "0";
                    string oldPath = Path.Combine(dirPath, Utility.ArrayToString(excelInfo, 0, 6, "@") + ".xls");
                    System.IO.File.Move(file, oldPath);
                }
                return false;
            }

        }

        [UserLoginFilters]
        public IActionResult RecipeSummaryByMonth([FromQuery] string name, [FromQuery] int dataYear, [FromQuery] string accountPlan = "")
        {
            DateTime dataFrom = From;
            DateTime dataTo = To;
            if (dataYear != 0)
            {
                dataFrom = new DateTime(dataYear, 6, 1);
                dataTo = new DateTime(dataYear + 1, 5, 31);
            }
            string plan = string.Empty;
            if (string.IsNullOrEmpty(accountPlan))
            {
                plan = CurrentSession.Get<string>("plan");
            }
            else
            {
                plan = accountPlan;
            }
            if (string.IsNullOrEmpty(plan)) return View("Error");
            ViewBag.Plan = plan;
            ViewBag.Company = name;
            ViewBag.DataYear = dataYear;
            ReaderWriterLockSlim r_locker = null;
            bool isSelf = false;
            var currUser = GetCurrentUser();
            if (currUser.CompanyName == name) isSelf = true;
            try
            {
                r_locker = currUser.MyLocker.RWLocker;
                r_locker.EnterReadLock();
                //获取该公司历史表单详细
                List<NewExcel> allMonthlyExcels = new List<NewExcel>();
                if (currUser.ChildAccounts.Count == 0)
                {
                    if (currUser.CompanyName != name)
                    {
                        ViewBag.Msg = "未查询到保单信息";
                        DetailModel dm1 = new DetailModel();
                        dm1.Company = name;
                        dm1.MonthlyExcel = new List<NewExcel>();
                        return View("RecipeSummaryDetail", dm1);
                    }
                }
                string companyName = name;
                string targetCompDir = Directory.GetDirectories(ExcelRoot, companyName, SearchOption.AllDirectories).FirstOrDefault();
                for (DateTime date = dataFrom; date <= dataTo; date = date.AddMonths(1))
                {
                    string monthDir = date.ToString("yyyy-MM");
                    SearchOption so = SearchOption.AllDirectories;
                    string[] dirs;
                    if (isSelf)
                    {
                        so = SearchOption.TopDirectoryOnly;
                        dirs = Directory.GetDirectories(Path.Combine(targetCompDir, plan), monthDir, so);
                    }
                    else
                    {
                        dirs = Directory.GetDirectories(targetCompDir, monthDir, so);
                    }

                    if (dirs.Length == 0) continue;
                    NewExcel excel = new NewExcel();
                    excel.Company = companyName;
                    foreach (string month in dirs)
                    {
                        DirectoryInfo di = new DirectoryInfo(month);
                        if (di.Parent.Name != plan || !di.Exists) continue;
                        var tempexcel = GetMonthlyDetail(month, name);
                        if (tempexcel == null) continue;
                        excel.StartDate = tempexcel.StartDate;
                        excel.EndDate = tempexcel.EndDate;
                        excel.Cost += tempexcel.Cost;
                        excel.HeadCount += tempexcel.HeadCount;
                        excel.Paid += tempexcel.Paid;
                        excel.Unpaid += tempexcel.Unpaid;
                        excel.UploadDate = tempexcel.UploadDate;
                    }
                    if (excel != null && excel.StartDate != null)
                    {
                        allMonthlyExcels.Add(excel);
                    }
                }

                DetailModel dm = new DetailModel();
                dm.Company = name;
                dm.MonthlyExcel = allMonthlyExcels;
                return View("RecipeSummaryDetail", dm);
            }
            catch
            {
                DetailModel dm = new DetailModel();
                dm.Company = name;
                dm.MonthlyExcel = new List<NewExcel>();
                return View("RecipeSummaryDetail", dm);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }

        protected NewExcel GetMonthlyDetail(string dir, string companyName)
        {
            if (Directory.GetFiles(dir).Length <= 0)
            {
                return null;
            }
            NewExcel excel = null;
            int headcount = 0;
            decimal cost = 0;
            decimal unpaid = 0;
            decimal paid = 0;
            foreach (string fileName in Directory.GetFiles(dir))
            {
                FileInfo fi = new FileInfo(fileName);
                if (fi.Name.Replace(fi.Extension, string.Empty) == companyName)
                    continue;

                ExcelTool et = new ExcelTool(fileName, "Sheet1");
                excel = new NewExcel();
                excel.Company = companyName;
                DateTime dt;
                string[] fileinfo = fi.Name.Split('@');
                //DateTime.TryParse(fileinfo[0].Replace(fi.Extension, string.Empty), out dt);
                DateTime.TryParse(new DirectoryInfo(dir).Name, out dt);
                excel.EndDate = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToShortDateString();
                headcount += et.GetEmployeeNumber();
                excel.StartDate = new DateTime(dt.Year, dt.Month, 1).ToShortDateString();
                paid += decimal.Parse(fileinfo[6]);
                unpaid += decimal.Parse(fileinfo[1]) - decimal.Parse(fileinfo[6]);
                cost += decimal.Parse(fileinfo[1]);
            }
            excel.UploadDate = new DirectoryInfo(dir).Name;
            excel.HeadCount = headcount;
            excel.Cost = cost;
            excel.Unpaid = unpaid;
            excel.Paid = paid;
            return excel;
        }
        public FileStreamResult EmployeeDownload()
        {
            try
            {
                string fileName = "加减保模板.xls";//客户端保存的文件名
                string filePath = Path.Combine(Utility.Instance.TemplateFolder, "employee_download.xls");//路径
                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(fs, "text/plain", fileName);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                LogServices.LogService.Log(e.StackTrace);
                return null;
            }
        }

        public FileStreamResult DownloadCompensationDocs()
        {
            try
            {
                string fileName = "理赔材料.rar";//客户端保存的文件名
                string filePath = Path.Combine(Utility.Instance.TemplateFolder, "理赔材料.rar");//路径
                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(fs, "application/x-zip-compressed", fileName);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                LogServices.LogService.Log(e.StackTrace);
                return null;
            }
        }

        public FileStreamResult DownloadJobs()
        {
            try
            {
                string fileName = "平安职业分类表.xls";//客户端保存的文件名
                string filePath = Path.Combine(Utility.Instance.TemplateFolder, "home_download_jobs.xls");//路径
                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(fs, "text/plain", fileName);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                LogServices.LogService.Log(e.StackTrace);
                return null;
            }
        }
        public FileStreamResult DownloadPlan(string plan)
        {
            try
            {
                string fileName = "保障方案.doc";//客户端保存的文件名
                string filePath = Path.Combine(Utility.Instance.TemplateFolder, $"home_download_plan_{plan}.docx");//路径
                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(fs, "text/plain", fileName);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                LogServices.LogService.Log(e.StackTrace);
                return null;
            }
        }

        [UserLoginFilters]
        public FileStreamResult DownloadExcel(string company, string fileName, string date)
        {
            try
            {
                UserInfoModel currUser = GetCurrentUser();
                string[] fileinfo = new FileInfo(fileName).Name.Split("@");
                UserInfoModel uploadUser = DatabaseService.SelectUser(fileinfo[2]);
                if (!uploadUser.UserName.Equals("期初自动流转"))
                {
                    if (!IsChildCompany(currUser, company) && company != currUser.CompanyName)
                        return File(new FileStream(Path.Combine(_hostingEnvironment.WebRootPath, "Excel", "未知错误.txt"), FileMode.Open, FileAccess.Read), "text/plain", "未知错误.txt");
                }

                FileInfo fi = new FileInfo(fileName);
                string downloadName = company + "_" + fi.Name.Split("@")[0] + ".xls";
                string companyDir = string.Empty;
                if (currUser.AccessLevel == 0)
                {
                    companyDir = ExcelRoot;
                }
                else
                {
                    companyDir = GetSearchExcelsInDir(company);
                }
                string filePath = Directory.GetFiles(companyDir, fileName, SearchOption.AllDirectories).FirstOrDefault();//路径
                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(fs, "text/plain", downloadName);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                LogServices.LogService.Log(e.StackTrace);
                return null;
            }
        }
        [UserLoginFilters]
        public JsonResult PreviewTable(string company, string fileName, string date)
        {
            try
            {
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
                LogServices.LogService.Log(e.Message);
                LogServices.LogService.Log(e.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// 删除该公司文件及所有账号
        /// </summary>
        /// <param name="companyName"></param>
        private void RemoveCompanyFiles(string companyName)
        {
            string targetDir = GetSearchExcelsInDir(companyName);
            DirectoryInfo di = new DirectoryInfo(targetDir);
            di.Delete(true);

            var accounts = DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", companyName);
            if (accounts == null) return;
            List<string> account2Delete = new List<string>();
            foreach (DataRow row in accounts.Rows)
            {
                string usrName = row["userName"].ToString();
                var user = DatabaseService.SelectUser(usrName);
                account2Delete.Add(usrName);
                user.SpringAccounts.ForEach(x => account2Delete.Add(x.UserName));
            }

            foreach (var u in account2Delete)
            {
                DatabaseService.Delete("UserInfo", u);
            }
            //foreach (string directory in Directory.GetDirectories(targetDir))
            //{
            //    DirectoryInfo di = new DirectoryInfo(directory);
            //    if (!DateTime.TryParse(di.Name, out DateTime dateTime))
            //    {
            //        RemoveCompanyFiles(di.Name);
            //    }
            //    else
            //    {
            //        Directory.Delete(directory, true);
            //    }
            //}

            //string summaryFile = Path.Combine(targetDir, companyName + ".xls");
            //System.IO.File.Delete(summaryFile);
            //string template = Path.Combine(_hostingEnvironment.WebRootPath, "Excel", "SummaryTemplate.xls");
            //System.IO.File.Copy(template, summaryFile, true);
            //var file = Directory.GetFiles(targetDir).Where(x => new FileInfo(x).Extension.Contains("txt"));
            //string txtPath = Path.Combine(targetDir, companyName + "_0.txt");
            //System.IO.File.Delete(file.First());
            //using (System.IO.File.Create(txtPath))
            //{

            //}
        }

        /// <summary>
        /// 删除所有月份文件夹及文件
        /// 删除该公司汇总表中的所有人员信息
        /// 删除赔付信息
        /// </summary>
        /// <param name="accountName"></param>
        [UserLoginFilters]
        [AdminFilters]
        public IActionResult RemoveAccountData([FromQuery] string accountName)
        {
            var locker = GetCurrentUser().MyLocker.RWLocker;
            try
            {
                if (!locker.IsWriteLockHeld)
                {
                    locker.EnterWriteLock();
                }
                RemoveCompanyFiles(accountName);
            }
            catch
            {
                return Json(false);
            }
            finally
            {
                if (locker.IsWriteLockHeld)
                {
                    locker.ExitWriteLock();
                }
            }
            return Json(true);
        }

        [AdminFilters]
        [UserLoginFilters]
        public IActionResult DeleteReceipt(string company, string fileName, string startDate, string plan)
        {
            var locker = (Utility.LockerList.Where(_ => _.LockerCompany == company).FirstOrDefault() as ReaderWriterLockerWithName).RWLocker;
            try
            {
                if (fileName.Contains("期初自动流转"))
                {
                    return Json("该保单为期初自动流转保单，无法删除");
                }


                if (!locker.IsWriteLockHeld)
                {
                    locker.EnterWriteLock();
                }

                string[] fileinfo = fileName.Split("@");
                string companyDir = string.Empty;
                var dateStr = startDate.Split('/');
                DateTime dt = new DateTime(Convert.ToInt32(dateStr[0]), Convert.ToInt32(dateStr[1]), Convert.ToInt32(dateStr[2]));
                if (dt.Year < From.Year)
                {
                    string historyDir = Path.Combine(ExcelRoot, "历年归档");
                    companyDir = Directory.GetDirectories(historyDir, company, SearchOption.AllDirectories).FirstOrDefault();
                    //companyDir = Directory.GetDirectories(dir, dt.ToString("yyyy-MM"), SearchOption.AllDirectories).FirstOrDefault();
                }
                else
                {
                    companyDir = GetSearchExcelsInDir(company);
                }

                DateTime start = Convert.ToDateTime(startDate);
                string targetFilePath = Path.Combine(companyDir, plan, start.ToString("yyyy-MM"), fileName);
                ExcelTool targetExcel = new ExcelTool(targetFilePath, "Sheet1");
                ExcelTool summary = new ExcelTool(Path.Combine(companyDir, plan, company + ".xls"), "sheet1");
                DataTable targetExcelTable = targetExcel.ExcelToDataTable("Sheet1", true);
                DataTable summaryTable = summary.ExcelToDataTable("Sheet1", true);
                DateTime summaryStartDate = new DateTime();
                if (summaryTable.Rows.Count >= 1)
                {
                    summaryStartDate = Convert.ToDateTime(summaryTable.Rows[0][6].ToString());
                }
                else
                {
                    return Json("当前无人员参保，无法删除");
                }

                if (start.Year != summaryStartDate.Year || (start.Year == summaryStartDate.Year && start.Month != summaryStartDate.Month))
                {
                    return Json("仅能删除当月保单");
                }

                List<int> rowsToRemove = new List<int>();
                if (fileinfo[3] == "Add")
                {
                    foreach (DataRow row in targetExcelTable.Rows)
                    {
                        foreach (DataRow summaryrow in summaryTable.Rows)
                        {
                            if (summaryrow[3].ToString() == row[1].ToString())
                            {
                                rowsToRemove.Add(summaryTable.Rows.IndexOf(summaryrow));
                                break;
                            }
                        }
                    }

                    if (rowsToRemove.Count > 0)
                    {
                        var sortedlist = rowsToRemove.OrderByDescending(x => x);
                        foreach (var item in sortedlist)
                        {
                            summaryTable.Rows.RemoveAt(item);
                        }
                    }

                }
                else if (fileinfo[3] == "Sub")
                {
                    //TODO: Add removed employees back to summary table
                    //之前月份的保单是否允许删除，因为已归档l;;l;;
                    foreach (DataRow row in targetExcelTable.Rows)
                    {
                        DataRow newrow = summaryTable.NewRow();
                        newrow[0] = summaryTable.Rows.Count + 1;
                        newrow[1] = company;
                        newrow[2] = row[0];
                        newrow[3] = row[1];
                        newrow[4] = row[2];
                        newrow[5] = row[3];
                        newrow[6] = row[4];
                        summaryTable.Rows.Add(newrow);
                    }
                }
                summary.DatatableToExcel(summaryTable);
                targetExcel.Dispose();
                System.IO.File.Delete(targetFilePath);

                return Json(true);
            }
            catch
            {
                return NotFound();
            }
            finally
            {
                if (locker.IsWriteLockHeld)
                {
                    locker.ExitWriteLock();
                }
            }
        }

        [HttpGet]
        //[AdminFilters]
        public JsonResult UpdateCaseCost([FromQuery] string id, [FromQuery] string cost)
        {
            if (!double.TryParse(cost, out double price)) return Json("金额不正确");
            bool res = DatabaseService.UpdateOneColumn("CaseInfo", "CaseId", id, "Price", price);
            res = DatabaseService.UpdateOneColumn("CaseInfo", "CaseId", id, "State", "已结案");
            return res ? Json("成功") : Json("失败");
        }

        public IActionResult ViewCase()
        {
            var result = DatabaseService.Select("CaseInfo");
            if (result == null || result.Rows.Count == 0) return Json("未找到报案信息");
            foreach (DataRow row in result.Rows)
            {
                row[4] = DateTime.Parse(row[4].ToString()).Date;
            }
            SearchPeopleModel model = new SearchPeopleModel();
            model.CaseTable = result;
            return View("SearchPeople", model);
        }

        [HttpPost]
        public IActionResult SubmitCase([FromForm] DateTime casedate, [FromForm] string person, [FromForm] string detail)
        {
            if (casedate.Year < 2000 || string.IsNullOrEmpty(person) || string.IsNullOrEmpty(detail)) return Json("信息不完整");
            string caseDir = Path.Combine(ExcelRoot, "报案信息");
            CaseModel caseModel = new CaseModel();
            caseModel.Wounded = person;
            caseModel.Date = new DateTime(casedate.Year, casedate.Month, casedate.Day);
            caseModel.Detail = detail;
            caseModel.CaseId = Guid.NewGuid().ToString();
            caseModel.State = "未结案";
            if (DatabaseService.InsertStory("CaseInfo", caseModel))
                return Json(true);
            else
                return Json(false);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }


}
