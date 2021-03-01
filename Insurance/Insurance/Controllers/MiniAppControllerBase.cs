using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Models;

namespace Insurance.Controllers
{
    public class MiniAppControllerBase : ControllerBase
    {
        public static List<UserInfoModel> OnlineUsers = new List<UserInfoModel>();
    }
}
