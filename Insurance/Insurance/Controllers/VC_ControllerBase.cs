using Insurance;
using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VirtualCredit.LogServices;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit
{
    public class VC_ControllerBase : Controller
    {
        public static TreeNode<UserInfoModel> AccountTreeRoot = new TreeNode<UserInfoModel>();
        public static ConcurrentDictionary<string, string> CachedCompanyDir = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, ISession> MiniAppSessions = new ConcurrentDictionary<string, ISession>();
        protected IHostingEnvironment _hostingEnvironment;
        public static List<string> Plans = new List<string> { "60万A", "60万B", "80万A", "80万B" };
        public static long CustomersCount = 0;
        public static string ExcelRoot = Utility.Instance.ExcelRoot;
        public static string AdminDir = Path.Combine(ExcelRoot, "管理员");
        protected ImageTool _imageTool;
        public UserInfoModel currUser_temp;
        public ISession CurrentSession;
        public string RoleId
        {
            get
            {
                return CurrentSession.Get<string>("RoleId");
            }
            set
            {
                CurrentSession.Set<string>("RoleId", value);
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
            {
                CurrentSession = context.Session;
                _imageTool = new ImageTool(context);
            }

            UpdateAccountTree();
            UpdateFromTo();
            var t = AccountTreeRoot.Descendant;
        }

        public void UpdateAccountTree()
        {
            UserInfoModel admin = DatabaseService.SelectUser("oliver");
            AccountTreeRoot = new TreeNode<UserInfoModel>();
            AccountTreeRoot.Data = admin;
            AccountTreeRoot.Parent = null;
            foreach (var item in admin.ChildAccounts)
            {
                AddNode(AccountTreeRoot, item);
            }
        }

        void AddNode(TreeNode<UserInfoModel> parent, UserInfoModel account)
        {
            TreeNode<UserInfoModel> treeNode = new TreeNode<UserInfoModel>();
            treeNode.Data = account;
            treeNode.Parent = parent;

            if (account.ChildAccounts.Count <= 0)
            {
                return;
            }

            foreach (var child in account.ChildAccounts)
            {
                AddNode(treeNode, child);
            }
        }

        public void UpdateFromTo()
        {
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

        public JsonResult CheckMiniAuth(string openId)
        {
            string result = "";
            if (!MiniAppSessions.Keys.Contains(openId))
                result = "权限不足，无法访问，重新进入小程序重试";
            return Json(result);
        }

        public void MiniSession(string openId)
        {
#if DEBUG
            var k = MiniAppSessions.Keys.First();
            CurrentSession = MiniAppSessions[k];
            ViewBag.CurrentUser = GetCurrentUser();
            ViewBag.OpenId = openId;
            return;
#endif
            CheckMiniAuth(openId);
            CurrentSession = MiniAppSessions[openId];
            ViewBag.CurrentUser = GetCurrentUser();
            ViewBag.OpenId = openId;
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

        protected double CalculateSubPrice(DateTime start, DateTime end, double price)
        {
            //对于退保人员，计算退费
            double unitPrice = price / DateTime.DaysInMonth(end.Year, end.Month);
            double received = MathEx.ToCurrency((DateTime.DaysInMonth(end.Year, end.Month) - start.Day + 1) * unitPrice);
            double earned = MathEx.ToCurrency((end.Day - start.Day + 1) * unitPrice);
            double payback = earned - received;
            return payback;
        }

        protected double CalculateAddPrice(UserInfoModel account, DateTime dt)
        {
            //对于添加新的保费信息，按生效日期至本月底的天数收费
            double result = 0;
            var newEmployees = CurrentSession.Get<List<Employee>>("validationResult");
            if (newEmployees is null || newEmployees.Count <= 0)
            {
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
            result = pricedDays * unitPrice * totalNumber;
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

        public List<Company> GetSpringAccountsCompany()
        {
            UserInfoModel currUser = GetCurrentUser();
            string companiesDirectory = GetCurrentUserRootDir(currUser);
            List<Company> result = new List<Company>();
            var springs = currUser.SpringAccounts;
            foreach (var account in springs)
            {
                var companyName = account.CompanyName;
                if (result.Any(_ => _.Name == companyName))
                {
                    continue;
                }
                var companyDir = Directory.GetDirectories(companiesDirectory, companyName, SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrEmpty(companyDir)) continue;
                if (!Directory.Exists(companyDir)) continue;
                var dirInfo = new DirectoryInfo(companyDir);
                Company company = new Company();
                company.Name = dirInfo.Name;
                ExcelDataReader edr = new ExcelDataReader(company.Name, From.Year, "");
                company.EmployeeNumber = edr.GetEmployeeNumber();
                company.StartDate = From;
                company.PaidCost = edr.GetPaidCost();
                company.CustomerAlreadyPaid = edr.GetCustomerAlreadyPaid();
                company.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", company.Name).Rows[0]["UnitPrice"]);
                company.TotalCost = edr.GetTotalCost();
                result.Add(company);
            }
            Company self = new Company();
            self.Name = currUser.CompanyName;
            string thisSummary = Path.Combine(companiesDirectory, currUser.CompanyName + ".xls");

            if (System.IO.File.Exists(thisSummary))
            {
                ExcelTool et = new ExcelTool(thisSummary, "Sheet1");
                self.EmployeeNumber = et.GetEmployeeNumber();
                self.StartDate = From;
                self.PaidCost = et.GetPaidCost();
                self.CustomerAlreadyPaid = et.GetCustomerAlreadyPaidFromJuneToMay(companiesDirectory, From.Year);
                self.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", self.Name).Rows[0]["UnitPrice"]);
                self.TotalCost = et.GetCostFromJuneToMay(companiesDirectory, From.Year);
                result.Add(self);
            }

            return result;
        }

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
                string companyDir = "";
                companyDir = Utility.CachedCompanyDirPath.Where(x => new DirectoryInfo(x).Name.Equals(company)).FirstOrDefault();
                if (string.IsNullOrEmpty(companyDir))
                {
                    companyDir = Directory.GetDirectories(AdminDir, company, SearchOption.AllDirectories).FirstOrDefault();
                    Utility.CachedCompanyDirPath.Add(companyDir);
                }
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
                Utility.CachedCompanyDirPath.Add(result);
                return result;
            }
        }

        /// <summary>
        /// 获取当前账号所有子公司（包括所有后代账号的信息）的信息,包括当前账号自身的数据
        /// </summary>
        /// <returns></returns>
        public List<Company> GetChildAccountsCompany()
        {
            try
            {
                UserInfoModel currUser = GetCurrentUser();
                string companiesDirectory = GetCurrentUserRootDir(currUser);
                List<Company> result = new List<Company>();
                var children = currUser.ChildAccounts;
                var companyAccounts = GroupAccountByCompanyName(children);
                foreach (var companyAccount in companyAccounts)
                {
                    var companyName = companyAccount[0].CompanyName;
                    var companyDir = GetCompanyDir(currUser, companyName);
                    if (string.IsNullOrEmpty(companyDir)) continue;
                    if (!Directory.Exists(companyDir)) continue;
                    Company company = new Company();
                    company.Name = companyName;
                    company.AbbrName = companyAccount[0].CompanyNameAbb;
                    foreach (var account in companyAccount)
                    {
                        ExcelDataReader edr = new ExcelDataReader(account.CompanyName, From.Year, account._Plan);
                        company.EmployeeNumber += edr.GetCurrentEmployeeNumber();
                        company.StartDate = From;
                        company.PaidCost += edr.GetPaidCost();
                        company.CustomerAlreadyPaid += edr.GetCustomerAlreadyPaid();
                        company.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", company.Name).Rows[0]["UnitPrice"]);
                        company.TotalCost += edr.GetTotalCost(currUser);
                    }
                    result.Add(company);
                }

                Company self = new Company();
                self.Name = currUser.CompanyName;
                string thisSummary = Path.Combine(companiesDirectory, currUser._Plan, self.Name + ".xls");
                if (System.IO.File.Exists(thisSummary))
                {
                    ExcelTool et = new ExcelTool(thisSummary, "Sheet1");
                    self.EmployeeNumber = et.GetEmployeeNumber();
                    self.StartDate = From;
                    self.AbbrName = currUser.CompanyNameAbb;
                    self.PaidCost = et.GetPaidCost();
                    self.CustomerAlreadyPaid = et.GetCustomerAlreadyPaidFromJuneToMay(companiesDirectory, From.Year);
                    self.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", self.Name).Rows[0]["UnitPrice"]);
                    self.TotalCost = et.GetCostFromJuneToMay(Path.Combine(companiesDirectory, currUser._Plan), From.Year);
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
        public IEnumerable<Company> GetChildrenCompanies(UserInfoModel user, string plan, int year)
        {
            DateTime dataFrom = From;
            DateTime dataTo = To;
            if (year != 0)
            {
                dataFrom = new DateTime(year, 6, 1);
                dataTo = new DateTime(year + 1, 5, 31);
            }
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
                    com.TotalCost = edr.GetCostFromJuneToMay(Path.Combine(companyDir.FullName, plan), dataFrom.Year);
                    com.EmployeeNumber = edr.GetEmployeeNumber();
                    com.CustomerAlreadyPaid = edr.GetCustomerAlreadyPaidFromJuneToMay(Path.Combine(companyDir.FullName, plan), dataFrom.Year);
                    com.StartDate = dataFrom;
                    com.AbbrName = DatabaseService.CompanyAbbrName(com.Name);
                    com.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", com.Name).Rows[0]["UnitPrice"]);
                    result.Add(com);
                }
                else
                {
                    Company com = new Company();
                    com.Name = companyDir.Name;
                    com.AbbrName = DatabaseService.CompanyAbbrName(com.Name);
                    com.StartDate = dataFrom;
                    ExcelDataReader edr = new ExcelDataReader(companyDir.Name, dataFrom.Year, plan);
                    com.PaidCost += edr.GetPaidCost();
                    com.TotalCost += edr.GetTotalCost(user);
                    com.EmployeeNumber += edr.GetEmployeeNumber();
                    com.CustomerAlreadyPaid += edr.GetCustomerAlreadyPaid();
                    var temp = DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", com.Name);
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
        //        company.TotalCost = edr.GetTotalCost();
        //        result.Add(company);
        //    }
        //    return result;
        //}
        protected void AdaptModel(ViewModelBase model)
        {
            UserInfoModel uim = GetCurrentUser();
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
            var currUser = CurrentSession.Get<UserInfoModel>("CurrentUser");
            currUser_temp = currUser;
            user.UserName = currUser.UserName;
            user.userPassword = currUser.userPassword;

            UserInfoModel uim = DatabaseService.UserMatchUserNamePassword(user);
            uim.MyLocker = Utility.GetCompanyLocker(uim.CompanyName);
            if (uim.ChildAccounts == null || uim.ChildAccounts.Count == 0)
            {
                var children = DatabaseService.Select("UserInfo").Select().Where(_ => _[nameof(UserInfoModel.Father)].ToString() == uim.UserName);
                foreach (var item in children)
                {
                    uim.ChildAccounts.Add(DatabaseService.SelectUser(item[nameof(UserInfoModel.UserName)].ToString()));
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

                if (!DatabaseService.IsMailUsed(_mail))
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
                int i2 = Convert.ToInt16(CurrentSession.Get<int>("ValidationResult"));

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
