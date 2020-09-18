using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.V3.Pages.Internal.Account.Manage;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using VirtualCredit.Models;
using VirtualCredit.Services;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace VirtualCredit.Controllers
{
    public class LoginController : VC_ControllerBase
    {
        private IHttpContextAccessor _accessor;

        public LoginController(IHttpContextAccessor accessor) : base(accessor.HttpContext)
        {
            _accessor = accessor;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (SessionService.IsUserLogin(HttpContext))
            {
                ViewBag.Online = true;
                return RedirectToAction(controllerName: "Home", actionName: "Index");
            }
            ActionAfterReload(string.Empty);
            //        Response.Cookies.Append("ValidationResult", _imageTool.ValidationResult.ToString(),
            //new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddMinutes(2) });
            return View();
        }

        [HttpPost]
        public IActionResult Login(UserInfoModel user)
        {
            if (SessionService.IsUserLogin(HttpContext))
            {
                return RedirectToAction(controllerName: "Home", actionName: "Index");
            }
            if (!ModelState.IsValid)
            {
                ActionAfterReload("账号或密码不正确");
                EncryptCache(HttpContext);
                return View();
            }
            int MDIteration;
            //若Session中读取不到前端预处理Iteration，则重置Iteration信息，并返回。
            if (!int.TryParse(HttpContext.Session.Get<string>("Frontend"), out MDIteration))
            {
                ActionAfterReload("");
                EncryptCache(HttpContext);
                return View("Login", new UserInfoModel() { UserName = user.UserName });
            }
            HttpContext.Session.Set<string>("Frontend", null);
            MD5Helper md = new MD5Helper();

            string ip = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();

            #region 验证验证码
            string userInputValidation = string.Empty;
            userInputValidation = HttpContext.Request.Form["validationResult"];
            int backendValidation = HttpContext.Session.Get<int>("ValidationResult");
            UserInfoModel uim = new UserInfoModel();
            uim = DatabaseService.UserMatchUserNamePassword(user);
            if (Convert.ToInt16(userInputValidation) != backendValidation)
            {
                ActionAfterReload("验证码计算错误，请重新输入！");
                return View();
            }
            #endregion
            else if (uim != null)
            {
                user.CompanyName = uim.CompanyName;
                user.IsOnline = HttpContext.Session.Id;
                user.IPAddress = ip;
                if (!DatabaseService.UpdateUserInfo(user, new List<string>() { "IPAddress", "IsOnline" }))
                {
                    LogServices.LogService.Log($"Failed updating userinfo.{user.CompanyName} {user.UserName}");
                    return View();
                }
                user.AccessLevel = uim.AccessLevel;
                user.CompanyNameAbb = uim.CompanyNameAbb;
                user.Mail = uim.Mail;
                user.TaxNum = uim.TaxNum;
                user.Telephone = uim.Telephone;
                user.DaysBefore = uim.DaysBefore;
                user.Name = uim.Name;
                user.UnitPrice = uim.UnitPrice;
                user.RecipeAccount = uim.RecipeAccount;
                user.RecipeAddress = uim.RecipeAddress;
                user.RecipeBank = uim.RecipeBank;
                user.RecipeCompany = uim.RecipeCompany;
                user.AllowCreateAccount = uim.AllowCreateAccount;
                user.RecipePhone = uim.RecipePhone;
                user.userPassword = uim.userPassword;
                user.RecipeType = uim.RecipeType;
                user.ChildAccounts = new List<UserInfoModel>();
                user._Plan = uim._Plan;
                user.Father = uim.Father;
                var children = DatabaseService.Select("UserInfo").Select().Where(_ => _[nameof(UserInfoModel.Father)].ToString() == uim.UserName);
                //foreach (var item in children)
                //{
                //    user.ChildAccounts.Add(DatabaseService.SelectUser(item[nameof(UserInfoModel.UserName)].ToString()));
                //}

                SessionService.UserOnline(HttpContext);
                var locker = Utility.LockerList.Where(_ => _.LockerCompany == user.CompanyName);
                if (locker == null || locker.Count() <= 0)
                {
                    ReaderWriterLockerWithName newlocker = new ReaderWriterLockerWithName()
                    {
                        LockerCompany = user.CompanyName,
                        RWLocker = new System.Threading.ReaderWriterLockSlim()
                    };
                    Utility.LockerList.Add(newlocker);
                    foreach (var item in user.SpringAccounts)
                    {
                        if (Utility.LockerList.Any(_ => _.LockerCompany == item.CompanyName)) continue;
                        newlocker = new ReaderWriterLockerWithName()
                        {
                            LockerCompany = item.CompanyName,
                            RWLocker = new System.Threading.ReaderWriterLockSlim()
                        };
                        Utility.LockerList.Add(newlocker);
                    }
                }
                user.MyLocker = Utility.GetCompanyLocker(user.CompanyName);
                currUser_temp = user;
                HttpContext.Session.Set("CurrentUser", user);
                HttpContext.Session.Set<string>("Frontend", null);
                UpdateDailyData();
                return RedirectToAction("Index", "Home", null);
            }
            else
            {
                LogServices.LogService.Log($"{userInputValidation};{backendValidation}");
                ActionAfterReload("用户名或密码错误，请重新输入");
                return View();
            }
        }

        private void UpdateDailyData()
        {
            double dailyPrice = 0;
            int dailyHeadCount = 0;
            var currUser = GetCurrentUser();
            List<UserInfoModel> accounts = new List<UserInfoModel>();
            accounts.Add(currUser);
            accounts.AddRange(currUser.SpringAccounts);
            foreach (var account in accounts)
            {
                AccountData ad = GetAccountData(account);
                if (ad != null)
                {
                    dailyPrice += MathEx.ToCurrency(ad.PricePerDay * ad.HeadCount);
                    dailyHeadCount += ad.HeadCount;
                }
            }
            HttpContext.Session.Set("DailyHeadCount", dailyHeadCount);
            HttpContext.Session.Set("DailyPrice", dailyPrice);
        }

        public IActionResult ForgetPassword()
        {
            return View("");
        }

        /// <summary>
        /// 登录失败后调用，刷新验证码，加密参数等
        /// </summary>
        /// <param name="msg"></param>
        private void ActionAfterReload(string msg)
        {
            ViewBag.ValidationStr = _imageTool.DrawValidationImg();
            EncryptCache(HttpContext);
            ViewBag.LoginResult = msg;
        }

        /// <summary>
        /// 将加密参数写入Cookie，Session
        /// </summary>
        /// <param name="context"></param>
        private void EncryptCache(HttpContext context)
        {
            //Get请求时，添加Frontend Cookie及Session，添加前端MD5次数Cookie MD5Iter
            Random rnd = new Random();
            string iter = rnd.Next(1, 501).ToString();
            Response.Cookies.Append("Frontend", MD5Helper.FrontendSalt,
                new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddDays(7) });
            Response.Cookies.Append("MD5Iter", iter,
                new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddMinutes(5) });
            context.Session.Set<string>("Frontend", iter);
            //Post请求时，若登录失败则：更新Cookie MD5Iter及Session Frontend.
            //若登录成功，则清除前端预处理次数的Cookie及Session
        }
    }
}
