using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Services;

namespace Insurance.Models
{
    public class CaseModel
    {
        [DatabaseProp]
        public string CaseId { get; set; }
        [DatabaseProp]
        public string Submitter { get; set; }
        [DatabaseProp]
        public string Wounded { get; set; }
        [DatabaseProp]
        public decimal Price { get; set; }
        [DatabaseProp]
        public DateTime Date { get; set; }

        int state = 1;
        [DatabaseProp]
        public string State
        {
            get
            {
                return state == 0 ? "未结案" : "已结案";
            }
            set
            {
                if (value == "未结案") state = 0;
                if (value == "已结案") state = 1;
            }
        }
        [DatabaseProp]
        public string Detail { get; set; }
    }

    public enum CaseState
    {
        Open = 0, //未结案
        Close = 1,//已结案
    }
}
