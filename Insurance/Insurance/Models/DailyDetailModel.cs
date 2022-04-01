using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace Insurance.Models
{
    public class DailyDetailModel: ViewModelBase
    {
        [DatabaseProp]
        public string Company { get; set; }
        [DatabaseProp]
        public string SubmittedBy { get; set; }
        [DatabaseProp]
        public int NewAdd { get; set; }
        [DatabaseProp]
        public double TotalPrice { get; set; }
        [DatabaseProp]
        public int Reduce { get; set; }
        [DatabaseProp]
        public string Product { get; set; }
        [DatabaseProp]
        public DateTime Date { get; set; }
        public DataTable DetailTable { get; set; }
        public DataTable DetailTableByDate { get; set; }
    }

    public class DailyDetailDataModel : ViewModelBase
    {
        [DatabaseProp]
        public DateTime YMDDate { get; set; }      

        [DatabaseProp]
        public string Company { get; set; }

        [DatabaseProp]
        public int HeadCount { get; set; }

        [DatabaseProp]
        public double DailyPrice { get; set; }
        [DatabaseProp]
        public string Product { get; set; }

    }
}
