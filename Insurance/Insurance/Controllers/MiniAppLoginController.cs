using Microsoft.AspNetCore.Mvc;
using VirtualCredit;
using VirtualCredit.Models;
using System.Net.Http;
using System.Threading.Tasks;
using VirtualCredit.Services;
using VirtualCredit.LogServices;
using System.Collections.Generic;
using System.Linq;
using Insurance.Services;
using Insurance.Models;
using VirtualCredit.Controllers;
using Microsoft.AspNetCore.Http;

namespace Insurance.Controllers
{
    [ApiController]
    [Route("mini/[controller]")]
    public class MiniAppLoginController : LoginController
    {

        public MiniAppLoginController(IHttpContextAccessor accessor) : base(accessor)
        {

        }
        [Route("[action]")]
        public async Task<IActionResult> Login(string userName, string password, string openId)
        {
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage str = await httpClient.GetAsync($@"https://api.weixin.qq.com/sns/jscode2session?appid=wxabac8813c1db09ae&secret=4d6008f5585d7b34e70910b33f4e23a4&js_code=" + openId + @"&grant_type=authorization_code");
            if (str.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return new JsonResult("腾讯接口返回异常");
            }

            string responseTxt = await str.Content.ReadAsStringAsync();
            LoginReturnModel result = Newtonsoft.Json.JsonConvert.DeserializeObject<LoginReturnModel>(responseTxt);
            IUser tryLoginUser = new UserInfoModel();
            tryLoginUser.UserName = userName;
            tryLoginUser.userPassword = password;
            var uim = DatabaseService.UserMatchUserNamePassword(tryLoginUser);
            UserInfoModel user = new UserInfoModel();
            if (uim is null)
            {
                return StatusCode(401,"用户名或密码不正确");
            }
            else
            {
                user.UserName = userName;
                user.userPassword = password;
                user.CompanyName = uim.CompanyName;
                user.IsOnline = CurrentSession.Id;
                user.IPAddress = "";
                if (!DatabaseService.UpdateUserInfo(user, new List<string>() { "IPAddress", "IsOnline" }))
                {
                    return new JsonResult("登录状态更新失败");
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
                CurrentSession.Set("CurrentUser", user);
                CurrentSession.Set<string>("Frontend", null);
                UpdateDailyData();
                if (MiniAppSessions.Keys.Contains(result.OpenId))
                {
                    MiniAppSessions[result.OpenId] = CurrentSession;
                }
                else
                    MiniAppSessions.TryAdd(result.OpenId, CurrentSession);
                return Ok(result);
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
            CurrentSession.Set("DailyHeadCount", dailyHeadCount);
            CurrentSession.Set("DailyPrice", dailyPrice);
        }
    }

    public class LoginReturnModel
    {
        public string OpenId { get; set; }
        public string Session_Key { get; set; }
    }
}
