using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Models;

namespace Insurance.Models
{
    public class AccountData
    {
        public UserInfoModel Account { get; set; }
        public int HeadCount { get; set; } //该账号当前在保人数
        public double PricePerMonth { get; set; }
        public double PricePerDay
        {
            get
            {
                DateTime now = DateTime.Now;
                return PricePerMonth / DateTime.DaysInMonth(now.Year, now.Month);
            }
        }


    }
}
