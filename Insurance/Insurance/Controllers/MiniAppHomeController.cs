using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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
        private static readonly int idCol = 3;
        private static readonly int nameCol = 2;

        public MiniAppHomeController(IHostingEnvironment hostingEnvironment, IHttpContextAccessor httpContextAccessor) : base(hostingEnvironment, httpContextAccessor)
        {

        }

        public IActionResult MiniHistoricalList(string openId)
        {

            try
            {
                MiniSession(openId);
                var user = GetCurrentUser();
                if (user.AllowCreateAccount != "1")
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

        public IActionResult MiniSearchPeople(SearchPeopleModel model, string openId)
        {
            MiniSession(openId);
            CurrentSession.Set<List<Employee>>("searchResult", null);
            try
            {
                return View(nameof(MiniSearchPeople), model);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return View("Error");
            }
        }

        public IActionResult MiniViewCase(string openId)
        {
            MiniSession(openId);
            var result = DatabaseService.Select("CaseInfo");
            if (result == null || result.Rows.Count == 0) return Json("未找到报案信息");
            foreach (DataRow row in result.Rows)
            {
                row[4] = DateTime.Parse(row[4].ToString()).Date;
            }
            SearchPeopleModel model = new SearchPeopleModel();
            model.CaseTable = result;
            return View("MiniSearchPeople", model);
        }
        public IActionResult MiniRecipeSummaryByMonth([FromQuery] string name, string openid, [FromQuery] string accountPlan = "")
        {
            MiniSession(openid);
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
            ViewBag.AbbrCompName = DatabaseService.CompanyAbbrName(name);
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
                        return View("MiniRecipeSummaryDetail", dm1);
                    }
                }
                string companyName = name;
                string targetCompDir = Directory.GetDirectories(ExcelRoot, companyName, SearchOption.AllDirectories).FirstOrDefault();
                for (DateTime date = From; date <= To; date = date.AddMonths(1))
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
                return View("MiniRecipeSummaryDetail", dm);
            }
            catch
            {
                DetailModel dm = new DetailModel();
                dm.Company = name;
                dm.MonthlyExcel = new List<NewExcel>();
                return View("MiniRecipeSummaryDetail", dm);
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }
        public IActionResult MiniGetTargetPlanData(string plan, string openId)
        {
            try
            {
                MiniSession(openId);
                ViewBag.Plan = plan;
                RecipeSummaryModel model = new RecipeSummaryModel();
                var currUser = GetCurrentUser();
                model.CompanyList = GetChildrenCompanies(currUser, plan).ToList();
                model.CompanyList = model.CompanyList.OrderBy(x => x.Name).ToList();
                CurrentSession.Set("plan", plan);
                return View("MiniRecipeSummary", model);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return View("Error");
            }
        }

        public IActionResult MiniBanlanceAccount([FromQuery] string companyName, string openId)
        {
            MiniSession(openId);
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
                    var excel = GetExcelInfo(file, companyName, plan);
                    if (excel != null && excel.Cost != excel.Paid)
                    {
                        detailModel.Excels.Add(excel);
                    }
                }
            }
            detailModel.Company = companyName;
            ViewBag.Page = "未结算汇总";
            return View("MiniRecipeSummaryDetail", detailModel);
        }

        [HttpGet]
        public string MiniProofTable(string company, string date, string openid)
        {
            MiniSession(openid);
            UserInfoModel currUser = GetCurrentUser();
            string companyDir = GetSearchExcelsInDir(company);
            List<string> summaries = new List<string>();
            DateTime month = DateTime.Parse(date);
            bool isCurrentMonth = month.Month == DateTime.Now.Month;
            foreach (var plan in Plans)
            {
                string planDir = Path.Combine(companyDir, plan);
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
            if (dt.Rows.Count > 0)
            {
                var dt2 = dt.DefaultView.ToTable(false, "姓名", "身份证", "生效日期", "离职日期");
                dt2.Columns[2].ColumnName = "生效";
                dt2.Columns[3].ColumnName = "离职";
                return JsonConvert.SerializeObject(dt2);
            }
            dt.Columns.Add(new DataColumn());
            DataRow r = dt.NewRow();
            r[0] = "无数据";
            dt.Rows.Add(r);
            return JsonConvert.SerializeObject(dt);
        }

        public IActionResult MiniRecipeSummary([FromQuery] string date, [FromQuery] string name, string openid)
        {
            MiniSession(openid);
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
                return View("MiniRecipeSummaryDetail", dm);
            }
            foreach (string fileName in excels)
            {
                NewExcel excel = GetExcelInfo(fileName, companyName, plan);
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
            return View("MiniRecipeSummaryDetail", dm);
        }
        public IActionResult MiniGetAllRecipeSummary(string openid)
        {
            try
            {
                MiniSession(openid);
                var currUser = GetCurrentUser();
                if (currUser.AllowCreateAccount != "1")
                {
                    return RedirectToAction("MiniRecipeSummaryByMonth", new { openId = openid, accountPlan = currUser._Plan, name = currUser.CompanyName });
                }
                SummaryModel sm = new SummaryModel();
                sm.PlanList = new List<Plan>();
                foreach (var plan in Plans)
                {
                    Plan p = new Plan();
                    p.Name = plan;
                    var comp = GetChildrenCompanies(currUser, plan);
                    p.TotalCost = comp.Sum(x => x.TotalCost);
                    p.TotalPaid = comp.Sum(x => x.CustomerAlreadyPaid);
                    p.HeadCount = comp.Sum(x => x.EmployeeNumber);
                    sm.PlanList.Add(p);
                }

                CurrentSession.Set("plan", string.Empty);
                return View("MiniRecieptPlans", sm);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return View("Error");
            }
        }
        public IActionResult MiniSearch(string em_name, string em_id, string open_id)
        {
            MiniSession(open_id);
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
                    string compAbbr = DatabaseService.CompanyAbbrName(comp);
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
                            string history = string.Join('%', uploadtime, mode, row["start_date"], row["end_date"], compAbbr, uploader);
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
                return View("MiniSearchPeople", model);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return View("Error");
            }
            finally
            {
                currUser.MyLocker.RWLocker.ExitReadLock();
            }
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
                DataTable result = excelTool.ExcelToDataTable("Sheet1", false);
                DataRow firstRow = result.Rows[0];
                for (int i = 0; i < result.Columns.Count; i++)
                {
                    if (firstRow[i].ToString() == "保障开始时间") { result.Columns.RemoveAt(i); i--; };
                    if (firstRow[i].ToString() == "保障结束时间") { result.Columns.RemoveAt(i); i--; };
                    if (firstRow[i].ToString() == "职业类别") { result.Columns.RemoveAt(i); i--; };
                }

                return Json(result);
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
