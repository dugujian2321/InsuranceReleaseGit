using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using VirtualCredit.LogServices;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit
{
    public class VC_ControllerBase : Controller
    {
        protected IHostingEnvironment _hostingenvironment;
        public static long CustomersCount = 0;
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
        public string GameName
        {
            get
            {
                return HttpContext.Session.Get<string>("GlobalGameName");
            }
            set
            {
                HttpContext.Session.Set<string>("GlobalGameName", value);
            }
        }
        public double ScreenWidth { get; set; }
        public double ScreenHeight { get; set; }
        public VC_ControllerBase()
        {

        }
        public VC_ControllerBase(HttpContext context)
        {
            _imageTool = new ImageTool(context);
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

        public List<Company> GetAllCompanies(string companiesDirectory)
        {
            List<Company> result = new List<Company>();
            foreach (string comp in Directory.GetDirectories(companiesDirectory))
            {
                ExcelTool et;
                if (System.IO.File.Exists(Path.Combine(comp, new DirectoryInfo(comp).Name + ".xls")))
                {
                    et = new ExcelTool(Path.Combine(comp, new DirectoryInfo(comp).Name + ".xls"), "Sheet1");
                }
                else
                {
                    continue;
                }
                Company company = new Company();
                company.Name = Path.GetFileName(comp);
                company.EmployeeNumber = et.GetEmployeeNumber();
                company.StartDate = new DateTime(DateTime.Now.Date.Year, DateTime.Now.Date.Month, 1);
                company.PaidCost = et.GetPaidCost();
                company.UnitPrice = Convert.ToDouble(DatabaseService.SelectPropFromTable("UserInfo", "CompanyName", company.Name).Rows[0]["UnitPrice"]);
                company.TotalCost = et.GetTotalCost(Path.Combine(companiesDirectory, company.Name));
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
