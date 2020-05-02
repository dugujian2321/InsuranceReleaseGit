using System.ComponentModel.DataAnnotations;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit
{
    public abstract class IUser : ViewModelBase
    {
        [DatabaseProp]
        [Required(AllowEmptyStrings = false, ErrorMessage = "用户名不能为空")]
        [MaxLength(18, ErrorMessage = "用户名最长为18个字符")]
        [MinLength(2, ErrorMessage = "用户名最短为2个字符")]
        [RegularExpression("^[a-zA-Z\u4E00-\u9FA5]{1}[a-zA-Z0-9_\u4E00-\u9FA5]{1,17}$", ErrorMessage = "用户名只能以字母或汉字开头，可包含汉字、字母、数字及下划线，最短为2个字符，最长为18个字符")]
        public string UserName { get; set; }

        [DatabaseProp]
        [Required(ErrorMessage = "密码不能为空")]
        [DataType(DataType.Password)]
        //[MinLength(7, ErrorMessage = "密码最短为7个字符")]
        //[MaxLength(20, ErrorMessage = "2密码最长为20个字符")]
        public string userPassword { get; set; }
    }
}
