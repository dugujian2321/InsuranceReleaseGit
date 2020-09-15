using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Models;

namespace Insurance.Models
{
    public class SummaryModel : ViewModelBase
    {
        public List<Plan> PlanList;
    }

    public class Plan
    {
        public string Name { get; set; }
        [DataType(DataType.Currency)]
        public double TotalCost { get; set; }
        [DataType(DataType.Currency)]
        public double TotalPaid { get; set; }
        [DataType(DataType.Currency)]
        public double Unpaid { get { return (TotalCost - TotalPaid) == 0 ? 0 : 0.01 * (Math.Ceiling(100 * (TotalCost - TotalPaid)) + 1); } }

        public int HeadCount { get; set; }
    }
}
