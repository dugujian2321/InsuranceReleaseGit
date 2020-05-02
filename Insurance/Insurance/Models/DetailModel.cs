using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Models;

namespace Insurance.Models
{
    public class DetailModel : ViewModelBase
    {
        public string Company { get; set; }

        public List<NewExcel> Excels { get; set; }

        public List<NewExcel> MonthlyExcel { get; set; }
    }
}
