using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using VirtualCredit.Models;

namespace Insurance.Models
{
    public class HistoricalModel : ViewModelBase
    {
        public string Company { get; set; }
        public List<Company> CompanyList { get; set; }
        public DataTable SummaryByYearTable { get; set; }
        public DateTime ProofDate { get; set; }
    }
}
