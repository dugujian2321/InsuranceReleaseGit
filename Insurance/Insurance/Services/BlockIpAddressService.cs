using Microsoft.AspNetCore.Http;
using System;
using System.Data;
using VirtualCredit.Models;

namespace VirtualCredit.Services
{
    public class BlockIpAddressService
    {
        static string IllegalIPTable = "illegal_IP";

        public static void ClearErrorTime(HttpContext httpContext)
        {

        }

    }
}
