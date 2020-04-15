﻿using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using VirtualCredit;
using VirtualCredit.LogServices;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace Insurance.Controllers
{
    public class AccountController : VC_ControllerBase
    {
        [HttpGet]
        public IActionResult Register()
        {
            UserInfoModel currUser = GetCurrentUser();
            if (currUser is null || currUser.AccessLevel > 1 || currUser.AllowCreateAccount != "1")
            {
                HttpContext.Session.Set<string>("noAccessCreateAccout", "当前用户权限不足");
                return View("../User/AccountManagement");
            }
            ViewBag.AllowCreateAccount = new SelectList(new List<string>() { "允许", "不允许" });
            ViewBag.Plans = new SelectList(new List<string>() { "30万", "60万", "80万" });
            Response.Cookies.Append("Frontend", MD5Helper.FrontendSalt,
       new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddDays(7) });
            ActionAfterReload(string.Empty);
            return View("../User/Register");
        }

        public AccountController(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor.HttpContext)
        {

        }

        [UserLoginFilters]
        public IActionResult Account()
        {
            return View("../User/AccountManagement", GetCurrentUser());
        }

        [UserLoginFilters]
        public IActionResult ViewAccounts()
        {
            try
            {
                UserInfoModel currUser = GetCurrentUser();
                DataTable dt = DatabaseService.SelectChildAccounts(currUser);
                ResetPwdModel model = new ResetPwdModel();
                model.CompanyName = currUser.CompanyName;
                model.CompanyNameAbb = currUser.CompanyNameAbb;
                model.Mail = currUser.Mail;
                model.Name = currUser.Name;
                model.RecipeAccount = currUser.RecipeAccount;
                model.RecipeAddress = currUser.RecipeAddress;
                model.RecipeBank = currUser.RecipeBank;
                model.RecipeCompany = currUser.RecipeCompany;
                model.RecipePhone = currUser.RecipePhone;
                model.TaxNum = currUser.TaxNum;
                model.Telephone = currUser.Telephone;
                model.userName = currUser.UserName;
                model.RecipeType = currUser.RecipeType;
                HttpContext.Session.Set("ChildAccounts", dt);
                return View("../User/ViewAccounts", model);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return View("Error");
            }
        }

        [HttpPost]
        [UserLoginFilters]
        public IActionResult ResetPassword(ResetPwdModel model)
        {
            HttpContext.Session.Set<string>("msg", string.Empty);
            var user = DatabaseService.SelectUser(model.userName);
            UserInfoModel currUser = GetCurrentUser();
            if (user is null)
            {
                HttpContext.Session.Set<string>("msg", "用户不存在,密码修改失败");
                return RedirectToAction("ViewAccounts");
            }
            if (user.AccessLevel <= currUser.AccessLevel && !user.UserName.Equals(currUser.UserName, StringComparison.CurrentCultureIgnoreCase))
            {
                HttpContext.Session.Set<string>("msg", "权限不足,密码修改失败");
                return RedirectToAction("ViewAccounts");
            }
            if (model.newPassword != model.confirmNewPassword)
            {
                HttpContext.Session.Set<string>("msg", "两次输入的密码不一致,密码修改失败");
                return RedirectToAction("ViewAccounts");
            }
            //MD5Helper md5 = new MD5Helper();
            //model.newPassword = md5.EncryptNTimesWithBackendSalt(model.newPassword, 500);
            user.userPassword = model.newPassword;
            user.ResetPwd = string.Empty;
            user.Token_Reset = string.Empty;
            user.ExpiredTime = 0;
            bool res = DatabaseService.UpdateUserInfo(user, new List<string>() { "userPassword" });
            if (res)
            {
                HttpContext.Session.Set<string>("msg", "密码修改成功");
            }
            return RedirectToAction("ViewAccounts");
        }
        /// <summary>
        /// 注册内容非法时调用，刷新验证码，加密参数等
        /// </summary>
        /// <param name="msg"></param>
        private void ActionAfterReload(string msg)
        {
            ViewBag.ValidationStr = _imageTool.DrawValidationImg();
            ViewBag.LoginResult = msg;
        }

        [HttpGet]
        [UserLoginFilters]
        public JsonResult GetAccountDetail(string userName)
        {
            //浏览已登录信息 或 浏览子账号信息
            UserInfoModel currUser = GetCurrentUser();
            HttpContext.Session.Set("editUser", userName);
            if (currUser == null)
            {
                return Json("fail");
            }
            UserInfoModel uim = DatabaseService.SelectUser(userName);
            uim.userPassword = string.Empty;
            uim.UserNameEdit = userName;
            if (currUser.AccessLevel == 0) //管理员账号读取其他公司账号
            {
                uim.AllowEdit = true;
                return Json(uim);
            }

            if (currUser.AccessLevel > 0 && currUser.UserName.Equals(userName, StringComparison.CurrentCultureIgnoreCase))
            {
                uim.AllowEdit = false;
                return Json(uim);
            }

            if (currUser.AccessLevel > 0 && uim.Father.Equals(currUser.UserName, StringComparison.CurrentCultureIgnoreCase)
                && uim.CompanyName.Equals(currUser.CompanyName, StringComparison.CurrentCultureIgnoreCase)) //非管理员账号：可以读取其创建的账号及其自身
            {
                uim.AllowEdit = true;
                return Json(uim);
            }
            return Json("fail");
        }


        [HttpGet]
        public bool UserNameExists()
        {
            string userName = Request.Query["userName"]; //读取get请求中的userName参数
            if (string.IsNullOrEmpty(userName))
            {
                return false;
            }

            if (!DatabaseService.UserMatchUserNameOnly(new UserInfoModel() { UserName = userName }))
            {
                return false;
            }

            return true;
        }

        [UserLoginFilters]
        public bool UpdateAccountInfo(ResetPwdModel model)
        {
            UserInfoModel currUser = GetCurrentUser();
            string user = HttpContext.Session.Get<string>("editUser");
            if (currUser.AccessLevel != 0 && (currUser.UserName.Equals(user, StringComparison.CurrentCultureIgnoreCase))) //管理员账号读取其他公司账号
            {
                return false;
            }

            UserInfoModel uim = DatabaseService.SelectUser(user);
            uim._Plan = model._Plan;
            uim.UnitPrice = model.UnitPrice;
            uim.DaysBefore = model.DaysBefore;
            uim.AllowCreateAccount = model.AllowCreateAccount;
            if (model.AllowCreateAccount == "1")
            {
                uim.AccessLevel = 1;
            }
            else
            {
                uim.AccessLevel = 2;
            }
            List<string> paras = new List<string>()
            {
                "AccessLevel","AllowCreateAccount","DaysBefore","_Plan","UnitPrice"
            };
            if (DatabaseService.UpdateUserInfo(uim, paras))
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        [UserLoginFilters]
        public bool UpdatePersonalInfo(ViewModelBase model)
        {
            UserInfoModel currUser = GetCurrentUser();
            string user = HttpContext.Session.Get<string>("editUser");
            if (currUser.AccessLevel != 0 && (currUser.UserName.Equals(user, StringComparison.CurrentCultureIgnoreCase))) //管理员账号读取其他公司账号
            {
                return false;
            }

            UserInfoModel uim = DatabaseService.SelectUser(user);
            uim.CompanyNameAbb = model.CompanyNameAbb;
            uim.Mail = model.Mail;
            uim.TaxNum = model.TaxNum;
            uim.Telephone = model.Telephone;
            uim.Name = model.Name;
            uim.RecipeAccount = model.RecipeAccount;
            uim.RecipeAddress = model.RecipeAddress;
            uim.RecipeBank = model.RecipeBank;
            uim.RecipeCompany = model.RecipeCompany;
            uim.RecipePhone = model.RecipePhone;
            uim.RecipeType = model.RecipeType;
            List<string> paras = new List<string>()
            {
                "CompanyNameAbb","Mail","TaxNum","Telephone","Name","RecipeAccount",
                "RecipeAddress","RecipeBank","RecipeCompany","RecipePhone","RecipeType"
            };
            if (DatabaseService.UpdateUserInfo(uim, paras))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        [HttpPost]
        [AdminFilters]
        public ActionResult Register(NewUserModel user)
        {
            UserInfoModel currUser = GetCurrentUser();
            if (currUser is null || currUser.AccessLevel > 1 || currUser.AllowCreateAccount == "0")
            {
                HttpContext.Session.Set<string>("noAccessCreateAccout", "当前用户权限不足");
                return View("../User/AccountManagement");
            }
            if (currUser.AccessLevel == 1)
            {
                if (user.CompanyName != currUser.CompanyName)
                {
                    return View("Error");
                }
            }
            if (!ModelState.IsValid)
            {
                HttpContext.Session.Set<string>("noAccessCreateAccout", "输入信息不合规范");
                ActionAfterReload("输入信息不合规范");
                return View();
            }
            bool pass = true;

            #region 验证验证码
            //当用户点击提交按钮后，后端验证验证码
            string userInputValidation = string.Empty;
            userInputValidation = HttpContext.Request.Form["validationResult"]; //读取name = validationResult的input的值
            int backendValidation = HttpContext.Session.Get<int>("ValidationResult");
            if (Convert.ToInt16(userInputValidation) != backendValidation)
            {
                HttpContext.Session.Set("noAccessCreateAccout", "验证码计算错误，请重新输入！");
                ActionAfterReload("验证码计算错误，请重新输入！");
                return View();
            }
            #endregion

            if (DatabaseService.UserMatchUserNameOnly(user))
            {
                HttpContext.Session.Set<string>("noAccessCreateAccout", "用户名已存在");
                ViewBag.UserNameUsed = "用户名已存在";
                pass = false;
            }
            if (user.userPassword != user.confirmPassword)
            {
                HttpContext.Session.Set<string>("noAccessCreateAccout", "两次输入的密码不一致，请重新输入");
                ViewBag.PwdNotMatch = "两次输入的密码不一致，请重新输入";
                pass = false;
            }
            if (!pass)
            {
                HttpContext.Session.Set<string>("noAccessCreateAccout", "输入信息不合规范");
                ActionAfterReload("输入信息不合规范");
                return View();
            }
            MD5Helper md5 = new MD5Helper();
            //user.userPassword = md5.EncryptNTimesWithBackendSalt(user.userPassword, 500);
            TimeSpan ts1 = DateTime.UtcNow.AddMinutes(10) - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long temp = Convert.ToInt64(ts1.TotalSeconds);
            if (user.AllowCreateAccount == "1")
            {
                user.AccessLevel = 1;
            }
            else
            {
                user.AccessLevel = 2;
            }
            user.Father = currUser.UserName;

            List<Company> companies = GetAllCompanies(Path.Combine(Utility.Instance.WebRootFolder, "Excel"));

            var t = companies.Where(p => p.Name == user.CompanyName);

            if (t.Count() == 0)
            {
                if (!ExcelTool.CreateNewCompanyTable(user.CompanyName))
                {
                    return View("Error");
                }
            }

            if (DatabaseService.InsertStory("UserInfo", user))
            {
                LogService.Log($"New User {user.UserName} Registered");
                return View("../User/RegisterSucceed");
            }
            else
            {
                return View("../Shared/Error");
            }

        }
    }
}