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
using System.Threading.Tasks;
using VirtualCredit;
using VirtualCredit.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using VirtualCredit.LogServices;
using System.Threading;
using static System.Net.WebRequestMethods;

namespace Insurance.Controllers
{
    [ApiController]
    [Route("mini/[controller]/[action]")]
    public class MiniAppEmployeeChangeController : EmployeeChangeController
    {
        public MiniAppEmployeeChangeController(IHostingEnvironment hostingEnvironment, IHttpContextAccessor httpContextAccessor) : base(hostingEnvironment, httpContextAccessor)
        {

        }


        public IActionResult MiniEmployeeChange(string openId)
        {
            MiniSession(openId);
            var currUser = GetCurrentUser();
            try
            {
                EmployeeChangeModel model = new EmployeeChangeModel();
                int advanceDays = currUser.DaysBefore;
                DateTime dt = DateTime.Now.Date.AddDays(-1d * advanceDays);
                model.AllowedStartDate = dt.ToString("yyyy-MM-dd");
                model.CompanyNameList = GetSpringCompaniesName(true);
                model.CompanyNameList.Remove("管理员");
                //if (currUser.AccessLevel != 0)
                //    model.CompanyList.Add(new Company() { Name = currUser.CompanyName });
                model.Plans = currUser.AccessLevel == 0 ? "all" : currUser._Plan;
                return Json(model);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                Response.StatusCode = 404;
                return Json("Error");
            }
        }
        [Route("[action]")]
        public JsonResult MiniUpdateEmployees(string personList, string mode, string company, string plan, string openid)
        {
            MiniSession(openid);
            CurrentSession.Set("validationResult", new List<Employee>());
            CurrentSession.Set("company", string.Empty);
            CurrentSession.Set("price", 0);
            ////TODO: 添加验证格式代码
            ////将FormFile中的Sheet1转换成DataTable
            //string template = Path.Combine(Utility.Instance.TemplateFolder, "employee_download.xls");
            //string uploadedExcel = Path.Combine(Utility.Instance.WebRootFolder, "Temp", Guid.NewGuid() + Path.GetExtension(newExcel.FileName));
            //FileStream ms = System.IO.File.Create(uploadedExcel);
            //CurrentSession.Set("readyToSubmit", "N");
            //newExcel.CopyTo(ms);
            //ExcelTool et = new ExcelTool(ms, "Sheet1");
            //var dt = et.ExcelToDataTable("Sheet1", true);
            //et.Dispose();
            //System.IO.File.Delete(uploadedExcel);

            string template = Path.Combine(Utility.Instance.TemplateFolder, "employee_download.xls");

            DataTable dt = JsonConvert.DeserializeObject<DataTable>(personList);
            if (dt == null || dt.Rows.Count <= 0)
            {
                return Json("Empty add list");
            }

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
            if (string.IsNullOrEmpty(personList))
            {
                return null;
            }
            List<Employee> validationResult = MiniValidateExcel(inputStream, "Sheet1", mode, company, plan, openid);
            CurrentSession.Set("validationResult", validationResult);
            CurrentSession.Set("company", company);
            inputStream.Close();
            inputStream.Dispose();
            System.IO.File.Delete(temp_excel);
            return Json(validationResult);
        }

        public List<Employee> MiniValidateExcel(FileStream formFile, string sheetName, string mode, string companyName, string plan, string openId)
        {
            MiniSession(openId);
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
                //MergeList(et.CheckJobType(jobCol), result); //验证岗位类型

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

        public JsonResult MiniSearchPeople([FromQuery] string companyName, [FromQuery] string content, [FromQuery] string openid)
        {
            MiniSession(openid);
            var currUser = GetCurrentUser();
            try
            {
                string targetDir = GetSearchExcelsInDir(companyName);

                List<string> summaryFiles = new List<string>();
                foreach (var plan in Plans)
                {
                    if (System.IO.File.Exists(Path.Combine(targetDir, plan, companyName + ".xls")))
                        summaryFiles.Add(Path.Combine(targetDir, plan, companyName + ".xls"));
                }
                //string summaryFiles = Path.Combine(targetDir, companyName, ".xls");

                SearchPeopleModel model = new SearchPeopleModel();
                model.People = new Employee();
                DataTable temp = new DataTable();
                DataTable result = new DataTable();
                result.Columns.Add(new DataColumn("name"));
                result.Columns.Add(new DataColumn("id"));
                result.Columns.Add(new DataColumn("type"));
                result.Columns.Add(new DataColumn("job"));
                result.Columns.Add(new DataColumn("isChecked"));
                currUser.MyLocker.RWLocker.EnterReadLock();
                foreach (string file in summaryFiles)
                {
                    //如果为空则返回该公司全部在职员工
                    if (string.IsNullOrEmpty(content))
                    {
                        using (ExcelTool et = new ExcelTool(file, "Sheet1"))
                        {
                            temp = et.ExcelToDataTable("sheet1", true);
                        }
                    }
                    else
                    {
                        if (content.Length >= 10 && long.TryParse(content, out long id)) //身份证
                        {
                            using (ExcelTool et = new ExcelTool(file, "Sheet1"))
                            {
                                temp = et.MiniSelectPeopleByNameAndID("", 3, content, 4);
                            }
                        }
                        else //人名
                        {
                            using (ExcelTool et = new ExcelTool(file, "Sheet1"))
                            {
                                temp = et.MiniSelectPeopleByNameAndID(content, 3, "", 4);
                            }
                        }
                    }
                    foreach (DataRow row in temp.Rows)
                    {
                        DataRow nr = result.NewRow();
                        nr[0] = row["姓名"];
                        nr[1] = row["身份证"];
                        nr[2] = row["职业类别"];
                        nr[3] = row["工种"];
                        nr[4] = false;
                        result.Rows.Add(nr);
                    }
                }
                DataView dataView = result.DefaultView;
                dataView.Sort = "id";
                
                return new JsonResult(dataView.ToTable());
                //DataTable res = new DataTable();
                //string company = currUser.CompanyName;
                //string debug = "";

                //currUser.MyLocker.RWLocker.EnterReadLock();
                //foreach (var file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                //{
                //    debug = file;
                //    FileInfo fi = new FileInfo(file);
                //    if (!DateTime.TryParse(fi.Directory.Name, out DateTime date)) continue;
                //    string[] excelInfo = fi.Name.Split("@");
                //    string mode = excelInfo[3].Equals("Add", StringComparison.CurrentCultureIgnoreCase) ? "加保" : "减保";
                //    string uploadtime = excelInfo[0] + " " + excelInfo[5].Replace('-', ':');
                //    string comp = fi.Directory.Parent.Parent.Name;
                //    string uploader = excelInfo[2];
                //    using (ExcelTool et = new ExcelTool(file, "Sheet1"))
                //    {
                //        //temp = et.SelectPeopleByNameAndID(em_name, nameCol, em_id, idCol);
                //    }
                //    if (temp != null && temp.Rows.Count > 0)
                //    {
                //        if (res.Columns.Count <= 0)
                //        {
                //            res = temp.Clone(); //拷贝表结构
                //            DataColumn dc = new DataColumn("History");
                //            res.Columns.Add(dc);
                //        }
                //        if (temp.Rows[0][1].ToString() == "未找到符合条件的人员")
                //        {
                //            continue;
                //        }
                //        foreach (DataRow row in temp.Rows)
                //        {
                //            string history = string.Join('%', uploadtime, mode, row["start_date"], row["end_date"], comp, uploader);
                //            DataRow[] t = res.Select($"id = '{row["id"]}'");
                //            if (t != null && t.Length > 0)
                //            {
                //                t[0]["History"] = string.Join("+", t[0]["History"], history);
                //            }
                //            else
                //            {
                //                DataRow newRow = res.NewRow();
                //                newRow.ItemArray = row.ItemArray;
                //                newRow["History"] = history;
                //                res.Rows.Add(newRow);
                //            }

                //        }
                //    }

                //}

                //model.Result = res;
                //// CacheSearchResult(res);
                //return new JsonResult("");
            }
            catch (Exception e)
            {
                return new JsonResult("");
            }
            finally
            {
                if (currUser.MyLocker.RWLocker.IsReadLockHeld)
                    currUser.MyLocker.RWLocker.ExitReadLock();
            }
        }

        public IActionResult MiniUpdateSummary(string date, string plan, string openId)
        {
            MiniSession(openId);
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
                if (!DateTime.TryParse(date, out DateTime startdate))
                {
                    return BadRequest(Json("日期格式错误"));
                }
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
                            summary.SetCellText(i, 3, em.ID); //ID
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
                        result.Add(headCount.ToString());
                        result.Add(price.ToString());
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
                            var row = from DataRow p in tbl_summary.Rows where p[3].ToString() == guest.ID select tbl_summary.Rows.IndexOf(p);
                            foreach (int item in row)
                            {
                                tbl_summary.Rows.RemoveAt(item);
                                break;
                            }
                        }
                        summary.DatatableToExcel(tbl_summary);
                        List<string> result = new List<string>();
                        result.Add("退保成功");
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
                        result.Add(headCount.ToString());
                        result.Add(price.ToString());
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

        [HttpPost]
        public JsonResult MiniCalculatePrice([FromForm] string personList, [FromForm] string mode1, [FromForm] string date, [FromForm] string company, [FromForm] string plan, [FromForm] string openId)
        {
            MiniSession(openId);
            MiniUpdateEmployees(personList, mode1, company, plan, openId);
            if (!DateTime.TryParse(date, out DateTime startdate))
            {
                return Json("日期格式错误");
            }
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
                    return Json(-9999996);
                }
            }
            if (startdate.Year > DateTime.Now.Year)
            {
                return Json("生效日期错误");
            }

            int offset = currUser.DaysBefore;
            if (DateTime.Now.Date.AddDays(offset * -1) > startdate.Date)
            {
                return Json("生效日期不在追溯期内");
            }

            CurrentSession.Set("price", 0);
            var validationResult = CurrentSession.Get<List<Employee>>("validationResult");
            var mode = CurrentSession.Get<string>("mode");

            if (validationResult is null || validationResult.Count <= 0 || validationResult.Any(a => a.Valid == false))
            {
                List<string> msg = new List<string>();
                if (validationResult == null)
                {
                    msg.Add("无法验证提交的名单，请确认后重试");
                }
                else
                {
                    foreach (var item in validationResult)
                    {
                        if (item.DataDesc != "")
                            msg.Add($"{item.Name} {item.DataDesc}");
                    }
                }
                return Json(msg); //存在invalid信息
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
                    return Json($"数据无误{temp}");
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
                            return Json(-9999995);
                        }
                        result += CalculateSubPrice(start.Date, startdate, targetUser.UnitPrice);
                    }
                    double temp = Math.Round(result, 2);
                    CurrentSession.Set("price", temp);
                    CurrentSession.Set("readyToSubmit", "Y");
                    CurrentSession.Set("plan", plan);
                    return Json($"数据无误{temp}");
                }
                else
                {
                    CurrentSession.Set("price", 0);
                    return Json(0);
                }
            }
            catch
            {
                return Json(-9999999);
            }
            finally
            {
                if (locker.IsReadLockHeld)
                    locker.ExitReadLock();
            }

        }
    }
}
