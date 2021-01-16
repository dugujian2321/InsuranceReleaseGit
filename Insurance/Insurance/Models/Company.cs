using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Insurance.Models
{
    public class Company
    {
        public string Name { get; set; }

        /// <summary>
        /// 当前在保人数
        /// </summary>
        public int EmployeeNumber { get; set; }
        /// <summary>
        /// 总保费
        /// </summary>
        public double TotalCost { get; set; }

        /// <summary>
        /// 已结算
        /// </summary>
        public double CustomerAlreadyPaid { get; set; }

        /// <summary>
        /// 已赔付金额
        /// </summary>
        public double PaidCost { get; set; }
        public DateTime StartDate { get; set; }
        public double UnitPrice { get; set; }
        public double DailyCost { get; set; }
        public bool ViewDetail { get; set; } = false;
    }
}
