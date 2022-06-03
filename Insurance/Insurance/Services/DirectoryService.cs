using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit;
using VirtualCredit.Services;

namespace Insurance.Services
{

    public class DirectoryService
    {
        private static readonly object locker = new object();
        string adminDir = Path.Combine(Utility.Instance.ExcelRoot, "管理员");
        public void UpdateDirs()
        {
            var list = Directory.GetDirectories(adminDir, "*", SearchOption.AllDirectories);
            lock (locker)
            {
                Utility.CachedCompanyDirPath = list.ToList();
            }
            Task.Delay(30000);
        }
    }
}
