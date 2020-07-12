using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using NPOI.HSSF.EventUserModel.DummyRecord;
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
        protected IHostingEnvironment _hostingEnvironment;
        public static long CustomersCount = 0;
        public static string ExcelRoot = Utility.Instance.ExcelRoot;
        protected ImageTool _imageTool;
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

        protected double CalculateSubPrice(DateTime start, DateTime end)
        {
            //对于退保人员，计算退费
            double unitPrice = (double)GetCurrentUser().UnitPrice / DateTime.DaysInMonth(end.Year, end.Month);
            double received = (DateTime.DaysInMonth(end.Year, end.Month) - start.Day + 1) * unitPrice;
            double earned = (end.Day - start.Day + 1) * unitPrice;
            double payback = Math.Round(earned - received, 2);
            return payback;
        }

        protected double CalculateAddPrice(DateTime dt)
        {
            //对于添加新的保费信息，按生效日期至本月底的天数收费
            double result = 0;
            var newEmployees = HttpContext.Session.Get<List<Employee>>("validationResult");
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
            double totalPrice = GetCurrentUser().UnitPrice;
            int monthDays = DateTime.DaysInMonth(year_specified, month_specified); //计算生效日期所在月份的天数
            double unitPrice = (double)totalPrice / monthDays;
            int totalNumber = newEmployees.Count;
            double pricedDays = monthDays - day_sepecified + 1; //收费天数
            result = pricedDays * unitPrice * totalNumber;
            return Math.Round(result, 2);
        }

        public string GetSearchExcelsInDir(string companyName)
        {
            var currUser = GetCurrentUser();
            if (currUser.CompanyName == companyName)
            {
                return GetCurrentUserRootDir();
            }
            else
            {
                return Directory.GetDirectories(GetCurrentUserRootDir(), companyName, SearchOption.AllDirectories).FirstOrDefault();
            }
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

        public string GetCurrentUserRootDir()
        {
            var currUser = GetCurrentUser();

            string result = Directory.GetDirectories(ExcelRoot, currUser.CompanyName, SearchOption.AllDirectories)[0];
            return result;

        }

        public List<Company> GetAllChildAccounts()
        {
            UserInfoModel currUser = GetCurrentUser();
            string companiesDirectory = GetCurrentUserRootDir();
            List<Company> result = new List<Company>();
            List<string> subDirs = Directory.GetDirectories(companiesDirectory).ToList();
            foreach (string account in subDirs)
            {
                var dirInfo = new DirectoryInfo(account);
                if (DateTime.TryParse(dirInfo.Name, out DateTime dateTime))
                {
                    continue;
                }
                if (!System.IO.File.Exists(Path.Combine(account, dirInfo.Name + ".xls")))
                {
                    continue;
                }
                Company company = new Company();
                company.Name = Path.GetFileName(account);
                ExcelDataReader edr = new ExcelDataReader(company.Name, From.Year);
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

        public IEnumerable<string> GetChildrenCompanies(string companyName)
        {
            ConcurrentBag<string> result = new ConcurrentBag<string>();
            string targetDir = Directory.GetDirectories(ExcelRoot, companyName, SearchOption.AllDirectories)[0];

            foreach (var dir in Directory.GetDirectories(targetDir))
            {
                var dirInfo = new DirectoryInfo(dir);
                if (!DateTime.TryParse(dirInfo.Name, out DateTime date))
                {
                    result.Add(dirInfo.Name);
                    GetChildrenCompanies(dirInfo.Name).ToList().ForEach(_ => result.Add(new DirectoryInfo(_).Name));
                }
            }
            return result;
        }

        public IEnumerable<Company> GetChildrenCompanies(UserInfoModel user)
        {
            List<Company> result = new List<Company>();
            string companyName = user.CompanyName;
            string targetDir = Directory.GetDirectories(ExcelRoot, companyName, SearchOption.AllDirectories).FirstOrDefault();
            List<string> companies = Directory.GetDirectories(targetDir).ToList();
            companies.Add(targetDir);
            foreach (var dir in companies)
            {
                var dirInfo = new DirectoryInfo(dir);
                if (dirInfo.Name == "管理员") continue;
                if (dir == targetDir)
                {
                    Company com = new Company();
                    com.Name = dirInfo.Name;
                    string summary = Path.Combine(dirInfo.FullName, dirInfo.Name + ".xls");
                    if (!new FileInfo(summary).Exists) continue;
                    ExcelTool edr = new ExcelTool(summary, "Sheet1");
                    com.PaidCost = edr.GetPaidCost();
                    com.TotalCost = edr.GetCostFromJuneToMay(dirInfo.FullName, From.Year);
                    com.EmployeeNumber = edr.GetEmployeeNumber();
                    com.CustomerAlreadyPaid = edr.GetCustomerAlreadyPaidFromJuneToMay(dirInfo.FullName, From.Year);
                    com.StartDate = From;
                    com.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", com.Name).Rows[0]["UnitPrice"]);
                    result.Add(com);
                }
                else
                {
                    if (!DateTime.TryParse(dirInfo.Name, out DateTime date))
                    {
                        Company com = new Company();
                        com.Name = dirInfo.Name;
                        ExcelDataReader edr = new ExcelDataReader(dirInfo.Name, From.Year);
                        com.PaidCost = edr.GetPaidCost();
                        com.TotalCost = edr.GetTotalCost();
                        com.EmployeeNumber = edr.GetEmployeeNumber();
                        com.CustomerAlreadyPaid = edr.GetCustomerAlreadyPaid();
                        com.StartDate = From;
                        com.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", com.Name).Rows[0]["UnitPrice"]);
                        result.Add(com);
                    }
                }
            }
            return result;
        }

        public List<Company> GetAllCompanies()
        {
            List<Company> result = new List<Company>();
            string companiesDirectory = GetSearchExcelsInDir("管理员");
            foreach (string comp in Directory.GetDirectories(companiesDirectory))
            {
                DirectoryInfo di = new DirectoryInfo(comp);
                ExcelDataReader edr;
                if (System.IO.File.Exists(Path.Combine(comp, new DirectoryInfo(comp).Name + ".xls")))
                {
                    edr = new ExcelDataReader(di.Name, From.Year);
                }
                else
                {
                    continue;
                }
                Company company = new Company();
                company.Name = Path.GetFileName(comp);
                company.EmployeeNumber = edr.GetEmployeeNumber();
                company.StartDate = new DateTime(DateTime.Now.Date.Year, DateTime.Now.Date.Month, 1);
                company.PaidCost = edr.GetPaidCost();
                company.CustomerAlreadyPaid = edr.GetCustomerAlreadyPaid();
                company.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", company.Name).Rows[0]["UnitPrice"]);
                company.TotalCost = edr.GetTotalCost();
                result.Add(company);
            }
            return result;
        }
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

        public UserInfoModel GetCurrentUser()
        {
            IUser user = new UserInfoModel();
            user.UserName = HttpContext.Session.Get<UserInfoModel>("CurrentUser").UserName;
            user.userPassword = HttpContext.Session.Get<UserInfoModel>("CurrentUser").userPassword;
            UserInfoModel uim = DatabaseService.UserMatchUserNamePassword(user);
            uim.MyLocker = Utility.GetCompanyLocker(uim.CompanyName);
            var children = DatabaseService.Select("UserInfo").Select().Where(_ => _[nameof(UserInfoModel.Father)].ToString() == uim.UserName);
            foreach (var item in children)
            {
                uim.ChildAccounts.Add(DatabaseService.SelectUser(item[nameof(UserInfoModel.UserName)].ToString()));
            }
            return uim;
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
