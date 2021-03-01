using Microsoft.AspNetCore.Mvc;
using VirtualCredit;
using MiniApp.Helper;
using VirtualCredit.Models;
using System.Net.Http;
using System.Threading.Tasks;

namespace Insurance.Controllers
{
    [ApiController]
    [Route("mini/[controller]")]
    public class MiniAppLoginController : MiniAppControllerBase
    {
        [Route("[action]")]
        public async Task<IActionResult> Login(string userName, string password, string openId)
        {

            HttpClient httpClient = new HttpClient();
            HttpResponseMessage str = await httpClient.GetAsync($@"https://api.weixin.qq.com/sns/jscode2session?appid=wxabac8813c1db09ae&secret=SECRET&js_code={openId}&grant_type=authorization_code");
                       
            LoginReturnModel result = new LoginReturnModel();
            IUser tryLoginUser = new UserInfoModel();
            tryLoginUser.UserName = userName;
            tryLoginUser.userPassword = password;
            var uim = DatabaseService.UserMatchUserNamePassword(tryLoginUser);
            if (uim is null)
            {
                result.Message = "用户名或密码错误";
            }
            else
            {
                result.Message = "登录成功";
                OnlineUsers.Add(uim);
            }

            return Ok(result);
        }
    }

    public class LoginReturnModel
    {
        public string Message { get; set; }
    }
}
