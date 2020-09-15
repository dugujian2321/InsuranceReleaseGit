using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataMigration
{
    class Program
    {
        static void Main(string[] args)
        {
            string dir = @"E:\VisualStudioProduct-Self Use\InsuranceRelease\Git\Insurance\Insurance\wwwroot\Excel\管理员";
            foreach (var d in Directory.GetDirectories(dir,"*"))
            {
                DirectoryInfo oldDir = new DirectoryInfo(d);
                string plan = Path.Combine(d, "60万");
                DirectoryInfo di = new DirectoryInfo(plan);
                if (!di.Exists) di.Create();
                foreach (FileInfo file in oldDir.GetFiles())
                {
                    file.MoveTo(Path.Combine(di.FullName,file.Name));
                }
                foreach (var folder in oldDir.GetDirectories())
                {
                    if (folder.Name == di.Name) continue;
                    folder.MoveTo(Path.Combine(di.FullName,folder.Name));
                }
            }
        }
    }
}
