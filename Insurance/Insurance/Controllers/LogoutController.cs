using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit.Controllers
{
    public class LogoutController : VC_ControllerBase
    {
        [UserLoginFilters]
        public IActionResult Logout()
        {
            UserInfoModel user = HttpContext.Session.Get<UserInfoModel>("CurrentUser");
            if (user == null)
            {
                return RedirectToAction(actionName: "Index", controllerName: "Home");
            }
            user.IsOnline = null;
            InsuranceDatabaseService.UpdateUserInfo(user, new List<string>() { "IsOnline" });
            HttpContext.Session.Set<UserInfoModel>("CurrentUser", null);
            SessionService.SetUserOffline(HttpContext);
            return View();
        }
    }
}