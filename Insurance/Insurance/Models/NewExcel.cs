using Microsoft.Net.Http.Headers;
using System;

namespace Insurance.Models
{
    public class NewExcel
    {
        public string Company { get; set; }

        public string UploadDate { get; set; }

        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public decimal Cost { get; set; }

        public int HeadCount { get; set; }

        public string Submitter { get; set; }

        public string Mode { get; set; }

        public string FileName { get; set; }

        public decimal Paid { get; set; }

        public decimal Unpaid
        {
            get;set;
        }
        public string Uploader { get; set; }
        public string Plan { get; set; }
        /// <summary>
        /// //结算状态 true-已结算 false-未结算
        /// </summary>
        public bool Status { get { return Cost - Paid == 0; } } 
    }


}
