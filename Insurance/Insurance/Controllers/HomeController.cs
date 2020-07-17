using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Constraints;
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
        private readonly int idCol = 3;
        private readonly int nameCol = 2;
        private static bool isSignExpired { get; set; }
        private static long expiredTime { get; set; }
        public DataTable SearchResult { get; set; }

        public HomeController(IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }
        [UserLoginFilters]
        public IActionResult Index(HomePageModel model)
        {
            try
            {
                if (SessionService.IsUserLogin(HttpContext))
                {
                    ViewBag.UserName = GetCurrentUser().UserName;
                }
                Response.Cookies.Append("invalidFlag", DateTime.UtcNow.ToString(),
                new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddDays(7) });
                return View(model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }

        }
        [UserLoginFilters]
        public IActionResult HistoricalList(HistoricalModel model)
        {
            try
            {
                model.CompanyList = GetAllChildAccounts();
                return View("HistoricalList", model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }

        }

        /// <summary>
        /// 判断当前用户是否为公司的上级账号
        /// </summary>
        /// <param name="user"></param>
        /// <param name="companyname"></param>
        /// <returns></returns>
        private bool IsAncestor(UserInfoModel user, string companyname)
        {
            ConcurrentBag<bool> results = new ConcurrentBag<bool>();
            foreach (var child in user.ChildAccounts)
            {
                if (child.ChildAccounts != null && child.ChildAccounts.Count > 0)
                {
                    var isChild = IsAncestor(child, companyname);
                    if (isChild)
                    {
                        results.Add(isChild);
                        break;
                    }

                }
                if (child.CompanyName.Equals(companyname, StringComparison.CurrentCultureIgnoreCase))
                {
                    results.Add(true);
                    break;
                }
            }

            return results.Contains(true);
        }

        [UserLoginFilters]
        public IActionResult CompanyHisitoryByMonth([FromQuery]string name)
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
                if (!IsAncestor(currUser, name) && currUser.CompanyName != name)
                {
                    return View("Error");
                }
                string companyName = name;
                string targetCompanyDir;

                string currUserDir = GetCurrentUserRootDir();

                if (isSelf)
                {
                    targetCompanyDir = currUserDir;
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
                    double cost = 0;
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
                            cost += Convert.ToDouble(fileinfo[1]);
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
                r_locker.EnterReadLock();
                DataTable tbl_summary = new DataTable();
                string targetDirectory = _hostingEnvironment.WebRootPath;
                string template = Path.Combine(targetDirectory, "Excel", "AllEmployeeTemplate.xls");
                allemployees = Path.Combine(targetDirectory, "Excel", "all_employees.xls");
                System.IO.File.Copy(template, allemployees, true);
                foreach (string company in Directory.GetDirectories(Path.Combine(targetDirectory, "Excel")))
                {
                    DirectoryInfo di = new DirectoryInfo(company);
                    string comp_summary = Path.Combine(company, di.Name + ".xls");
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
        public IActionResult CompanyHistory([FromQuery]string date, [FromQuery]string name)
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
                string currUserRootDir = GetCurrentUserRootDir();
                currUserCompany = new DirectoryInfo(currUserRootDir).Name;

                List<NewExcel> allexcels = new List<NewExcel>();
                if (!GetChildrenCompanies(currUserCompany).Contains(name) && currUser.CompanyName != name) return View("Error");
                //string date = "2020/3/1";
                DateTime dt1 = DateTime.Parse(date);
                string month = dt1.ToString("yyyy-MM");
                string companyName = name;
                List<string> excels = new List<string>();
                string companyDir = GetSearchExcelsInDir(companyName);
                SearchOption so = SearchOption.AllDirectories;
                if (isSelf) so = SearchOption.TopDirectoryOnly;
                var monthDirs = Directory.GetDirectories(companyDir, month, so);

                if (monthDirs.Length > 0)
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
                    excel.Company = Directory.GetParent(Directory.GetParent(fileName).FullName).Name;
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
                        excel.Cost = Convert.ToDouble(fileinfo[1]);
                    }
                    else //若结束时间不为空，则为减员文档
                    {
                        excel.Mode = "减保";
                        excel.StartDate = et.GetCellText(1, 4, ExcelTool.DataType.String);
                        excel.EndDate = et.GetCellText(1, 5, ExcelTool.DataType.String);
                        excel.Cost = Convert.ToDouble(fileinfo[1]);
                    }
                    allexcels.Add(excel);
                }
                allexcels.Sort((l, r) =>
                {
                    if (DateTime.Parse(l.UploadDate) <= DateTime.Parse(r.UploadDate))
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
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
                model.CompanyList = GetAllChildAccounts();
                return View(model);
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
        public JsonResult StartRenew([FromForm]RenewModel test)
        {
            ReaderWriterLockerWithName locker = null;
            string summary = string.Empty;
            string summary_backup = string.Empty;
            DateTime now = DateTime.Now.Date;
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

                if (GetCurrentUser() == null)
                {
                    return Json("请先登录");
                }

                if (GetCurrentUser().AccessLevel != 0 && GetCurrentUser().CompanyName != test.CompanyName)
                {
                    return Json("当前账号无权进行该操作");
                }
                //==================写锁====================
                if (GetCurrentUser().AccessLevel == 0)
                {
                    locker = Utility.GetCompanyLocker(test.CompanyName);
                }
                if (locker != null)
                {
                    locker.RWLocker.EnterWriteLock();
                }
                //备份当前summary文件，生成新的summary,生效日期为每月1号
                summary = Path.Combine(_hostingEnvironment.WebRootPath, "Excel", test.CompanyName, test.CompanyName + ".xls");
                summary_backup = Path.Combine(_hostingEnvironment.WebRootPath, "Excel", test.CompanyName, test.CompanyName + "_" + DateTime.Now.ToString("yyyy-MM") + "_bk.xls");
                System.IO.File.Copy(summary, summary_backup, true);
                ExcelTool et = new ExcelTool(summary, "Sheet1");
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
                GenerateNewExcelForRenew(test.CompanyName);
                return Json("续保成功");
            }
            catch
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

        private bool GenerateNewExcelForRenew(string company)
        {
            UserInfoModel currUser = null;
            string summary_bk = string.Empty;
            try
            {
                currUser = GetCurrentUser();
                //=========================================进入读锁=====================================================//
                currUser.MyLocker.RWLocker.EnterReadLock();

                string companyDir = Path.Combine(_hostingEnvironment.WebRootPath, "Excel", company);
                string summaryPath = Path.Combine(companyDir, company + ".xls");

                string monDir = Path.Combine(companyDir, DateTime.Now.AddMonths(1).ToString("yyyy-MM"));
                DateTime now = DateTime.Now;
                summary_bk = Path.Combine(companyDir, company + $"_{now.ToString("yyyy-MM")}_" + ".xls");
                DateTime startdate = DateTime.Parse(now.AddMonths(1).ToString("yyyy-MM-01"));
                System.IO.File.Copy(summaryPath, summary_bk, true); //备份当月总表
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
                return true;
            }
            catch (Exception e)
            {
                System.IO.File.Delete(summary_bk);
                throw new Exception();
            }
            finally
            {
                if (currUser != null && currUser.MyLocker.RWLocker.IsReadLockHeld)
                    currUser.MyLocker.RWLocker.ExitReadLock(); //退出写锁
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
            string company = string.Empty;
            DataTable result = new DataTable();
            result.Columns.AddRange(
                new DataColumn[] {
                    new DataColumn("公司"),
                    new DataColumn("当前保单月份"),
                    new DataColumn("续保人数"),
                    new DataColumn("总保费"),
                    new DataColumn("1-4类人数"),
                    new DataColumn("1-4类保费"),
                    new DataColumn("续保后到期日"),
                }
                );
            if (GetCurrentUser() != null)
            {
                company = GetCurrentUser().CompanyName;
            }
            RenewModel rm = new RenewModel();
            string companyDir = GetSearchExcelsInDir(company);
            string summary = Path.Combine(companyDir, company + ".xls");
            ExcelTool et = new ExcelTool(summary, "Sheet1");
            int headcount = et.GetEmployeeNumber(); //总人数
            double unitprice = GetCurrentUser().UnitPrice;
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
                errorrow["当前保单月份"] = "无人参保";
                errorrow["续保人数"] = 0;
                errorrow["总保费"] = 0;
                errorrow["1-4类人数"] = 0;
                errorrow["1-4类保费"] = 0;
                errorrow["续保后到期日"] = string.Empty;
                result.Rows.Add(errorrow);
                rm.MonthInfo = result;
                return View(rm);
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
            newrow["当前保单月份"] = currentMonth;
            newrow["续保人数"] = headcount;
            newrow["总保费"] = totalcost;
            newrow["1-4类人数"] = headcount_1_4;
            newrow["1-4类保费"] = cost_1_4;
            newrow["续保后到期日"] = nextMonth.ToString("yyyy-MM-dd");
            result.Rows.Add(newrow);
            rm.MonthInfo = result;
            return View(rm);
        }

        [HttpGet]
        [UserLoginFilters]
        public IActionResult SearchPeople(SearchPeopleModel model)
        {
            HttpContext.Session.Set<List<Employee>>("searchResult", null);
            try
            {
                return View(model);
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return View("Error");
            }
        }

        [HttpGet]
        [UserLoginFilters]
        public JsonResult ShowPersonalDetail([FromQuery]string comp, [FromQuery]string id)
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
                    string comp = fi.Directory.Parent.Name;
                    string uploader = excelInfo[2];
                    ExcelTool et = new ExcelTool(file, "Sheet1");
                    temp = et.SelectPeopleByNameAndID(em_name, nameCol, em_id, idCol);
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

        private void CacheSearchResult(DataTable table)
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
            HttpContext.Session.Set("searchResult", employees);
        }

        /// <summary>
        /// 列出所有公司的保费汇总信息
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [UserLoginFilters]
        public IActionResult GetAllRecipeSummary(RecipeSummaryModel model)
        {
            try
            {
                var currUser = GetCurrentUser();
                model.CompanyList = GetChildrenCompanies(currUser).ToList();
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
        public IActionResult BanlanceAccount([FromQuery]string companyName)
        {
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
            foreach (var dir in targetDirs)
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    var excel = GetExcelInfo(file, companyName);
                    if (excel != null && excel.Cost != excel.Paid)
                    {
                        detailModel.Excels.Add(excel);
                    }
                }
            }
            detailModel.Company = companyName;
            return View("RecipeSummaryDetail", detailModel);
        }

        [UserLoginFilters]
        public IActionResult RecipeSummary([FromQuery]string date, [FromQuery]string name)
        {
            //获取该公司历史表单详细
            DetailModel dm = new DetailModel();
            List<NewExcel> allexcels = new List<NewExcel>();
            bool isSelf = false;
            var currUser = GetCurrentUser();
            if (currUser.ChildAccounts.Count == 0)
            {
                if (currUser.CompanyName != name)
                {
                    return View("Error");
                }
            }
            if (currUser.CompanyName == name) isSelf = true;
            //string date = "2020/3/1";
            DateTime dt1 = DateTime.Parse(date);
            string month = dt1.ToString("yyyy-MM");
            string companyName = name;
            string targetDirectory = GetSearchExcelsInDir(companyName);
            List<string> excels = new List<string>();

            SearchOption so = SearchOption.AllDirectories;
            if (isSelf) so = SearchOption.TopDirectoryOnly;

            string[] monthDirs = Directory.GetDirectories(targetDirectory, month, so);

            foreach (var monthDir in monthDirs)
            {
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
                NewExcel excel = GetExcelInfo(fileName, companyName);
                if (excel != null)
                {
                    allexcels.Add(excel);
                }
            }
            dm.Company = name;
            dm.Excels = allexcels;
            return View("RecipeSummaryDetail", dm);
        }

        private NewExcel GetExcelInfo(string fileName, string companyName)
        {
            FileInfo fi = new FileInfo(fileName);
            if (fi.Name.Replace(fi.Extension, string.Empty) == companyName)
                return null;

            NewExcel excel = new NewExcel();
            excel.FileName = fi.Name;
            excel.Company = companyName;

            ExcelTool et = new ExcelTool(fileName, "Sheet1");
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
                excel.Cost = Convert.ToDouble(fileinfo[1]);
            }
            else
            {
                excel.Mode = "减保";
                excel.EndDate = et.GetCellText(1, 5, ExcelTool.DataType.String);
                excel.Cost = Convert.ToDouble(fileinfo[1]);
            }
            excel.Paid = Convert.ToDouble(fileinfo[6]);
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
            foreach (string monthDir in Directory.GetDirectories(companyDir, Convert.ToDateTime(date).ToString("yyyy-MM"), SearchOption.AllDirectories))
            {
                if (Directory.Exists(monthDir))
                {
                    foreach (string excel in Directory.GetFiles(monthDir))
                    {
                        FileInfo fi = new FileInfo(excel);
                        string str_uploadDate = fi.Name.Split('@')[0];
                        if (DateTime.TryParse(str_uploadDate, out DateTime uploadDate))
                        {
                            summary.GainData(excel);
                        }
                    }
                }
            }
            summary.RemoveDuplicate();
            summary.CreateAndSave(summary_file);
            System.IO.File.Delete(summary_file_temp);
            return File(new FileStream(summary_file, FileMode.Open, FileAccess.Read), "text/plain", $"{company}_{exportStart.ToString("yyyy-MM")}_入离职汇总表格.xls");
        }

        private static readonly object doclocker = new object();

        [UserLoginFilters]
        public FileStreamResult GenerateInsuranceRecipet(string company, string date)
        {
            UserInfoModel currUser = GetCurrentUser();
            string monthDir = DateTime.Parse(date).ToString("yyyy-MM");
            string template = Path.Combine(_hostingEnvironment.WebRootPath, "templates", "Insurance_recipet.docx");
            string companyDir = GetSearchExcelsInDir(company);
            List<string> childCompanies = GetChildrenCompanies(company).ToList();
            childCompanies.Add(company);
            List<string> summaries = new List<string>();
            foreach (var comp in childCompanies)
            {
                string compDir = GetSearchExcelsInDir(comp);
                summaries.Add(Path.Combine(compDir, comp + ".xls"));
            }
            string newdoc = Path.Combine(_hostingEnvironment.WebRootPath, "Word", company + DateTime.Now.ToString("yyyy-MM-dd-HH-hh-ss-mm") + ".docx");

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

            //读取表格
            UserInfoModel user = GetCurrentUser();
            user.MyLocker.RWLocker.EnterReadLock();
            int index = 1;
            DataTable dt = new DataTable();
            foreach (var summary in summaries)
            {
                dt.Merge(new ExcelTool(summary, "Sheet1").ExcelToDataTable("Sheet1", true));
            }
            //foreach (XWPFTable table in document.Tables)

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

            GetCurrentUser().MyLocker.RWLocker.ExitReadLock();
            using (FileStream output = new FileStream(newdoc, FileMode.Create, FileAccess.ReadWrite))
            {
                document.Write(output);
            }

            FileStream downloadStream = new FileStream(newdoc, FileMode.Open, FileAccess.Read);
            return File(downloadStream, "text/plain", $"{company}_{monthDir}_保险凭证.docx");
        }

        [HttpPost]
        public bool MarkAsPaid([FromForm]string ids)
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
        public IActionResult RecipeSummaryByMonth([FromQuery]string name)
        {
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
                        return View("Error");
                    }
                }
                string companyName = name;
                string targetCompDir = Directory.GetDirectories(ExcelRoot, companyName, SearchOption.AllDirectories).FirstOrDefault();

                for (DateTime date = From; date <= To; date = date.AddMonths(1))
                {
                    string monthDir = date.ToString("yyyy-MM");
                    SearchOption so = SearchOption.AllDirectories;
                    if (isSelf)
                    {
                        so = SearchOption.TopDirectoryOnly;
                    }
                    var dirs = Directory.GetDirectories(targetCompDir, monthDir, so);
                    if (dirs.Length == 0) continue;
                    NewExcel excel = new NewExcel();
                    excel.Company = companyName;
                    foreach (string month in dirs)
                    {
                        var tempexcel = GetMonthlyDetail(month, name);
                        excel.StartDate = tempexcel.StartDate;
                        excel.EndDate = tempexcel.EndDate;
                        excel.Cost += tempexcel.Cost;
                        excel.HeadCount += tempexcel.HeadCount;
                        excel.Paid += tempexcel.Paid;
                        excel.Unpaid += tempexcel.Unpaid;
                        excel.UploadDate = tempexcel.UploadDate;
                    }
                    if (excel != null)
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

        private NewExcel GetMonthlyDetail(string dir, string companyName)
        {
            if (Directory.GetFiles(dir).Length <= 0)
            {
                return null;
            }
            NewExcel excel = null;
            int headcount = 0;
            double cost = 0;
            double unpaid = 0;
            double paid = 0;
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
                paid += Convert.ToDouble(fileinfo[6]);
                unpaid += Convert.ToDouble(fileinfo[1]) - Convert.ToDouble(fileinfo[6]);
                cost += Convert.ToDouble(fileinfo[1]);
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
        public FileStreamResult DownloadPlan()
        {
            try
            {
                string fileName = "保障方案.doc";//客户端保存的文件名
                string filePath = Path.Combine(Utility.Instance.TemplateFolder, "home_download_plan.doc");//路径
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
                companyDir = GetCurrentUserRootDir();
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

        private void RemoveCompanyFiles(string companyName)
        {
            string targetDir = GetSearchExcelsInDir(companyName);

            foreach (string directory in Directory.GetDirectories(targetDir))
            {
                DirectoryInfo di = new DirectoryInfo(directory);
                if (!DateTime.TryParse(di.Name, out DateTime dateTime))
                {
                    RemoveCompanyFiles(di.Name);
                }
                else
                {
                    Directory.Delete(directory, true);
                }
            }

            string summaryFile = Path.Combine(targetDir, companyName + ".xls");
            System.IO.File.Delete(summaryFile);
            string template = Path.Combine(_hostingEnvironment.WebRootPath, "Excel", "SummaryTemplate.xls");
            System.IO.File.Copy(template, summaryFile, true);
            var file = Directory.GetFiles(targetDir).Where(x => new FileInfo(x).Extension.Contains("txt"));
            string txtPath = Path.Combine(targetDir, companyName + "_0.txt");
            System.IO.File.Delete(file.First());
            using (System.IO.File.Create(txtPath))
            {

            }
        }

        /// <summary>
        /// 删除所有月份文件夹及文件
        /// 删除该公司汇总表中的所有人员信息
        /// 删除赔付信息
        /// </summary>
        /// <param name="accountName"></param>
        [UserLoginFilters]
        [AdminFilters]
        public IActionResult RemoveAccountData([FromQuery]string accountName)
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
        public IActionResult DeleteReceipt(string company, string fileName, string startDate)
        {
            var locker = GetCurrentUser().MyLocker.RWLocker;
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
                string companyDir = GetSearchExcelsInDir(company);
                DateTime start = Convert.ToDateTime(startDate);
                string targetFilePath = Path.Combine(companyDir, start.ToString("yyyy-MM"), fileName);
                ExcelTool targetExcel = new ExcelTool(targetFilePath, "Sheet1");
                ExcelTool summary = new ExcelTool(Path.Combine(companyDir, company + ".xls"), "sheet1");
                DataTable targetExcelTable = targetExcel.ExcelToDataTable("Sheet1", true);
                DataTable summaryTable = summary.ExcelToDataTable("Sheet1", true);
                DateTime summaryStartDate = new DateTime();
                if (summaryTable.Rows.Count > 1)
                {
                    summaryStartDate = Convert.ToDateTime(summaryTable.Rows[1][6].ToString());
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


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
