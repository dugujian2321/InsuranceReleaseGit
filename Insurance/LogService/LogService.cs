using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace VirtualCredit.LogServices
{
    public class LogService
    {
        public static string LogFile { get; set; }

        static LogService()
        {
            string LogFolder = @"..\AppLog";
            LogFile = Path.Combine(LogFolder, "log.txt");
            if (!File.Exists(LogFile))
            {
                if (!Directory.Exists(LogFolder))
                {
                    Directory.CreateDirectory(LogFolder);
                }
                File.Create(LogFile).Close();
            }
        }

        private readonly static object lockObj = new object();
        public static void Log(string str)
        {
            lock (lockObj)
            {
                StackFrame stackFrame = new StackTrace(true).GetFrame(1);
                string beforeCallFileName = stackFrame.GetFileName();
                string beforeCall = stackFrame.GetMethod().ToString();
                int beforeCallFileLine = stackFrame.GetFileLineNumber();
                str = beforeCallFileName + " Line:" + beforeCallFileLine + " " + str;
                str = DateTime.Now.ToString() + "\t" + str;
                File.AppendAllLines(LogFile, new List<string> { str });
            }
        }
    }
}
