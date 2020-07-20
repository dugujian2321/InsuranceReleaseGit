using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Models;

namespace Insurance.Models
{
    public class EmployeeChangeModel : ViewModelBase
    {
        public List<Company> CompanyList { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public int DaysBefore { get; set; }

        public string AllowedStartDate { get; set; }
        public string Plans { get; set; }
    }
}
