using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Insurance.Models
{
    public class Employee
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public string JobType { get; set; }
        public string Job { get; set; }
        public string DataDesc { get; set; }
        public string Company { get; set; }
        public bool Valid { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }
}
