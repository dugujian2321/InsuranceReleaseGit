using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit.Controllers
{
    public class LogoutController : VC_ControllerBase
    {
        public LogoutController(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor.HttpContext)
        {

        }

        [UserLoginFilters]
        public IActionResult Logout()
        {
            UserInfoModel user = CurrentSession.Get<UserInfoModel>("CurrentUser");
            if (user == null)
            {
                return RedirectToAction(actionName: "Index", controllerName: "Home");
            }
            user.IsOnline = null;
            DatabaseService.UpdateUserInfo(user, new List<string>() { "IsOnline" });
            CurrentSession.Set<UserInfoModel>("CurrentUser", null);
            SessionService.SetUserOffline(HttpContext);
            return View();
        }
    }
}