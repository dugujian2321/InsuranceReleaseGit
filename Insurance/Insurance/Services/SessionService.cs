using Microsoft.AspNetCore.Http;
using VirtualCredit.Models;

namespace VirtualCredit.Services
{
    public class SessionService
    {
        private UserInfoModel CurrUser(HttpContext hc)
        {
            return hc.Session.Get<UserInfoModel>("CurrentUser");
        }

        public static bool IsUserLogin(HttpContext hc)
        {
            UserInfoModel uim = new SessionService().CurrUser(hc);
            if (uim == null) return false;
            UserInfoModel databaseUser = InsuranceDatabaseService.UserInfo(uim);
            if (databaseUser == null) return false;
            return databaseUser.IsOnline == hc.Session.Id;
        }

        public static void SetUserOffline(HttpContext context)
        {
            context.Session.Set<bool>("Online", false);
        }

        public static void UserOnline(HttpContext context)
        {
            context.Session.Set<bool>("Online", true);
        }
    }
}