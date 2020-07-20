using System.ComponentModel.DataAnnotations;
using VirtualCredit;
using VirtualCredit.Services;

namespace Insurance.Models
{
    public class NewUserModel : IUser
    {
        [Required(ErrorMessage = "确认密码不能为空")]
        [DataType(DataType.Password)]
        public string confirmPassword { get; set; }

        [Required(ErrorMessage = "公司名称不能为空")]
        [DataType(DataType.Text)]
        [DatabaseProp]
        public new string CompanyName { get; set; }

        [DatabaseProp]
        public int AccessLevel { get; set; }

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
                if(value.ToString() == "允许")
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
