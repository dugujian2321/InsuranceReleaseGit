using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtualCredit.Interfaces
{
    interface ICOS
    {
        IActionResult GetWriteOnlyCredential();
        IActionResult GetReadOnlyCredential();
    }
}
