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
        public double Unpaid
        {
            get
            {
                double result = TotalCost - TotalPaid;
                if (result * 1000 > Math.Floor(result * 100) * 10)
                {
                    return (result * 1000 + 1) / 1000;
                }
                else
                {
                    return Math.Round(result, 2);
                }
            }
        }

        public int HeadCount { get; set; }
    }
}
