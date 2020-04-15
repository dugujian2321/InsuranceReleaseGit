using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Insurance.Models
{
    public class Company
    {
        public string Name { get; set; }
        public int EmployeeNumber { get; set; }
        public double TotalCost { get; set; }
        public double PaidCost { get; set; }
        public DateTime StartDate { get; set; }
        public double UnitPrice { get; set; }
    }
}
