using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using VirtualCredit.LogServices;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit
{
    public class VC_ControllerBase : Controller
    {
        protected string summaryFilePath;
        public static string ExcelDirectory;
        public readonly int idCol = 1;
        public readonly int jobCol = 2;
        public static ConcurrentDictionary<string, string> CachedCompanyDir = new ConcurrentDictionary<string, string>();
        protected IHostingEnvironment _hostingEnvironment;
        public static List<string> Plans = new List<string> { "30万", "60万", "80万" };
        public static long CustomersCount = 0;
        public static string ExcelRoot = Utility.Instance.ExcelRoot;
        protected ImageTool _imageTool;
        public UserInfoModel currUser_temp;
        public string RoleId
        {
            get
            {
                return HttpContext.Session.Get<string>("RoleId");
            }
            set
            {
                HttpContext.Session.Set<string>("RoleId", value);
            }
        }

        public static DateTime From { get; set; }
        public static DateTime To { get; set; }
        public double ScreenWidth { get; set; }
        public double ScreenHeight { get; set; }
        public VC_ControllerBase() : this(null)
        {

        }
        public VC_ControllerBase(HttpContext context)
        {

            if (context != null)
                _imageTool = new ImageTool(context);


            int y = DateTime.Now.Year;
            if (DateTime.Now.Month < 6)
            {
                From = new DateTime(y - 1, 6, 1);
                To = new DateTime(y, 5, 31);
            }
            else
            {
                From = new DateTime(y, 6, 1);
                To = new DateTime(y + 1, 5, 31);
            }
        }

        public override ViewResult View(string viewName)
        {
            return base.View(viewName);
        }

        public override ViewResult View(object model)
        {
            AdaptModel(model as ViewModelBase);
            return base.View(model);
        }

        public override ViewResult View(string viewName, object model)
        {
            if (model != null)
                AdaptModel(model as ViewModelBase);
            return base.View(viewName, model);
        }


        ///// <summary>
        /////计算某个文件的保费
        ///// </summary>
        ///// <param name="fileName"></param>
        ///// <param name="monthlyPrice"></param>
        ///// <returns></returns>
        //public decimal CalculatePriceFromFileName(string fileName, decimal monthlyPrice)
        //{
        //    int effecttiveDays = 0;
        //    ExcelTool et = new ExcelTool(fileName, "sheet1");
        //    DataTable dt = et.ExcelToDataTable("sheet1", true);
            
        //    string[] info = fileName.Split('@');
        //    try
        //    {
        //        effecttiveDays = int.Parse(info[7]);
        //    }
        //    catch
        //    {

        //    }
        //    if (effecttiveDays == 0)
        //    {
        //        return decimal.Parse(info[1]);
        //    }
        //    else
        //    {

        //    }
        //}

        /// <summary>
        /// 计算每个人的应退保费
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="price">每月保费定价</param>
        /// <param name="effectiveDays"></param>
        /// <returns></returns>
        protected double CalculateSubPrice(DateTime start, DateTime end, double price, int effectiveDays)
        {
            //对于退保人员，计算退费
            double unitPrice = price / DateTime.DaysInMonth(end.Year, end.Month); //当月每天保费，元/天
            double payback = unitPrice * effectiveDays;
            return payback;
        }

        protected int CalculateEffectiveDays(DateTime start, DateTime end, double price)
        {
            //对于退保人员，计算退费
            int effectiveDays = end.Day - start.Day + 1; //当月已生效天数
            effectiveDays = (DateTime.DaysInMonth(end.Year, end.Month) - start.Day + 1) - effectiveDays;//
            return effectiveDays;
        }

        public List<Employee> ValidateExcel(FileStream formFile, string sheetName, string mode, string companyName, string plan)
        {
            formFile.Position = 0;
            ReaderWriterLockSlim r_locker = null;
            string companyDir = GetSearchExcelsInDir(companyName);
            var currUser = GetCurrentUser();
            var result = new List<Employee>();
            try
            {
                r_locker = currUser.MyLocker.RWLocker;
                r_locker.EnterReadLock();
                string summaryPath = Path.Combine(companyDir, plan, companyName + ".xls");
                if (!System.IO.File.Exists(summaryPath))
                {
                    result.Add(
                    new Employee()
                    {
                        Valid = false,
                        Name = $"{companyName}尚未开通{plan}账号"
                    }
                    );
                    return result;

                }
                ExcelTool summary = new ExcelTool(summaryPath, sheetName);
                DataTable sourceDT = summary.ExcelToDataTable("Sheet1", true);
                r_locker.ExitReadLock();

                string fileName = Guid.NewGuid().ToString() + ".xls";

                using (FileStream fs = System.IO.File.Create(Path.Combine(companyDir, fileName)))
                {
                    formFile.CopyTo(fs);
                    fs.Flush();
                }
                ExcelTool et = new ExcelTool(Path.Combine(companyDir, fileName), sheetName);

                result = et.ValidateIDs(idCol); // 验证身份证号码
                for (int i = 0; i < result.Count - 1; i++)
                {
                    for (int j = i + 1; j < result.Count; j++)
                    {
                        if (result[i].ID == result[j].ID)
                        {
                            result[i].Valid = false;
                            result[i].DataDesc = "所提交的表格中存在人员重复：" + result[i].ID;
                            result[j].Valid = false;
                            result[j].DataDesc = "所提交的表格中存在人员重复：" + result[j].ID;
                        }
                    }
                }
                MergeList(et.CheckJobType(jobCol), result); //验证岗位类型

                if (!System.IO.File.Exists(summaryFilePath)) //如果汇总表格不存在，则新建一个
                {
                    string source = Path.Combine(ExcelDirectory, "templates", "recipe.xls");
                    System.IO.File.Copy(source, summaryFilePath);
                }

                var temp = et.CheckDuplcateWithSummary(sourceDT, 3, idCol, mode); //验证总表中是否有重复
                MergeList(temp, result);
                HttpContext.Session.Set("mode", mode);
                HttpContext.Session.Set("plan", plan);
                HttpContext.Session.Set("newTable", et.ExcelToDataTable("Sheet1", true));
                System.IO.File.Delete(Path.Combine(companyDir, fileName));
                return result;
            }
            catch
            {
                result.Add(
                    new Employee()
                    {
                        Valid = false,
                        Name = "未知错误，请刷新页面并确保表格内容无误后重试"
                    }
                    );
                return result;
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }

        private void MergeList(List<Employee> newEmployees, List<Employee> employees)
        {
            if (newEmployees != null)
            {
                bool isSame = false;
                foreach (Employee item in newEmployees)
                {
                    isSame = false;
                    foreach (Employee item1 in employees)
                    {
                        if (item.ID == item1.ID && item.DataDesc == item1.DataDesc)
                        {
                            isSame = true;
                            item1.StartDate = item.StartDate;
                            break;
                        }
                    }
                    if (!isSame)
                    {
                        employees.Add(item);
                    }
                }
            }
        }

        protected double CalculateAddPrice(UserInfoModel account, DateTime dt, out int effectiveDays, List<Employee> employees = null)
        {
            List<Employee> newEmployees;
            //对于添加新的保费信息，按生效日期至本月底的天数收费
            double result = 0;
            if (employees != null)
            {
                newEmployees = employees;
            }
            else
                newEmployees = HttpContext.Session.Get<List<Employee>>("validationResult");
            if (newEmployees is null || newEmployees.Count <= 0)
            {
                effectiveDays = 0;
                return 0;
            }
            int year_now = DateTime.Now.Year;
            int month_now = DateTime.Now.Month;
            int day_now = DateTime.Now.Day;
            int year_specified = dt.Year;
            int month_specified = dt.Month;
            int day_sepecified = dt.Day;
            double totalPrice = account.UnitPrice;
            int monthDays = DateTime.DaysInMonth(year_specified, month_specified); //计算生效日期所在月份的天数
            double unitPrice = (double)totalPrice / monthDays;
            int totalNumber = newEmployees.Count;
            double pricedDays = monthDays - day_sepecified + 1; //收费天数
            effectiveDays = (int)pricedDays * totalNumber;
            result = (double)effectiveDays * unitPrice;
            return MathEx.ToCurrency(result);
        }

        public string GetSearchExcelsInDir(string companyName)
        {
            string result = string.Empty;
            CachedCompanyDir.TryAdd("管理员", Path.Combine(ExcelRoot, "管理员"));
            if (CachedCompanyDir.TryGetValue(companyName, out result))
            {
                if (Directory.Exists(result))
                {
                    return result;
                }
                else
                {
                    CachedCompanyDir.Remove(companyName, out result);
                }
            }
            var currUser = GetCurrentUser();
            if (currUser.CompanyName == companyName)
            {
                result = GetCurrentUserRootDir(currUser);
            }
            else
            {
                result = Directory.GetDirectories(GetCurrentUserRootDir(currUser), companyName, SearchOption.AllDirectories).FirstOrDefault();
            }
            if (!CachedCompanyDir.ContainsKey(companyName))
            {
                CachedCompanyDir.TryAdd(companyName, result);
            }
            return result;
        }

        /// <summary>
        /// Check if company is the child of user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="company"></param>
        /// <returns></returns>
        public bool IsChildCompany(UserInfoModel user, string company)
        {
            var children = GetChildrenCompanies(user.CompanyName);
            return children.Contains(company);
        }

        /// <summary>
        /// 当前账号所属公司的文件夹
        /// </summary>
        /// <param name="currUser"></param>
        /// <returns></returns>
        public string GetCurrentUserRootDir(UserInfoModel currUser)
        {
            string compName = currUser.CompanyName;
            if (CachedCompanyDir.ContainsKey(compName)) return CachedCompanyDir[compName];
            string result = Directory.GetDirectories(ExcelRoot, currUser.CompanyName, SearchOption.AllDirectories).FirstOrDefault();
            CachedCompanyDir.TryAdd(compName, result);
            return result;
        }
        public string GetCurrentUserHistoryRootDir(int year)
        {
            var currUser = GetCurrentUser();
            string root = Path.Combine(ExcelRoot, "历年归档", year.ToString());
            string result = Directory.GetDirectories(root, currUser.CompanyName, SearchOption.AllDirectories).FirstOrDefault();
            return result;
        }

        public List<string> GetSpringCompaniesName(bool includingself)
        {
            UserInfoModel currUser = GetCurrentUser();
            string companiesDirectory = GetCurrentUserRootDir(currUser);
            List<string> result = new List<string>();
            var springs = currUser.SpringAccounts;
            foreach (var account in springs)
            {
                var companyName = account.CompanyName;
                if (result.Any(_ => _ == companyName))
                {
                    continue;
                }
                result.Add(account.CompanyName);
            }

            if (includingself)
            {
                result.Add(currUser.CompanyName);
            }
            result = result.OrderBy(x => x).ToList();
            return result;
        }

        //public List<Company> GetSpringAccountsCompany()
        //{
        //    UserInfoModel currUser = GetCurrentUser();
        //    string companiesDirectory = GetCurrentUserRootDir(currUser);
        //    List<Company> result = new List<Company>();
        //    var springs = currUser.SpringAccounts;
        //    foreach (var account in springs)
        //    {
        //        var companyName = account.CompanyName;
        //        if (result.Any(_ => _.Name == companyName))
        //        {
        //            continue;
        //        }
        //        var companyDir = Directory.GetDirectories(companiesDirectory, companyName, SearchOption.AllDirectories).FirstOrDefault();
        //        if (string.IsNullOrEmpty(companyDir)) continue;
        //        if (!Directory.Exists(companyDir)) continue;
        //        var dirInfo = new DirectoryInfo(companyDir);
        //        Company company = new Company();
        //        company.Name = dirInfo.Name;
        //        ExcelDataReader edr = new ExcelDataReader(company.Name, From.Year, "");
        //        company.EmployeeNumber = edr.GetEmployeeNumber();
        //        company.StartDate = From;
        //        company.PaidCost = edr.GetPaidCost();
        //        company.CustomerAlreadyPaid = edr.GetCustomerAlreadyPaid();
        //        company.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", company.Name).Rows[0]["UnitPrice"]);
        //        company.TotalCost = edr.GetTotalCost();
        //        result.Add(company);
        //    }
        //    Company self = new Company();
        //    self.Name = currUser.CompanyName;
        //    string thisSummary = Path.Combine(companiesDirectory, currUser.CompanyName + ".xls");

        //    if (System.IO.File.Exists(thisSummary))
        //    {
        //        ExcelTool et = new ExcelTool(thisSummary, "Sheet1");
        //        self.EmployeeNumber = et.GetEmployeeNumber();
        //        self.StartDate = From;
        //        self.PaidCost = et.GetPaidCost();
        //        self.CustomerAlreadyPaid = et.GetCustomerAlreadyPaidFromJuneToMay(companiesDirectory, From.Year);
        //        self.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", self.Name).Rows[0]["UnitPrice"]);
        //        self.TotalCost = et.GetCostFromJuneToMay(companiesDirectory, From.Year);
        //        result.Add(self);
        //    }

        //    return result;
        //}

        private List<UserInfoModel[]> GroupAccountByCompanyName(List<UserInfoModel> accounts)
        {
            List<UserInfoModel[]> result = new List<UserInfoModel[]>();
            List<string> scannedCompany = new List<string>();
            foreach (var item in accounts)
            {
                if (scannedCompany.Contains(item.CompanyName)) continue;
                List<UserInfoModel> users = new List<UserInfoModel>();
                string companyName = item.CompanyName;
                scannedCompany.Add(companyName);
                users.AddRange(accounts.Where(x => x.CompanyName == companyName));
                result.Add(users.ToArray());
            }
            return result;
        }

        /// <summary>
        /// 获取账号自身的相关数据，不包含其子账号的数据
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public AccountData GetAccountData(UserInfoModel account)
        {
            try
            {
                AccountData ad = new AccountData();
                ad.Account = account;
                ad.PricePerMonth = account.UnitPrice;
                string company = account.CompanyName;
                if (company == "管理员")
                {
                    return null;
                }
                string plan = account._Plan;
                string companyDir = Directory.GetDirectories(Path.Combine(ExcelRoot, "管理员"), company, SearchOption.AllDirectories).FirstOrDefault();
                string accountDir = Directory.GetDirectories(companyDir, plan, SearchOption.AllDirectories).FirstOrDefault();
                string accountSummaryFile = Path.Combine(accountDir, company + ".xls");
                if (!System.IO.File.Exists(accountSummaryFile)) return ad;
                using (ExcelTool et = new ExcelTool(accountSummaryFile, "Sheet1"))
                {
                    ad.HeadCount = et.GetEmployeeNumber();
                }
                return ad;
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return null;
            }

        }

        /// <summary>
        /// 获取当前账号所属公司 或 其子账号所属公司的根目录
        /// </summary>
        /// <param name="currUser"></param>
        /// <param name="companyName"></param>
        /// <returns></returns>
        public string GetCompanyDir(UserInfoModel currUser, string companyName)
        {
            string companiesDirectory = GetCurrentUserRootDir(currUser);
            if (currUser.CompanyName == companyName) return companiesDirectory;
            if (Utility.CachedCompanyDirPath.Any(x => new DirectoryInfo(x).Name == companyName))
            {
                return Utility.CachedCompanyDirPath.Where(x => new DirectoryInfo(x).Name == companyName).FirstOrDefault();
            }
            else
            {
                string result = Directory.GetDirectories(companiesDirectory, companyName, SearchOption.AllDirectories).FirstOrDefault();
                if (result != null)
                    Utility.CachedCompanyDirPath.Add(result);
                return result;
            }
        }

        private bool IsSpringNotChildAccount(UserInfoModel father, UserInfoModel child)
        {
            return !father.ChildAccounts.Any(a => a.UserName == child.UserName);
        }

        public double CalculatePrice(string filePath, decimal unitPrice)
        {
            List<Employee> es = new List<Employee>();
            ExcelTool et = new ExcelTool(filePath, "Sheet1");
            var table = et.ExcelToDataTable("Sheet1", true);
            foreach (DataRow row in table.Rows)
            {
                Employee e = new Employee();
                e.Name = row[0].ToString();
                e.ID = row[1].ToString();
                e.JobType = row[2].ToString();
                e.Job = row[3].ToString();
                e.StartDate = row[4].ToString();
                e.EndDate = row[5].ToString();
                es.Add(e);
            }
            var curUser = GetCurrentUser();
            FileInfo fi = new FileInfo(filePath);
            string fileName = fi.Name;
            var info = fileName.Split("@");
            string mode = info[3];
            int a = 0;
            switch (mode)
            {
                case "Add":

                    return CalculateAddPrice(curUser, DateTime.Parse(es[0].StartDate), out a, employees: es); ;
                case "Sub":
                    double result = 0;
                    DateTime end = DateTime.Parse(es[0].EndDate);
                    foreach (Employee item in es)
                    {
                        DateTime start = DateTime.Parse(item.StartDate);
                        a = CalculateEffectiveDays(start, end, (double)unitPrice);
                        result += CalculateSubPrice(start.Date, end, curUser.UnitPrice, a);
                    }
                    return result;
            }
            return 1;
        }

        protected UserInfoModel GetChildAccountOfCurrentUserFromCompanyName(string companyName, UserInfoModel curUser)
        {
            UserInfoModel user = InsuranceDatabaseService.SelectUserByCompany(companyName);
            if (user is null) return user;
            while (!curUser.ChildAccounts.Any(u => u.UserName == user.UserName) && !string.IsNullOrEmpty(user.Father))
            {
                user = InsuranceDatabaseService.SelectUser(user.Father);
            }
            return user;
        }

        protected decimal GetCostFromFileName(string filePath, decimal priceEveryMonth = 0)
        {
            string[] info = filePath.Split("@");
            if (info.Length > 8)
            {
                FileInfo fi = new FileInfo(filePath);
                string dateStr = fi.Directory.Name;
                DateTime dt = DateTime.Parse(dateStr);
                int daysInMonth = DateTime.DaysInMonth(dt.Year, dt.Month);
                decimal priceEveryDay = priceEveryMonth / daysInMonth;
                int effectiveDays = int.Parse(info[7]);
                if (info[3].Equals("sub", StringComparison.CurrentCultureIgnoreCase))
                {
                    return -1 * effectiveDays * priceEveryDay;
                }
                else
                    return effectiveDays * priceEveryDay;
            }
            else
            {
                return decimal.Parse(info[1]);
            }
        }

        /// <summary>
        /// 获取当前账号所有子公司（包括所有后代账号）的信息,包括当前账号自身的数据
        /// </summary>
        /// <returns></returns>
        public List<Company> GetSpringAccountsCompanyInfo(UserInfoModel user, string targetCompany)
        {
            try
            {
                UserInfoModel curUser = GetCurrentUser();
                string companiesDirectory = GetCurrentUserRootDir(user);
                List<Company> result = new List<Company>();
                var children = user.ChildAccounts;
                var companyAccounts = GroupAccountByCompanyName(children);
                foreach (var companyAccount in companyAccounts)
                {
                    var companyName = companyAccount[0].CompanyName;
                    var companyDir = GetCompanyDir(user, companyName);
                    if (string.IsNullOrEmpty(companyDir)) continue;
                    if (!Directory.Exists(companyDir)) continue;
                    Company company = new Company();
                    company.Name = companyName;
                    double companyUnitPrice = 0;
                    //foreach (var account in companyAccount)
                    //{
                    //    if (user.ChildAccounts.Contains(account))
                    //    {
                    //        companyUnitPrice = account.UnitPrice; //读取保费时，按照当前账号为子账号的定价计算保费
                    //        break;
                    //    }
                    //}
                    companyUnitPrice = GetChildAccountOfCurrentUserFromCompanyName(companyName, curUser).UnitPrice;//读取保费时，按照当前账号为子账号的定价计算保费

                    foreach (var account in companyAccount)
                    {
                        ExcelDataReader edr = new ExcelDataReader(company.Name, From.Year, account._Plan);
                        company.EmployeeNumber += edr.GetCurrentEmployeeNumber();
                        company.StartDate = From;
                        company.PaidCost += edr.GetPaidCost();
                        company.CustomerAlreadyPaid += edr.GetCustomerAlreadyPaid();
                        company.UnitPrice = Convert.ToDouble(InsuranceDatabaseService.SelectPropFromTable("UserInfo", "CompanyName", company.Name).Rows[0]["UnitPrice"]);
                        company.TotalCost += edr.ReadTotalCost(companyUnitPrice);
                    }
                    result.Add(company);
                }

                Company self = new Company();
                self.Name = user.CompanyName;
                string thisSummary = Path.Combine(companiesDirectory, user._Plan, self.Name + ".xls");
                if (System.IO.File.Exists(thisSummary))
                {
                    ExcelTool et = new ExcelTool(thisSummary, "Sheet1");
                    self.EmployeeNumber = et.GetEmployeeNumber();
                    self.StartDate = From;
                    self.PaidCost = et.GetPaidCost();
                    self.CustomerAlreadyPaid = et.GetCustomerAlreadyPaidFromJuneToMay(companiesDirectory, From.Year);
                    self.UnitPrice = Convert.ToDouble(InsuranceDatabaseService.SelectPropFromTable("UserInfo", "CompanyName", self.Name).Rows[0]["UnitPrice"]);
                    self.TotalCost = et.GetCostFromJuneToMay(Path.Combine(companiesDirectory, user._Plan), From.Year, user.UnitPrice);
                    result.Add(self);
                    //ExcelDataReader et = new ExcelDataReader(self.Name,From.Year,currUser._Plan);
                    //self.EmployeeNumber = et.GetEmployeeNumber();
                    //self.StartDate = From;
                    //self.PaidCost = et.GetPaidCost();
                    //self.CustomerAlreadyPaid = et.GetCustomerAlreadyPaid();
                    //self.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", self.Name).Rows[0]["UnitPrice"]);
                    //self.TotalCost = et.GetTotalCost();
                    //result.Add(self);
                }
                return result;
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return null;
                throw new Exception(e.StackTrace);
            }

        }

        public IEnumerable<string> GetChildrenCompanies(string companyName)
        {
            ConcurrentBag<string> result = new ConcurrentBag<string>();
            string targetDir = Directory.GetDirectories(ExcelRoot, companyName, SearchOption.AllDirectories)[0];
            var dirs = Directory.GetDirectories(targetDir);
            foreach (var dir in dirs)
            {
                var dirInfo = new DirectoryInfo(dir);
                if (!DateTime.TryParse(dirInfo.Name, out DateTime date) && !Plans.Contains(dirInfo.Name))
                {
                    result.Add(dirInfo.Name);
                    GetChildrenCompanies(dirInfo.Name).ToList().ForEach(_ => result.Add(new DirectoryInfo(_).Name));
                }
            }
            return result;
        }

        /// <summary>
        /// 获取用户的直接子公司及自身公司的信息
        /// </summary>
        /// <param name="user"></param>
        /// <param name="plan"></param>
        /// <returns></returns>
        public IEnumerable<Company> GetChildrenCompanies(UserInfoModel user, string plan)
        {
            UserInfoModel curUser = GetCurrentUser();
            List<Company> result = new List<Company>();
            string companyName = user.CompanyName;
            string targetDir = Directory.GetDirectories(ExcelRoot, companyName, SearchOption.AllDirectories).FirstOrDefault();
            List<string> companies = Directory.GetDirectories(targetDir).Where(_ => !Plans.Contains(new DirectoryInfo(_).Name)).ToList();
            companies.Add(targetDir);

            foreach (var company in companies)
            {
                var companyDir = new DirectoryInfo(company);
                if (companyDir.Name == "管理员") continue;
                if (companyDir.Name == new DirectoryInfo(targetDir).Name) //若该文件夹为当前账号文件夹
                {
                    Company com = new Company();
                    com.Name = companyDir.Name;
                    string summary = Path.Combine(companyDir.FullName, plan, companyDir.Name + ".xls");
                    if (!new FileInfo(summary).Exists) continue;
                    ExcelTool edr = new ExcelTool(summary, "Sheet1");
                    com.PaidCost = edr.GetPaidCost();
                    com.TotalCost = edr.GetCostFromJuneToMay(Path.Combine(companyDir.FullName, plan), From.Year, curUser.UnitPrice);
                    com.EmployeeNumber = edr.GetEmployeeNumber();
                    com.CustomerAlreadyPaid = edr.GetCustomerAlreadyPaidFromJuneToMay(Path.Combine(companyDir.FullName, plan), From.Year);
                    com.StartDate = From;
                    com.UnitPrice = Convert.ToDouble(InsuranceDatabaseService.SelectPropFromTable("UserInfo", "CompanyName", com.Name).Rows[0]["UnitPrice"]);
                    result.Add(com);
                }
                else
                {
                    double price = Convert.ToDouble(InsuranceDatabaseService.SelectPropFromTable("UserInfo", "CompanyName", companyDir.Name).Rows[0]["UnitPrice"]);
                    Company com = new Company();
                    com.Name = companyDir.Name;
                    com.StartDate = From;
                    ExcelDataReader edr = new ExcelDataReader(companyDir.Name, From.Year, plan);
                    com.PaidCost += edr.GetPaidCost();
                    com.TotalCost += edr.ReadTotalCost(price);
                    com.EmployeeNumber += edr.GetEmployeeNumber();
                    com.CustomerAlreadyPaid += edr.GetCustomerAlreadyPaid();
                    var temp = InsuranceDatabaseService.SelectPropFromTable("UserInfo", "CompanyName", com.Name);
                    if (temp != null)
                        com.UnitPrice += Convert.ToDouble(temp.Rows[0]["UnitPrice"]);
                    result.Add(com);
                }
            }
            return result;
        }

        //public List<Company> GetAllCompanies()
        //{
        //    List<Company> result = new List<Company>();
        //    string companiesDirectory = GetSearchExcelsInDir("管理员");
        //    foreach (string comp in Directory.GetDirectories(companiesDirectory))
        //    {
        //        DirectoryInfo di = new DirectoryInfo(comp);
        //        ExcelDataReader edr;
        //        if (System.IO.File.Exists(Path.Combine(comp, new DirectoryInfo(comp).Name + ".xls")))
        //        {
        //            edr = new ExcelDataReader(di.Name, From.Year, "");
        //        }
        //        else
        //        {
        //            continue;
        //        }
        //        Company company = new Company();
        //        company.Name = Path.GetFileName(comp);
        //        company.EmployeeNumber = edr.GetEmployeeNumber();
        //        company.StartDate = new DateTime(DateTime.Now.Date.Year, DateTime.Now.Date.Month, 1);
        //        company.PaidCost = edr.GetPaidCost();
        //        company.CustomerAlreadyPaid = edr.GetCustomerAlreadyPaid();
        //        company.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", company.Name).Rows[0]["UnitPrice"]);
        //        company.TotalCost = edr.ReadTotalCost();
        //        result.Add(company);
        //    }
        //    return result;
        //}
        protected void AdaptModel(ViewModelBase model)
        {
            UserInfoModel uim = HttpContext.Session.Get<UserInfoModel>("CurrentUser");
            if (uim is null)
            {
                return;
            }
            model.CompanyName = uim.CompanyName;
            model.CompanyNameAbb = uim.CompanyNameAbb;
            model.Mail = uim.Mail;
            model.TaxNum = uim.TaxNum;
            model.Telephone = uim.Telephone;
            model.Name = uim.Name;
            model.RecipeAccount = uim.RecipeAccount;
            model.RecipeAddress = uim.RecipeAddress;
            model.RecipeBank = uim.RecipeBank;
            model.RecipeCompany = uim.RecipeCompany;
            model.RecipePhone = uim.RecipePhone;
        }

        /// <summary>
        /// JS Ajax请求:更新验证码图片
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public string UpdateValidationImg()
        {
            ViewBag.ValidationStr = _imageTool.DrawValidationImg();
            return ViewBag.ValidationStr;
        }

        [UserLoginFilters]
        public UserInfoModel GetCurrentUser()
        {
            if (currUser_temp != null) return currUser_temp;
            IUser user = new UserInfoModel();
            var currUser = HttpContext.Session.Get<UserInfoModel>("CurrentUser");
            user.UserName = currUser.UserName;
            user.userPassword = currUser.userPassword;
            UserInfoModel uim = InsuranceDatabaseService.UserMatchUserNamePassword(user);
            uim.MyLocker = Utility.GetCompanyLocker(uim.CompanyName);
            if (uim.ChildAccounts == null || uim.ChildAccounts.Count == 0)
            {
                var children = InsuranceDatabaseService.Select("UserInfo").Select().Where(_ => _[nameof(UserInfoModel.Father)].ToString() == uim.UserName);
                foreach (var item in children)
                {
                    uim.ChildAccounts.Add(InsuranceDatabaseService.SelectUser(item[nameof(UserInfoModel.UserName)].ToString()));
                }
            }

            return uim;
        }


        public bool HasAuthority(string companyname)
        {
            var currUser = GetCurrentUser();
            if (!IsAncestor(currUser, companyname) && currUser.CompanyName != companyname)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 判断当前用户是否为公司的上级账号
        /// </summary>
        /// <param name="user"></param>
        /// <param name="companyname"></param>
        /// <returns></returns>
        public bool IsAncestor(UserInfoModel user, string companyname)
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

        [HttpGet]
        public bool MailExists(string _mail)
        {
            try
            {
                if (string.IsNullOrEmpty(_mail))
                {
                    _mail = HttpContext.Request.Query["input"];
                }

                if (string.IsNullOrEmpty(_mail))
                {
                    return true;
                }

                if (!InsuranceDatabaseService.IsMailUsed(_mail))
                {
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                LogServices.LogService.Log(e.Message);
                return true;
            }
        }

        /// <summary>
        /// JS Ajax请求:获取验证码数值
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public bool ValidationResult()
        {
            try
            {
                string result = HttpContext.Request.Query["input"];
                if (string.IsNullOrEmpty(result))
                {
                    goto Err;
                }
                int i1 = 9999;
                if (!int.TryParse(result, out i1))
                {
                    goto Err;
                }
                int i2 = Convert.ToInt16(HttpContext.Session.Get<int>("ValidationResult"));

                if (i1 == i2)
                    return true;
                else
                    goto Err;

                Err:
                return false;
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return false;
            }

        }
    }
}
