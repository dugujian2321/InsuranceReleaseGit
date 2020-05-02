using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtualCredit.Interfaces
{
    interface IMail
    {
        void SendResetMail(string to, string userName,string token);
    }
}
