using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.IO;
using VirtualCredit.Models;

namespace Insurance.Models
{
    public class HistoricalModel : ViewModelBase
    {
        public string Company { get; set; }
        public List<Company> CompanyList { get; set; }
    }
}
