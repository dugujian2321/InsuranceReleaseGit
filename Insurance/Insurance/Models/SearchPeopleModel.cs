using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace Insurance.Models
{
    public class SearchPeopleModel: ViewModelBase
    {
        public string Company { get; set; }
        public Employee People { get; set; }
        public DataTable Result { get; set; }
    }
}
