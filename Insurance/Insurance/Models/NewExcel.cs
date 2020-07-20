using System;

namespace Insurance.Models
{
    public class NewExcel
    {
        public string Company { get; set; }

        public string UploadDate { get; set; }

        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public double Cost { get; set; }

        public int HeadCount { get; set; }

        public string Submitter { get; set; }

        public string Mode { get; set; }

        public string FileName { get; set; }

        public double Paid { get; set; }

        public double Unpaid { get; set; }
        public string Uploader { get; set; }
        public string Plan { get; set; }
    }


}
