using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit
{
    public class MiniAppLoginFilters : Attribute, IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {
            //throw new NotImplementedException();
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var res = SessionService.IsUserLogin(context.HttpContext);
            if (!res)
            {
                SessionService.SetUserOffline(context.HttpContext);
                context.Result = new RedirectToActionResult(actionName: "Login", controllerName: "Login", routeValues: null);
            }
        }
    }
    public class UserLoginFilters : Attribute, IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {
            //throw new NotImplementedException();
        }


        public void OnActionExecuting(ActionExecutingContext context)
        {
            var res = SessionService.IsUserLogin(context.HttpContext);
            if (!res)
            {
                SessionService.SetUserOffline(context.HttpContext);
                context.Result = new RedirectToActionResult(actionName: "Login", controllerName: "Login", routeValues: null);
            }
        }
    }

    public class AdminFilters : UserLoginFilters, IActionFilter
    {
        public new void OnActionExecuting(ActionExecutingContext context)
        {
            var res = SessionService.IsUserLogin(context.HttpContext);
            if (!res)
            {
                SessionService.SetUserOffline(context.HttpContext);
                context.Result = new RedirectToActionResult(actionName: "Login", controllerName: "Login", routeValues: null);
            }
            if (context.HttpContext.Session.Get<UserInfoModel>("CurrentUser").AccessLevel > 1)
            {
                (context.Controller as Controller).TempData["noAccess"] = "该账号无权创建新账号";
                context.Result = new RedirectToActionResult(actionName: "Account", controllerName: "Account", routeValues: null);
            }
        }
    }
}
