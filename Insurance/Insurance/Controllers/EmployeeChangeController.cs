using Insurance.Models;
using Insurance.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using VirtualCredit;
using VirtualCredit.LogServices;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace Insurance.Controllers
{
    public class EmployeeChangeController : VC_ControllerBase
    {
        protected readonly int idCol = 1;
        protected readonly int jobCol = 2;
        public static string ExcelDirectory;
        protected string companyFolder;
        string summaryFileName;
        protected string summaryFilePath;
        protected string targetCompany;
        private static readonly object summaryLocker = new object();

        public EmployeeChangeController(IHostingEnvironment hostingEnvironment, IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor.HttpContext)
        {
            _hostingEnvironment = hostingEnvironment;
            ExcelDirectory = _hostingEnvironment.WebRootPath;
        }

        protected void Initialize(string company)
        {
            Initialize(company, string.Empty);
        }


        protected void Initialize(string company, string plan)
        {
            if (string.IsNullOrEmpty(company)) company = CurrentSession.Get<string>("company");
            companyFolder = GetSearchExcelsInDir(company);
            targetCompany = company;
            summaryFileName = company + ".xls";
            summaryFilePath = Path.Combine(companyFolder, plan, summaryFileName);
        }



        [UserLoginFilters]
        public JsonResult UpdateSummary(DateTime startdate, string plan)
        {
            var currUser = GetCurrentUser();
            try
            {
                //判断是否已计算保费
                if (string.IsNullOrEmpty(CurrentSession.Get<string>("readyToSubmit")) || CurrentSession.Get<string>("readyToSubmit") != "Y")
                {
                    return Json("NotCalculated");
                }
                string summary_backup = string.Empty;
                string targetcompany = CurrentSession.Get<string>("company");
                //验证前后端价格计算是否一致
                double price = CurrentSession.Get<double>("price");
                double calculatedprice = CalculatePrice(startdate, targetcompany, CurrentSession.Get<string>("plan"));
                if (price != calculatedprice)
                {
                    return Json("NotCalculated");
                }

                var employees = CurrentSession.Get<List<Employee>>("validationResult");
                if (employees == null || employees.Count <= 0 || employees.Any<Employee>(_ => _.Valid == false))
                {
                    return Json(new List<string> { "信息错误", "", "" });
                }
                //===================================================//
                if (string.IsNullOrEmpty(summaryFilePath))
                {
                    Initialize(targetcompany);
                }
                string mode = CurrentSession.Get<string>("mode");
                string currUserDir = GetSearchExcelsInDir(currUser.CompanyName);
                var di = new DirectoryInfo(currUserDir);
                string excelsDirectory = string.Empty;
                if (di.Name == targetcompany)
                {
                    excelsDirectory = currUserDir;
                }
                else
                {
                    excelsDirectory = Directory.GetDirectories(currUserDir, targetcompany, SearchOption.AllDirectories).FirstOrDefault();
                }

                if (string.IsNullOrEmpty(excelsDirectory))
                {
                    return Json("No Permission");
                }
                string month = GetMonthFolder(startdate);
                currUser.MyLocker.RWLocker.EnterWriteLock(); //进入写锁
                string monthDir = Path.Combine(excelsDirectory, plan, month);
                if (!Directory.Exists(monthDir))
                {
                    Directory.CreateDirectory(monthDir);
                }
                //=========================================================================================================//

                ExcelTool summary = new ExcelTool(summaryFilePath, "Sheet1");
                DateTime currentMonth;
                if (summary.GetCurrentDate() != string.Empty)
                {
                    if (!DateTime.TryParse(summary.GetCurrentDate(), out currentMonth) || currentMonth.Month != startdate.Month) //验证总表中当前月份与投保月份是否一致
                    {
                        List<string> result = new List<string>();
                        result.Add("投保失败，投保日期不正确或本月保单已因续保锁定");
                        result.Add("");
                        result.Add("");
                        return Json(result);
                    }
                }
                else
                {
                    //if (startdate.Month != DateTime.Now.Month)
                    //{
                    //    List<string> result = new List<string>();
                    //    result.Add($"投保失败，{startdate.ToString("yyyy年MM月")}的保单尚未生成");
                    //    result.Add(string.Empty);
                    //    result.Add(string.Empty);
                    //    return Json(result);
                    //}
                }



                //===========备份总表，准备操作=====================
                summary_backup = Path.Combine(companyFolder, Guid.NewGuid() + "_summary_temp.xls");
                System.IO.File.Copy(summaryFilePath, summary_backup, true);
                //======================添加新员工===================

                int headCount = 0;

                if (mode == "add")
                {
                    string fileName = DateTime.Now.ToString("yyyy-MM-dd") + $"@{price}@{currUser.UserName}@Add@{Guid.NewGuid()}" + DateTime.Now.ToString("@HH-mm-ss") + "@0@.xls"; //命名规则： 上传日期_保费_上传账号_加/减保_GUID_时间_已结保费.xls
                    string newfilepath = Path.Combine(excelsDirectory, plan, month, fileName);
                    try
                    {
                        //1 - 新员工excel中，添加生效日期,然后保存文件
                        //创建新excel文档
                        using (FileStream fs = System.IO.File.Create(newfilepath))
                        {
                            var excel = CurrentSession.Get("newExcel");
                            MemoryStream ms = new MemoryStream(excel);
                            ms.CopyTo(fs);
                            fs.Flush();
                        }
                        ExcelTool et = new ExcelTool(newfilepath, "Sheet1");
                        headCount = et.GetEmployeeNumber();
                        et.SetCellText(0, 4, "保障开始时间");
                        et.SetCellText(0, 5, "保障结束时间");
                        for (int i = 1; i <= et.m_main.GetLastRow(); i++)
                        {
                            et.SetCellText(i, 4, startdate.Date.ToString("yyyy/MM/dd"));
                        }
                        et.Save();

                        //2 - 总表中添加新员工信息
                        int startrow = summary.m_main.GetLastRow() + 1;
                        string company = currUser.CompanyName;
                        for (int i = startrow; i < startrow + employees.Count; i++)
                        {
                            Employee em = employees[i - startrow];
                            summary.SetCellText(i, 0, i.ToString()); //序号
                            summary.SetCellText(i, 1, targetcompany); //单位
                            summary.SetCellText(i, 2, em.Name); //姓名
                            summary.SetCellText(i, 3, em.ID.ToUpper()); //ID
                            summary.SetCellText(i, 4, em.JobType); //险种
                            summary.SetCellText(i, 5, em.Job); //岗位
                            summary.SetCellText(i, 6, startdate.Date.ToShortDateString()); //生效日期
                        }
                        summary.Save();
                        List<string> result = new List<string>();
                        result.Add("投保成功");

                        DailyDetailModel detail = new DailyDetailModel()
                        {
                            Company = targetcompany,
                            SubmittedBy = currUser.UserName,
                            Product = plan,
                            TotalPrice = price,
                            Date = DateTime.Now.Date,
                            NewAdd = headCount
                        };
                        UpdateDailyDetail(detail);
                        string kickoffDate = startdate.Date.ToString("yyyy/MM/dd 00:00:01");
                        string endDate = (new DateTime(startdate.Year, startdate.Month, DateTime.DaysInMonth(startdate.Year, startdate.Month))).ToString("yyyy/MM/dd 23:59:59");
                        result.Add(kickoffDate);
                        result.Add(endDate);
                        ClearSession();
                        Utility.DailyAdd += headCount;
                        Utility.DailyTotalCost += price;
                        return Json(result);

                    }
                    catch (Exception e)
                    {
                        RevertSummaryFile(summaryFilePath, summary_backup);
                        return Json("failed");
                    }
                    finally
                    {
                        if (System.IO.File.Exists(summary_backup))
                            System.IO.File.Delete(summary_backup);
                    }
                }
                else if (mode == "sub")
                {
                    string fileName = DateTime.Now.ToString("yyyy-MM-dd") + $"@{price}@{currUser.UserName}@Sub@{Guid.NewGuid()}" + DateTime.Now.ToString("@HH-mm-ss") + "@0@.xls"; //命名规则： 上传日期_保费_上传账号_加/减保_GUID_时间_已结保费_.xls
                    string newfilepath = Path.Combine(excelsDirectory, plan, month, fileName);
                    try
                    {
                        //1 - 新表中添加离职信息，并保存文件
                        using (FileStream fs = System.IO.File.Create(newfilepath))
                        {
                            var excel = CurrentSession.Get("newExcel");
                            MemoryStream ms = new MemoryStream(excel);
                            ms.CopyTo(fs);
                            fs.Flush();
                        }
                        ExcelTool et = new ExcelTool(newfilepath, "Sheet1");
                        headCount = et.GetEmployeeNumber();
                        et.SetCellText(0, 4, "保障开始时间");
                        et.SetCellText(0, 5, "保障结束时间");
                        Employee employee = null;
                        for (int i = 1; i <= et.m_main.GetLastRow(); i++)
                        {
                            string id = et.GetCellText(i, 1, ExcelTool.DataType.String);
                            employee = summary.SelectByID(id);
                            et.SetCellText(i, 5, startdate.Date.ToString("yyyy/MM/dd"));
                            et.SetCellText(i, 4, employee.StartDate);
                        }
                        et.Save();

                        //2 - 总表中将离职员工信息删除
                        DataTable tbl_summary = summary.ExcelToDataTable("Sheet1", true);
                        foreach (Employee guest in employees)
                        {
                            var row = from DataRow p in tbl_summary.Rows where p[3].ToString().Equals(guest.ID, StringComparison.InvariantCultureIgnoreCase) select tbl_summary.Rows.IndexOf(p);
                            foreach (int item in row)
                            {
                                tbl_summary.Rows.RemoveAt(item);
                                break;
                            }
                        }
                        summary.DatatableToExcel(tbl_summary);
                        List<string> result = new List<string>();
                        result.Add("投保成功");
                        string kickoffDate = employee.StartDate + " 00:00:01";
                        string endDate = startdate.ToString("yyyy/MM/dd 23:59:59");
                        result.Add(kickoffDate);
                        result.Add(endDate);
                        DailyDetailModel detail = new DailyDetailModel()
                        {
                            Company = targetcompany,
                            SubmittedBy = currUser.UserName,
                            Product = plan,
                            TotalPrice = price,
                            Date = DateTime.Now.Date,
                            Reduce = headCount
                        };

                        UpdateDailyDetail(detail);
                        ClearSession();
                        Utility.DailySub += headCount;
                        Utility.DailyTotalCost += price;
                        return Json(result);
                    }
                    catch (Exception e)
                    {
                        RevertSummaryFile(summaryFilePath, summary_backup);
                        return Json("fail");
                    }
                    finally
                    {
                        if (System.IO.File.Exists(summary_backup))
                            System.IO.File.Delete(summary_backup);
                    }
                }
                else
                {
                    if (System.IO.File.Exists(summary_backup))
                        System.IO.File.Delete(summary_backup);
                    return Json("failed");
                }
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return Json("failed");
            }
            finally
            {
                if (currUser.MyLocker.RWLocker.IsWriteLockHeld)
                    currUser.MyLocker.RWLocker.ExitWriteLock(); //退出写锁
            }
        }

        protected bool UpdateDailyDetail(DailyDetailModel model)
        {
            return DatabaseService.InsertDailyDetail(model);
        }

        protected void ClearSession()
        {
            CurrentSession.Set<List<Employee>>("validationResult", null);
            CurrentSession.Set<string>("readyToSubmit", "N");
            CurrentSession.Set<string>("company", string.Empty);
        }

        protected void RevertSummaryFile(string path, string backup)
        {
            //删除原文件，重命名备份文件
            if (System.IO.File.Exists(path) && System.IO.File.Exists(backup))
            {
                System.IO.File.Delete(path);
                System.IO.File.Move(backup, path);
            }
        }

        public JsonResult UserDaysBefore([FromQuery] string company)
        {
            int days = 0;
            var currUser = GetCurrentUser();
            if (currUser.AccessLevel == 0)
            {
                days = currUser.DaysBefore;
            }
            else
            {
                var user = DatabaseService.SelectUserByCompany(company);
                days = user.DaysBefore;
            }

            DateTime dt = DateTime.Now.Date.AddDays(-1 * days);
            ClearSession();
            return Json(dt.ToString("yyyy-MM-dd"));
        }

        /// <summary>
        /// 验证保单文件，返回验证结果
        /// </summary>
        /// <param name="newExcel"></param>
        /// <param name="mode"></param>
        /// <param name="company"></param>
        /// <param name="plan"></param>
        /// <returns></returns>
        [UserLoginFilters]
        public JsonResult UpdateEmployees([FromForm] IFormFile newExcel, string mode, string company, string plan)
        {
            CurrentSession.Set("validationResult", new List<Employee>());
            CurrentSession.Set("company", string.Empty);
            CurrentSession.Set("price", 0);
            //TODO: 添加验证格式代码
            //将FormFile中的Sheet1转换成DataTable
            string template = Path.Combine(Utility.Instance.TemplateFolder, "employee_download.xls");
            string uploadedExcel = Path.Combine(Utility.Instance.WebRootFolder, "Temp", Guid.NewGuid() + Path.GetExtension(newExcel.FileName));
            FileStream ms = System.IO.File.Create(uploadedExcel);
            CurrentSession.Set("readyToSubmit", "N");
            newExcel.CopyTo(ms);
            ExcelTool et = new ExcelTool(ms, "Sheet1");
            var dt = et.ExcelToDataTable("Sheet1", true);
            et.Dispose();
            System.IO.File.Delete(uploadedExcel);

            //将DataTable转成Excel
            Initialize(company);
            string temp_excel = Path.Combine(Utility.Instance.WebRootFolder, "Temp", Guid.NewGuid() + ".xls");
            System.IO.File.Copy(template, temp_excel);
            MemoryStream stream = new MemoryStream();
            using (FileStream fs = System.IO.File.Open(temp_excel, FileMode.Open, FileAccess.ReadWrite))
            {
                ExcelTool test = new ExcelTool(fs, "Sheet1");
                test.RawDatatableToExcel(dt);
            }

            FileStream inputStream = System.IO.File.Open(temp_excel, FileMode.Open, FileAccess.ReadWrite);
            inputStream.CopyTo(stream);
            CurrentSession.Set("newExcel", stream.ToArray());
            CurrentSession.Set("validationResult", new List<Employee>());
            if (newExcel is null)
            {
                return null;
            }
            List<Employee> validationResult = ValidateExcel(inputStream, "Sheet1", mode, company, plan);
            CurrentSession.Set("validationResult", validationResult);
            CurrentSession.Set("company", company);
            inputStream.Close();
            inputStream.Dispose();
            System.IO.File.Delete(temp_excel);
            return Json(validationResult);
        }

        [UserLoginFilters]
        [AdminFilters]
        public FileStreamResult ExportUploadedFiles(DateTime exportStart, DateTime exportEnd)
        {
            DataTable res = new DataTable();
            ReaderWriterLockSlim r_locker = null;
            string dirPath = ExcelRoot;
            string tempDir = Path.Combine(ExcelDirectory, "Temp");
            string temp_file = "SummaryTemplate.xls";
            if (exportEnd < exportStart)
            {
                return File(new FileStream(Path.Combine(dirPath, temp_file), FileMode.Open, FileAccess.Read), "text/plain", "错误.xls");
            }
            string summary_file = Path.Combine(dirPath, DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss") + ".xls");
            string summary_file_temp = Path.Combine(dirPath, DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss") + "_temp.xls");

            try
            {
                r_locker = GetCurrentUser().MyLocker.RWLocker;
                r_locker.EnterReadLock();

                if (exportStart.Year == 1 || exportEnd.Year == 1)
                {
                    throw new Exception();
                }

                System.IO.File.Copy(Path.Combine(dirPath, temp_file), summary_file_temp, true);
                ExcelTool summary = new ExcelTool(summary_file_temp, "Sheet1");
                var companies = GetSpringCompaniesName(true);
                foreach (string company in companies) //获取所有后代公司的文件夹
                {
                    if (company == "管理员") continue;
                    var companyDir = GetSearchExcelsInDir(company);
                    foreach (var plan in Plans)
                    {
                        string planDir = Path.Combine(companyDir, plan);
                        if (!System.IO.Directory.Exists(planDir)) continue;
                        foreach (string month in Directory.GetDirectories(planDir)) //遍历各方案下月份文件夹
                        {
                            DirectoryInfo di = new DirectoryInfo(month);
                            if (!DateTime.TryParse(di.Name, out DateTime dt)) continue;
                            if (dt.Month < exportStart.Month || dt.Month > exportEnd.Month) continue;
                            foreach (string excel in Directory.GetFiles(month))
                            {
                                DateTime date;
                                FileInfo fi = new FileInfo(excel);
                                string uploadDate = fi.Name.Split('@')[0];
                                if (DateTime.TryParse(uploadDate, out date))
                                {
                                    if (date.Date >= exportStart.Date && date.Date <= exportEnd.Date)
                                        summary.GainDataFromNewFile(excel, company);
                                }
                            }

                        }
                    }

                }
                summary.CreateAndSave(summary_file);
                System.IO.File.Delete(summary_file_temp);
                return File(new FileStream(summary_file, FileMode.Open, FileAccess.Read), "text/plain", "汇总表格.xls");
            }
            catch
            {
                System.IO.File.Delete(summary_file_temp);
                return File(new FileStream(Path.Combine(dirPath, temp_file), FileMode.Open, FileAccess.Read), "text/plain", "错误.xls");
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }

        [UserLoginFilters]
        public bool RemoveEmployees()
        {
            //TODO: 添加验证格式代码
            return false;
        }

        public void SaveExcelFile(IFormFile excel)
        {
            UserInfoModel currentUser = GetCurrentUser();
            string directory = currentUser.CompanyName;
            string fileName = DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss") + ".xls";
            string excelsDirectory = Path.Combine(ExcelDirectory, "Excel", directory);
            if (!Directory.Exists(excelsDirectory))
            {
                Directory.CreateDirectory(excelsDirectory);
            }

            using (FileStream fs = System.IO.File.Create(Path.Combine(excelsDirectory, fileName)))
            {
                excel.CopyTo(fs);
                fs.Flush();
            }
        }

        public List<Employee> ValidateExcel(FileStream formFile, string sheetName, string mode, string companyName, string plan)
        {
            formFile.Position = 0;
            ReaderWriterLockSlim r_locker = null;
            string companyDir = GetSearchExcelsInDir(companyName);
            var currUser = GetCurrentUser();
            var result = new List<Employee>();
            try
            {
                r_locker = currUser.MyLocker.RWLocker;
                r_locker.EnterReadLock();
                string summaryPath = Path.Combine(companyDir, plan, companyName + ".xls");
                if (!System.IO.File.Exists(summaryPath))
                {
                    result.Add(
                    new Employee()
                    {
                        Valid = false,
                        Name = $"{companyName}尚未开通{plan}账号"
                    }
                    );
                    return result;

                }
                ExcelTool summary = new ExcelTool(summaryPath, sheetName);
                DataTable sourceDT = summary.ExcelToDataTable("Sheet1", true);
                r_locker.ExitReadLock();

                string fileName = Guid.NewGuid().ToString() + ".xls";

                using (FileStream fs = System.IO.File.Create(Path.Combine(companyDir, fileName)))
                {
                    formFile.CopyTo(fs);
                    fs.Flush();
                }
                ExcelTool et = new ExcelTool(Path.Combine(companyDir, fileName), sheetName);
                bool checkAge = currUser.AccessLevel != 0;
                result = et.ValidateIDs(idCol, checkAge); // 验证身份证号码
                for (int i = 0; i < result.Count - 1; i++)
                {
                    for (int j = i + 1; j < result.Count; j++)
                    {
                        if (result[i].ID == result[j].ID)
                        {
                            result[i].Valid = false;
                            result[i].DataDesc = "所提交的表格中存在人员重复：" + result[i].ID;
                            result[j].Valid = false;
                            result[j].DataDesc = "所提交的表格中存在人员重复：" + result[j].ID;
                        }
                    }
                }
                MergeList(et.CheckJobType(jobCol), result); //验证岗位类型

                if (!System.IO.File.Exists(summaryFilePath)) //如果汇总表格不存在，则新建一个
                {
                    string source = Path.Combine(ExcelDirectory, "templates", "recipe.xls");
                    System.IO.File.Copy(source, summaryFilePath);
                }

                var temp = et.CheckDuplcateWithSummary(sourceDT, 3, idCol, mode); //验证总表中是否有重复
                MergeList(temp, result);
                CurrentSession.Set("mode", mode);
                CurrentSession.Set("plan", plan);
                CurrentSession.Set("newTable", et.ExcelToDataTable("Sheet1", true));
                System.IO.File.Delete(Path.Combine(companyDir, fileName));
                return result;
            }
            catch
            {
                result.Add(
                    new Employee()
                    {
                        Valid = false,
                        Name = "未知错误，请刷新页面并确保表格内容无误后重试"
                    }
                    );
                return result;
            }
            finally
            {
                if (r_locker != null && r_locker.IsReadLockHeld)
                {
                    r_locker.ExitReadLock();
                }
            }

        }

        protected void MergeList(List<Employee> newEmployees, List<Employee> employees)
        {
            if (newEmployees != null)
            {
                bool isSame = false;
                foreach (Employee item in newEmployees)
                {
                    isSame = false;
                    foreach (Employee item1 in employees)
                    {
                        if (item.ID == item1.ID && item.DataDesc == item1.DataDesc)
                        {
                            isSame = true;
                            item1.StartDate = item.StartDate;
                            break;
                        }
                    }
                    if (!isSame)
                    {
                        employees.Add(item);
                    }
                }
            }
        }

        public double CalculatePrice([FromForm] DateTime startdate, string company, string plan)
        {
            var currUser = GetCurrentUser();
            if (string.IsNullOrEmpty(summaryFilePath))
            {
                Initialize(company, plan);
            }
            var targetUser = DatabaseService.SelectUserByCompanyAndPlan(targetCompany, plan);
            if (targetUser == null)
            {
                if (!currUser.ChildAccounts.Any(_ => _.CompanyName == company))
                {
                    return -9999996;
                }
            }
            if (startdate.Year > DateTime.Now.Year)
            {
                return -9999997;
            }

            int offset = currUser.DaysBefore;
            if (DateTime.Now.Date.AddDays(offset * -1) > startdate.Date)
            {
                return -9999999;
            }

            CurrentSession.Set("price", 0);
            var validationResult = CurrentSession.Get<List<Employee>>("validationResult");
            var mode = CurrentSession.Get<string>("mode");

            if (validationResult is null || validationResult.Count <= 0 || validationResult.Any(a => a.Valid == false))
            {
                return -9999998; //存在invalid信息
            }
            DataTable dt = new DataTable();

            var locker = currUser.MyLocker.RWLocker;
            locker.EnterReadLock();
            try
            {
                string dateTime = startdate.ToShortDateString();
                using (FileStream fs = System.IO.File.Open(summaryFilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
                {
                    lock (fs)
                    {
                        ExcelTool et = new ExcelTool(fs, "Sheet1");
                        dt = et.ExcelToDataTable("Sheet1", true);
                    }
                }
                if (mode == "add")
                {
                    double temp = CalculateAddPrice(targetUser, startdate);
                    CurrentSession.Set("price", temp);
                    CurrentSession.Set("readyToSubmit", "Y");
                    return temp;
                }
                else if (mode == "sub")
                {
                    double result = 0;
                    var employees = CurrentSession.Get<List<Employee>>("validationResult");
                    foreach (Employee item in employees)
                    {
                        DateTime start = DateTime.Parse(item.StartDate);
                        if (startdate < start.Date)
                        {
                            return -9999995;
                        }
                        result += CalculateSubPrice(start.Date, startdate, targetUser.UnitPrice);
                    }
                    double temp = Math.Round(result, 2);
                    CurrentSession.Set("price", temp);
                    CurrentSession.Set("readyToSubmit", "Y");
                    CurrentSession.Set("plan", plan);
                    return temp;
                }
                else
                {
                    CurrentSession.Set("price", 0);
                    return 0;
                }
            }
            catch
            {
                return -9999999;
            }
            finally
            {
                if (locker.IsReadLockHeld)
                    locker.ExitReadLock();
            }

        }

        protected string GetMonthFolder(DateTime date)
        {
            return date.ToString("yyyy-MM");
        }

        [UserLoginFilters]
        private string GetCompanyFolder()
        {
            UserInfoModel currentUser = GetCurrentUser();
            string directory = currentUser.CompanyName;
            string excelsDirectory = Path.Combine(ExcelDirectory, "Excel", directory);
            return excelsDirectory;
        }
    }
}
