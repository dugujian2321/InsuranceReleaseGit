using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace VirtualCredit.Controllers
{
    public class ErrorController : VC_ControllerBase
    {

        public ErrorController(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor.HttpContext)
        {

        }
        [Route("Error/404")]
        public IActionResult PageNotFound()
        {
            return View("../Shared/PageNotFound");
        }
    }
}