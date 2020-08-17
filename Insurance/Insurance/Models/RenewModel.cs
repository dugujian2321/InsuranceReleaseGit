using System.Data;
using VirtualCredit.Models;

namespace Insurance.Models
{
    public class RenewModel : ViewModelBase
    {
        public DataTable MonthInfo { get; set; }
        public string NextMonthEndDay { get; set; }
        public string CurrentMonth { get; set; }

        public string Plan { get; set; }
    }
}
