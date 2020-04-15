using System.ComponentModel.DataAnnotations;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace Insurance.Models
{
    public class ResetPwdModel : ViewModelBase
    {
        [DatabaseProp]
        [MaxLength(12, ErrorMessage = "用户名最长为12个字符")]
        [MinLength(2, ErrorMessage = "用户名最短为2个字符")]
        [RegularExpression("^[a-zA-Z\u4E00-\u9FA5]{1}[a-zA-Z0-9_\u4E00-\u9FA5]{1,11}$", ErrorMessage = "用户名只能以字母或汉字开头，可包含汉字、字母、数字及下划线，最短为2个字符，最长为12个字符")]
        public string userName { get; set; }

        [Required(ErrorMessage = "密码不能为空")]
        [DataType(DataType.Password)]
        public string newPassword { get; set; }

        [Required(ErrorMessage = "确认密码不能为空")]
        [DataType(DataType.Password)]
        public string confirmNewPassword { get; set; }

        [DatabaseProp]
        public string Token { get; set; }

        [DatabaseProp]
        public string ExpiredTime { get; set; }

        public UserInfoModel CurrUser { get; set; }

        [DatabaseProp]
        public int DaysBefore { get; set; }

        [DatabaseProp]
        public string _Plan { get; set; }

        [DatabaseProp]
        public double UnitPrice { get; set; }

        private string allowCreateAccount;

        [DatabaseProp]
        public string AllowCreateAccount
        {
            get { return allowCreateAccount; }
            set
            {
                if (value.ToString() == "允许")
                {
                    allowCreateAccount = "1";
                }
                else
                {
                    allowCreateAccount = "2";
                }
            }
        }
    }
}
