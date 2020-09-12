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
        private readonly int idCol = 1;
        private readonly int jobCol = 2;
        public static string ExcelDirectory;
        private readonly IHostingEnvironment _hostingEnvironment;
        string companyFolder;
        string summaryFileName;
        string summaryFilePath;
        private static readonly object summaryLocker = new object();

        public EmployeeChangeController(IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
            ExcelDirectory = _hostingEnvironment.WebRootPath;
        }

        private void Initialize()
        {
            companyFolder = GetCompanyFolder();
            summaryFileName = GetCurrentUser().CompanyName + ".xls";
            summaryFilePath = Path.Combine(companyFolder, summaryFileName);
        }

        [UserLoginFilters]
        public JsonResult UpdateSummary(DateTime startdate)
        {
            try
            {
                //判断是否已计算保费
                if (string.IsNullOrEmpty(HttpContext.Session.Get<string>("readyToSubmit")) || HttpContext.Session.Get<string>("readyToSubmit") != "Y")
                {
                    return Json("NotCalculated");
                }
                string summary_backup = string.Empty;
                //验证前后端价格计算是否一致
                double price = HttpContext.Session.Get<double>("price");
                double calculatedprice = CalculatePrice(startdate);
                if (price != calculatedprice)
                {
                    return Json("NotCalculated");
                }

                var employees = HttpContext.Session.Get<List<Employee>>("validationResult");
                if (employees == null || employees.Count <= 0 || employees.Any<Employee>(_ => _.Valid == false))
                {
                    return Json(new List<string> { "信息错误", "", "" });
                }
                //===================================================//
                if (string.IsNullOrEmpty(summaryFilePath))
                {
                    Initialize();
                }
                string mode = HttpContext.Session.Get<string>("mode");
                string excelsDirectory = GetCompanyFolder();
                string monthDir = GetMonthFolder(startdate);
                if (!Directory.Exists(Path.Combine(excelsDirectory, monthDir)))
                {
                    Directory.CreateDirectory(Path.Combine(excelsDirectory, monthDir));
                }
                //=========================================================================================================//
                GetCurrentUser().MyLocker.RWLocker.EnterWriteLock(); //进入写锁

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
                if (mode == "add")
                {
                    string fileName = DateTime.Now.ToString("yyyy-MM-dd") + $"@{price}@{GetCurrentUser().UserName}@Add@{Guid.NewGuid()}" + DateTime.Now.ToString("@HH-mm-ss") + "@0@.xls"; //命名规则： 上传日期_保费_上传账号_加/减保_GUID_时间_已结保费.xls
                    string newfilepath = Path.Combine(excelsDirectory, monthDir, fileName);
                    try
                    {
                        //1 - 新员工excel中，添加生效日期,然后保存文件
                        //创建新excel文档
                        using (FileStream fs = System.IO.File.Create(newfilepath))
                        {
                            var excel = HttpContext.Session.Get("newExcel");
                            MemoryStream ms = new MemoryStream(excel);
                            ms.CopyTo(fs);
                            fs.Flush();
                        }
                        ExcelTool et = new ExcelTool(newfilepath, "Sheet1");
                        et.SetCellText(0, 4, "保障开始时间");
                        et.SetCellText(0, 5, "保障结束时间");
                        for (int i = 1; i <= et.m_main.GetLastRow(); i++)
                        {
                            et.SetCellText(i, 4, startdate.Date.ToString("yyyy/MM/dd"));
                        }
                        et.Save();

                        //2 - 总表中添加新员工信息
                        int startrow = summary.m_main.GetLastRow() + 1;
                        for (int i = startrow; i < startrow + employees.Count; i++)
                        {
                            Employee em = employees[i - startrow];
                            string company = GetCurrentUser().CompanyName;
                            summary.SetCellText(i, 0, i.ToString()); //序号
                            summary.SetCellText(i, 1, company); //单位
                            summary.SetCellText(i, 2, em.Name); //姓名
                            summary.SetCellText(i, 3, em.ID); //ID
                            summary.SetCellText(i, 4, em.JobType); //险种
                            summary.SetCellText(i, 5, em.Job); //岗位
                            summary.SetCellText(i, 6, startdate.Date.ToShortDateString()); //生效日期
                        }
                        summary.Save();
                        System.IO.File.Delete(summary_backup);
                        List<string> result = new List<string>();
                        result.Add("投保成功");
                        string kickoffDate = startdate.Date.ToString("yyyy/MM/dd 00:00:01");
                        string endDate = (new DateTime(startdate.Year, startdate.Month, DateTime.DaysInMonth(startdate.Year, startdate.Month))).ToString("yyyy/MM/dd 23:59:59");
                        result.Add(kickoffDate);
                        result.Add(endDate);
                        HttpContext.Session.Set<List<Employee>>("validationResult", null);
                        HttpContext.Session.Set<string>("readyToSubmit", "N");
                        return Json(result);

                    }
                    catch (Exception e)
                    {
                        RevertSummaryFile(summaryFilePath, summary_backup);
                        if (System.IO.File.Exists(summary_backup))
                            System.IO.File.Delete(summary_backup);
                        return Json("failed");
                    }
                    finally
                    {

                    }
                }
                else if (mode == "sub")
                {
                    string fileName = DateTime.Now.ToString("yyyy-MM-dd") + $"@{price}@{GetCurrentUser().UserName}@Sub@{Guid.NewGuid()}" + DateTime.Now.ToString("@HH-mm-ss") + "@0@.xls"; //命名规则： 上传日期_保费_上传账号_加/减保_GUID_时间_已结保费_.xls
                    string newfilepath = Path.Combine(excelsDirectory, monthDir, fileName);
                    try
                    {
                        //1 - 新表中添加离职信息，并保存文件
                        using (FileStream fs = System.IO.File.Create(newfilepath))
                        {
                            var excel = HttpContext.Session.Get("newExcel");
                            MemoryStream ms = new MemoryStream(excel);
                            ms.CopyTo(fs);
                            fs.Flush();
                        }
                        ExcelTool et = new ExcelTool(newfilepath, "Sheet1");
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
                            var row = from DataRow p in tbl_summary.Rows where p[3].ToString() == guest.ID select tbl_summary.Rows.IndexOf(p);
                            foreach (int item in row)
                            {
                                tbl_summary.Rows.RemoveAt(item);
                                break;
                            }
                        }
                        summary.DatatableToExcel(tbl_summary);
                        System.IO.File.Delete(summary_backup);
                        List<string> result = new List<string>();
                        result.Add("投保成功");
                        string kickoffDate = employee.StartDate + " 00:00:01";
                        string endDate = startdate.ToString("yyyy/MM/dd 23:59:59");
                        result.Add(kickoffDate);
                        result.Add(endDate);
                        HttpContext.Session.Set<List<Employee>>("validationResult", null);
                        HttpContext.Session.Set<string>("readyToSubmit", "N");
                        return Json(result);
                    }
                    catch (Exception e)
                    {
                        RevertSummaryFile(summaryFilePath, summary_backup);
                        if (System.IO.File.Exists(summary_backup))
                            System.IO.File.Delete(summary_backup);
                        return Json("fail");
                    }
                    finally
                    {

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
                if (GetCurrentUser().MyLocker.RWLocker.IsWriteLockHeld)
                    GetCurrentUser().MyLocker.RWLocker.ExitWriteLock(); //退出写锁
            }
        }

        private void RevertSummaryFile(string path, string backup)
        {
            //删除原文件，重命名备份文件
            if (System.IO.File.Exists(path) && System.IO.File.Exists(backup))
            {
                System.IO.File.Delete(path);
                System.IO.File.Move(backup, path);
            }
        }

        [UserLoginFilters]
        public IActionResult UpdateEmployees([FromForm] IFormFile newExcel, string mode)
        {
            if (newExcel is null)
            {
                return BadRequest();
            }
            FileInfo fi = new FileInfo(newExcel.FileName);
            string temp_excel = Path.Combine(Utility.Instance.WebRootFolder, "Temp", Guid.NewGuid() + ".xls");
            try
            {
                //TODO: 添加验证格式代码
                //将FormFile中的Sheet1转换成DataTable
                string template = Path.Combine(Utility.Instance.TemplateFolder, "employee_download.xls");
                string uploadedExcel = Path.Combine(Utility.Instance.WebRootFolder, "Temp", Guid.NewGuid() + fi.Extension);
                DataTable dt = new DataTable();
                using (FileStream ms = System.IO.File.Create(uploadedExcel))
                {
                    HttpContext.Session.Set("readyToSubmit", "N");
                    newExcel.CopyTo(ms);
                    ms.Flush();
                }
                using (ExcelTool et = new ExcelTool(uploadedExcel, "Sheet1"))
                {
                    dt = et.ExcelToDataTable("Sheet1", true);
                }
                System.IO.File.Delete(uploadedExcel);


                //将DataTable转成Excel
                Initialize();
                System.IO.File.Copy(template, temp_excel);
                MemoryStream stream = new MemoryStream();
                using (ExcelTool test = new ExcelTool(temp_excel, "Sheet1"))
                {
                    test.RawDatatableToExcel(dt);
                }

                using (FileStream inputStream = System.IO.File.Open(temp_excel, FileMode.Open, FileAccess.ReadWrite))
                {
                    inputStream.CopyTo(stream);
                    HttpContext.Session.Set("newExcel", stream.ToArray());
                    HttpContext.Session.Set("validationResult", new List<Employee>());
                    List<Employee> validationResult = ValidateExcel(inputStream, "Sheet1", mode);
                    HttpContext.Session.Set("validationResult", validationResult);
                    return Json(validationResult);
                }
            }
            catch (Exception e)
            {
                Response.StatusCode = 500;
                Response.WriteAsync("模板数据结构异常，请重新下载本站提供的模板文件填入数据后再试");
                LogService.Log(e.Message);
                LogService.Log(e.StackTrace);
                return StatusCode(500);
            }
            finally
            {
                System.IO.File.Delete(temp_excel);
            }

        }

        [UserLoginFilters]
        [AdminFilters]
        public FileStreamResult ExportUploadedFiles(DateTime exportStart, DateTime exportEnd)
        {
            DataTable res = new DataTable();
            ReaderWriterLockSlim r_locker = null;
            string dirPath = Path.Combine(ExcelDirectory, "Excel");
            string tempDir = Path.Combine(ExcelDirectory, "Temp");
            string temp_file = "SummaryTemplate.xls";
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
                foreach (string dir in Directory.GetDirectories(dirPath))
                {
                    foreach (string month in Directory.GetDirectories(dir))
                    {
                        foreach (string excel in Directory.GetFiles(month))
                        {
                            DateTime date;
                            FileInfo fi = new FileInfo(excel);
                            string uploadDate = fi.Name.Split('@')[0];
                            if (DateTime.TryParse(uploadDate, out date))
                            {
                                if (date.Date >= exportStart.Date && date.Date <= exportEnd.Date)
                                    summary.GainDataFromNewFile(excel, fi.Directory.Parent.Name);
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

        public List<Employee> ValidateExcel(FileStream formFile, string sheetName, string mode)
        {
            formFile.Position = 0;
            ReaderWriterLockSlim r_locker = null;
            string excelsDirectory = GetCompanyFolder();
            var result = new List<Employee>();
            try
            {
                r_locker = GetCurrentUser().MyLocker.RWLocker;
                r_locker.EnterReadLock();
                ExcelTool summary = new ExcelTool(Path.Combine(excelsDirectory, GetCurrentUser().CompanyName + ".xls"), sheetName);
                DataTable sourceDT = summary.ExcelToDataTable("Sheet1", true);
                r_locker.ExitReadLock();

                string fileName = Guid.NewGuid().ToString() + ".xls";

                using (FileStream fs = System.IO.File.Create(Path.Combine(excelsDirectory, fileName)))
                {
                    formFile.CopyTo(fs);
                    fs.Flush();
                }
                ExcelTool et = new ExcelTool(Path.Combine(excelsDirectory, fileName), sheetName);

                result = et.ValidateIDs(idCol); // 验证身份证号码
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

                if (!System.IO.File.Exists(summaryFilePath))
                {
                    string source = Path.Combine(ExcelDirectory, "templates", "recipe.xls");
                    System.IO.File.Copy(source, summaryFilePath);
                }

                var temp = et.CheckDuplcateWithSummary(sourceDT, 3, idCol, mode); //验证总表中是否有重复
                MergeList(temp, result);
                HttpContext.Session.Set("mode", mode);
                HttpContext.Session.Set("newTable", et.ExcelToDataTable("Sheet1", true));
                System.IO.File.Delete(Path.Combine(excelsDirectory, fileName));
                return result;
            }
            catch
            {
                result.Add(
                    new Employee()
                    {
                        Valid = false
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

        private void MergeList(List<Employee> newEmployees, List<Employee> employees)
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
        static object locker1 = new object();
        public double CalculatePrice([FromForm] DateTime startdate)
        {

            if (startdate.Year > DateTime.Now.Year)
            {
                return -9999997;
            }
            if (GetCurrentUser() != null && GetCurrentUser().AccessLevel != 0)
            {
                int offset = GetCurrentUser().DaysBefore;
                if (DateTime.Now.Date.AddDays(offset * -1) > startdate.Date)
                {
                    return -9999999;
                }
            }
            HttpContext.Session.Set("price", 0);
            var validationResult = HttpContext.Session.Get<List<Employee>>("validationResult");
            var mode = HttpContext.Session.Get<string>("mode");

            if (validationResult is null || validationResult.Count <= 0 || validationResult.Any(a => a.Valid == false))
            {
                return -9999998; //存在invalid信息
            }
            DataTable dt = new DataTable();
            if (string.IsNullOrEmpty(summaryFilePath))
            {
                Initialize();
            }
            var locker = GetCurrentUser().MyLocker.RWLocker;
            locker.EnterReadLock();
            try
            {
                string dateTime = startdate.ToShortDateString();

                using (ExcelTool et = new ExcelTool(summaryFilePath, "Sheet1"))
                {
                    dt = et.ExcelToDataTable("Sheet1", true);
                }

                if (mode == "add")
                {
                    double temp = CalculateAddPrice(startdate);
                    HttpContext.Session.Set("price", temp);
                    HttpContext.Session.Set("readyToSubmit", "Y");
                    return temp;
                }
                else if (mode == "sub")
                {
                    double result = 0;
                    var employees = HttpContext.Session.Get<List<Employee>>("validationResult");
                    foreach (Employee item in employees)
                    {
                        DateTime start = DateTime.Parse(item.StartDate);
                        if (startdate < start.Date)
                        {
                            return -9999995;
                        }
                        result += CalculateSubPrice(start.Date, startdate);
                    }
                    double temp = Math.Round(result, 2);
                    HttpContext.Session.Set("price", temp);
                    HttpContext.Session.Set("readyToSubmit", "Y");
                    return temp;
                }
                else
                {
                    HttpContext.Session.Set("price", 0);
                    return 0;
                }
            }
            catch
            {
                return -9999999;
            }
            finally
            {
                locker.ExitReadLock();
            }

        }

        private string GetMonthFolder(DateTime date)
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
