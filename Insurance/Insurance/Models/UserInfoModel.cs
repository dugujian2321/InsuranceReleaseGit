using System.Collections.Generic;
using System.Linq;
using VirtualCredit.Services;

namespace VirtualCredit.Models
{
    public class UserInfoModel : IUser
    {
        //[DatabaseProp]
        //[Required(AllowEmptyStrings = false, ErrorMessage = "用户名不能为空")]
        //[MaxLength(18, ErrorMessage = "用户名最长为18个字符")]
        //public string UserName { get; set; }

        //[DatabaseProp]
        //[Required(ErrorMessage = "密码不能为空")]
        //[DataType(DataType.Password)]
        //public string userPassword { get; set; }

        [DatabaseProp]
        public string IsOnline { get; set; }

        [DatabaseProp]
        public string IPAddress { get; set; }

        [DatabaseProp]
        public string ResetPwd { get; set; }

        [DatabaseProp]
        public string Token_Reset { get; set; }

        [DatabaseProp]
        public long ExpiredTime { get; set; }

        [DatabaseProp]
        public string CompanyName { get; set; }

        [DatabaseProp]
        public int AccessLevel { get; set; }

        [DatabaseProp]
        public int DaysBefore { get; set; }

        [DatabaseProp]
        public double UnitPrice { get; set; }

        [DatabaseProp]
        public string AllowCreateAccount { get; set; }

        [DatabaseProp]
        public string _Plan { get; set; }

        [DatabaseProp]
        public string Father { get; set; }

        private List<UserInfoModel> childAccounts;

        public List<UserInfoModel> ChildAccounts
        {
            get
            {
                if (childAccounts != null) return childAccounts;
                var children = DatabaseService.Select("UserInfo").Select().Where(_ => _[nameof(UserInfoModel.Father)].ToString() == UserName);
                childAccounts = new List<UserInfoModel>();
                foreach (var item in children)
                {
                    childAccounts.Add(DatabaseService.SelectUser(item[nameof(UserInfoModel.UserName)].ToString()));
                }
                return childAccounts;
            }
            set
            {
                childAccounts = value;
            }
        }

        public int StartDate { get; set; }
        public bool AllowEdit { get; set; }
    }
}
