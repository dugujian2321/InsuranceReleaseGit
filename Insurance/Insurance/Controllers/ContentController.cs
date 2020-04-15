using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Insurance.Controllers
{
    public class ContentController : Controller
    {
        public IActionResult ProjectDetail()
        {
            return View("Project");
        }
    }
}