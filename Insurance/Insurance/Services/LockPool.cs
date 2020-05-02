using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Threading;

namespace Insurance.Services
{
    public class ReaderWriterLockerWithName
    {
        public ReaderWriterLockSlim RWLocker { get; set; }
        public string LockerCompany { get; set; }
    }
}
