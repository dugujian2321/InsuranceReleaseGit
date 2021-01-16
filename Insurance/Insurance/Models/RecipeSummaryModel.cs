using System.Collections.Generic;
using VirtualCredit.Models;

namespace Insurance.Models
{
    public class RecipeSummaryModel : ViewModelBase
    {
        public string Company { get; set; } 
        public List<Company> CompanyList { get; set; }


    }
}
