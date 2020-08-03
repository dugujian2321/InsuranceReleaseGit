using Insurance.Models;
using Microsoft.AspNetCore.Mvc;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using VirtualCredit;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace Insurance.Services
{
    public class ExcelTool : IDisposable
    {
        private string fileName = null; //文件名
        private IWorkbook workbook = null;
        public ISheet m_main;
        UserInfoModel User;
        ICellStyle style0;
        public ExcelTool(string fileName, string sheetName)
        {
            this.fileName = fileName;
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                if (fileName.IndexOf(".xlsx") > 0) // 2007版本
                    workbook = new XSSFWorkbook(fs);
                else if (fileName.IndexOf(".xls") > 0) // 2003版本
                    workbook = new HSSFWorkbook(fs);
                else
                    workbook = new XSSFWorkbook();
            }
            m_main = workbook.GetSheet(sheetName);
            InitializeStyle();
        }


        public ExcelTool(Stream stream, string sheetName)
        {
            SetCulture();
            fileName = (stream as FileStream).Name;
            if (fileName.IndexOf(".xlsx") > 0) // 2007版本
                workbook = new XSSFWorkbook(stream);
            else if (fileName.IndexOf(".xls") > 0) // 2003版本
                workbook = new HSSFWorkbook(stream);
            else
                workbook = new XSSFWorkbook();

            m_main = workbook.GetSheet(sheetName);
            InitializeStyle();
        }

        public ExcelTool()
        {

        }

        private void InitializeStyle()
        {
            IDataFormat dataformat = workbook.CreateDataFormat();
            style0 = workbook.CreateCellStyle();
            style0.DataFormat = dataformat.GetFormat("yyyy/MM/dd");
        }

        private void SetCulture()
        {
            CultureInfo newculture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            newculture.DateTimeFormat.TimeSeparator = ":";
            Thread.CurrentThread.CurrentCulture = newculture;
        }

        public enum DataType
        {
            DateTime,
            String,
            Number
        }

        public void Renew()
        {

        }

        public void SetCellText(int row, int column, string str)
        {
            if (m_main.GetRow(row) is null)
                m_main.CreateRow(row).CreateCell(column).SetCellValue(str);
            else
            {
                m_main.GetRow(row).CreateCell(column).SetCellValue(str);
            }
        }

        public string GetCellText(int row, int column, DataType dataType = DataType.Number)
        {
            if (m_main.GetLastRow() < row)
            {
                return string.Empty;
            }
            ICell cell = m_main.GetRow(row).GetCell(column);
            if (cell is null)
            {
                return string.Empty;
            }
            CellType cellType = m_main.GetRow(row).GetCell(column).CellType;
            switch (cellType)
            {
                case CellType.String:
                    DateTime dt;
                    if (DateTime.TryParse(m_main.GetRow(row).GetCell(column).StringCellValue, out dt))
                    {
                        return dt.Date.ToShortDateString();
                    }
                    else
                    {
                        return m_main.GetRow(row).GetCell(column).StringCellValue;
                    }
                case CellType.Numeric:
                    if (HSSFDateUtil.IsCellDateFormatted(cell))
                    {
                        return cell.DateCellValue.Date.ToShortDateString();
                    }
                    else
                    {
                        return cell.NumericCellValue.ToString();
                    }

                default:
                    return string.Empty;
            }
        }

        public void SaveXlsx()
        {
            using (FileStream fsWrite = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite))
            {
                workbook.Write(fsWrite);
            }
        }

        public void Save()
        {
            using (FileStream fsWrite = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite))
            {
                workbook.Write(fsWrite);
            }
        }
        public void RemoveDuplicate()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            int idCol = GetColumnIndexByColumnName("身份证");
            int resignDateCol = GetColumnIndexByColumnName("保障结束时间");
            int signDateCol = GetColumnIndexByColumnName("保障开始时间");
            for (int i = 1; i <= m_main.GetLastRow(); i++)
            {
                if (!string.IsNullOrEmpty(GetCellText(i, resignDateCol)))
                {
                    string id = GetCellText(i, idCol);
                    for (int j = 1; j <= m_main.GetLastRow(); j++)
                    {
                        if (GetCellText(j, idCol) == id && GetCellText(j, signDateCol) == GetCellText(i, signDateCol) && string.IsNullOrWhiteSpace(GetCellText(j, resignDateCol)))
                        {
                            RemoveByRowNum(j);
                            i--;
                            break;
                        }
                    }
                }
            }
        }
        //public void RemoveDuplicate()
        //{
        //    Dictionary<string, string> dic = new Dictionary<string, string>();
        //    int idCol = GetColumnIndexByColumnName("身份证");
        //    int resignDateCol = GetColumnIndexByColumnName("保障结束时间");
        //    int signDateCol = GetColumnIndexByColumnName("保障开始时间");
        //    for (int i = 1; i <= m_main.GetLastRow(); i++)
        //    {
        //        if (!string.IsNullOrEmpty(GetCellText(i, resignDateCol)))
        //        {
        //            IRow row_i = m_main.GetRow(i);
        //            dic.Add(GetCellText(i, idCol), string.Empty);
        //            continue;
        //        }
        //    }

        //    for (int i = 1; i <= m_main.GetLastRow(); i++)
        //    {
        //        string id = GetCellText(i, idCol);
        //        if (dic.ContainsKey(id) && string.IsNullOrWhiteSpace(GetCellText(i, resignDateCol)))
        //        {
        //            RemoveByRowNum(i);
        //            i = i - 1;
        //        }
        //    }
        //}

        private int GetColumnIndexByColumnName(string colName)
        {
            int result = -1;
            IRow row = m_main.GetRow(1);
            for (int i = 0; i < 100; i++)
            {
                if (GetCellText(0, i).Equals(colName, StringComparison.CurrentCultureIgnoreCase))
                {
                    result = i;
                }
            }
            return result;
        }

        public void CreateAndSave(string path)
        {
            MemoryStream ms = new MemoryStream();
            workbook.Write(ms);
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                fs.Write(ms.ToArray());
                fs.Flush();
            }
        }

        public int GetEmployeeNumber()
        {
            return m_main.GetLastRow();
        }

        public string GetCurrentDate()
        {
            return GetCellText(1, 6);
        }

        public double GetCostFromJuneToMay(string companyDir, int year, string plan)
        {
            DateTime now = DateTime.Now;
            DateTime from = new DateTime();
            DateTime to = new DateTime();

            from = new DateTime(year, 6, 1);
            to = new DateTime(year + 1, 5, 31, 23, 59, 59);


            double cost = 0;
            if (Directory.Exists(companyDir))
            {
                foreach (string monthDir in Directory.GetDirectories(companyDir))
                {
                    if (!DateTime.TryParse(new DirectoryInfo(monthDir).Name, out DateTime dateTime))
                    {
                        continue;
                    }
                    DateTime dirDate = Convert.ToDateTime(new DirectoryInfo(monthDir).Name);
                    dirDate = new DateTime(dirDate.Year, dirDate.Month, 1);
                    if (dirDate >= from && dirDate <= to)
                    {
                        foreach (string file in Directory.GetFiles(monthDir))
                        {
                            string[] excelinfo = file.Split('@');
                            cost += Convert.ToDouble(excelinfo[1]);
                        }
                    }
                }
            }
            return Math.Round(cost, 2);
        }
        public double GetCostFromJuneToMay(string companyDir, int year)
        {
            DateTime now = DateTime.Now;
            DateTime from = new DateTime();
            DateTime to = new DateTime();

            from = new DateTime(year, 6, 1);
            to = new DateTime(year + 1, 5, 31, 23, 59, 59);


            double cost = 0;
            if (Directory.Exists(companyDir))
            {
                foreach (string monthDir in Directory.GetDirectories(companyDir))
                {
                    if (!DateTime.TryParse(new DirectoryInfo(monthDir).Name, out DateTime dateTime))
                    {
                        continue;
                    }
                    DateTime dirDate = Convert.ToDateTime(new DirectoryInfo(monthDir).Name);
                    dirDate = new DateTime(dirDate.Year, dirDate.Month, 1);
                    if (dirDate >= from && dirDate <= to)
                    {
                        foreach (string file in Directory.GetFiles(monthDir))
                        {
                            string[] excelinfo = file.Split('@');
                            cost += Convert.ToDouble(excelinfo[1]);
                        }
                    }
                }
            }
            return Math.Round(cost, 2);
        }

        /// <summary>
        /// 计算公司所有月份所有上传保单的费用
        /// </summary>
        /// <param name="companyDir"></param>
        /// <returns></returns>
        public double GetTotalCost(string companyDir)
        {
            double cost = 0;
            if (Directory.Exists(companyDir))
            {
                foreach (string monthDir in Directory.GetDirectories(companyDir))
                {
                    if (!DateTime.TryParse(new DirectoryInfo(monthDir).Name, out DateTime dateTime))
                    {
                        continue;
                    }
                    foreach (string file in Directory.GetFiles(monthDir))
                    {
                        string[] excelinfo = file.Split('@');
                        cost += Convert.ToDouble(excelinfo[1]);
                    }
                }
            }

            return Math.Round(cost, 2);
        }

        public double GetCustomerAlreadyPaid(string companyDir)
        {
            double cost = 0;
            if (Directory.Exists(companyDir))
            {
                foreach (string monthDir in Directory.GetDirectories(companyDir))
                {
                    if (!DateTime.TryParse(new DirectoryInfo(monthDir).Name, out DateTime dateTime))
                    {
                        continue;
                    }
                    foreach (string file in Directory.GetFiles(monthDir))
                    {
                        string[] excelinfo = file.Split('@');
                        cost += Convert.ToDouble(excelinfo[6]);
                    }
                }
            }

            return Math.Round(cost, 2);
        }

        public double GetCustomerAlreadyPaidFromJuneToMay(string companyDir, int year)
        {
            double cost = 0; DateTime now = DateTime.Now;
            DateTime from = new DateTime();
            DateTime to = new DateTime();

            from = new DateTime(year, 6, 1);
            to = new DateTime(year + 1, 5, 31, 23, 59, 59);

            if (Directory.Exists(companyDir))
            {
                foreach (string monthDir in Directory.GetDirectories(companyDir))
                {
                    if (!DateTime.TryParse(new DirectoryInfo(monthDir).Name, out DateTime dateTime))
                    {
                        continue;
                    }
                    if (dateTime < from || dateTime > to) continue;
                    foreach (string file in Directory.GetFiles(monthDir))
                    {
                        string[] excelinfo = file.Split('@');
                        cost += Convert.ToDouble(excelinfo[6]);
                    }
                }
            }

            return Math.Round(cost, 2);
        }
        public double GetPaidCost()
        {
            string path = new FileInfo(fileName).DirectoryName;
            var txt = Directory.GetFiles(path).Where(_ => new FileInfo(_).Name.Contains("txt"));
            foreach (string item in txt)
            {
                string[] info = item.Split("_");
                return Convert.ToDouble(info[1].Replace(".txt", ""));
            }
            return 0;
        }

        public bool GainData(string sourceFile)
        {
            bool result = true;
            try
            {
                ExcelTool source = new ExcelTool(sourceFile, "Sheet1");
                DataTable sourceTbl;
                source.ExcelToDataTable("Sheet1", true, out sourceTbl);
                ISheet destSheet = this.workbook.GetSheet("Sheet1");
                int nextRow = destSheet.GetLastRow() + 1;
                foreach (DataRow row in sourceTbl.Rows)
                {
                    IRow newrow = destSheet.CreateRow(nextRow);
                    nextRow++;

                    for (int i = 0; i < sourceTbl.Columns.Count; i++)
                    {
                        DataColumn column = sourceTbl.Columns[i];
                        ICell cell = newrow.CreateCell(i);
                        DateTime dt;
                        if (DateTime.TryParse(row[column].ToString(), out dt))
                        {
                            cell.SetCellValue(dt);
                        }
                        else
                            cell.SetCellValue(row[column].ToString());
                    }
                }
            }
            catch
            {
                result = false;
            }
            return result;
        }

        public bool GainDataFromNewFile(string sourceFile, string companyName)
        {
            bool result = true;
            try
            {
                ExcelTool source = new ExcelTool(sourceFile, "Sheet1");
                DataTable sourceTbl;
                source.ExcelToDataTable("Sheet1", true, out sourceTbl);
                ISheet destSheet = this.workbook.GetSheet("Sheet1");
                int nextRow = destSheet.GetLastRow() + 1;
                foreach (DataRow row in sourceTbl.Rows)
                {
                    IRow newrow = destSheet.CreateRow(nextRow);
                    nextRow++;
                    ICell cell = newrow.CreateCell(0);
                    cell.SetCellValue(this.m_main.GetLastRow() + 1);
                    cell = newrow.CreateCell(1);
                    cell.SetCellValue(companyName);

                    for (int i = 0; i < sourceTbl.Columns.Count; i++)
                    {
                        DataColumn column = sourceTbl.Columns[i];
                        cell = newrow.CreateCell(i + 2);
                        DateTime dt;
                        if (DateTime.TryParse(row[column].ToString(), out dt))
                        {
                            cell.SetCellValue(dt);
                        }
                        else
                            cell.SetCellValue(row[column].ToString());
                    }
                }
            }
            catch
            {
                result = false;
            }
            return result;
        }

        /// <summary>
        /// 将excel中的数据导入到DataTable中
        /// </summary>
        /// <param name="sheetName">excel工作薄sheet的名称</param>
        /// <param name="isFirstRowColumn">第一行是否是DataTable的列名</param>
        /// <returns>返回的DataTable</returns>
        public DataTable ExcelToDataTable(string sheetName, bool isFirstRowColumn)
        {
            ISheet sheet = m_main;
            DataTable data = new DataTable();
            int startRow = 0;
            try
            {
                if (sheet is null)
                {
                    return null;
                }
                IRow firstRow = sheet.GetRow(0);
                int cellCount = firstRow.LastCellNum; //一行最后一个cell的编号 即总的列数

                if (isFirstRowColumn)
                {
                    for (int i = firstRow.FirstCellNum; i < cellCount; ++i)
                    {
                        ICell cell = firstRow.GetCell(i);
                        if (cell != null)
                        {
                            string cellValue = cell.StringCellValue;
                            if (cellValue != null)
                            {
                                DataColumn column = new DataColumn(cellValue);
                                data.Columns.Add(column);
                            }
                        }
                    }
                    startRow = sheet.FirstRowNum + 1;
                }
                else
                {
                    for (int i = firstRow.FirstCellNum; i < cellCount; ++i)
                    {
                        ICell cell = firstRow.GetCell(i);
                        if (cell != null)
                        {
                            string cellValue = cell.StringCellValue;
                            if (cellValue != null)
                            {
                                DataColumn column = new DataColumn(cellValue);
                                data.Columns.Add(column);
                            }
                        }
                    }
                    startRow = sheet.FirstRowNum;
                }

                //最后一列的标号
                int rowCount = sheet.GetLastRow();
                for (int i = startRow; i <= rowCount; ++i)
                {
                    IRow row = sheet.GetRow(i);
                    if (row == null) continue; //没有数据的行默认是null　　　　　　　

                    DataRow dataRow = data.NewRow();
                    for (int j = row.FirstCellNum; j < cellCount; ++j)
                    {
                        if (row.GetCell(j) != null)
                        {
                            if (j == 6 || j == 7)
                            {
                                dataRow[j] = GetCellText(i, j);
                            }
                            else if (row.GetCell(j).CellType == CellType.Numeric)
                            {
                                dataRow[j] = row.GetCell(j).NumericCellValue;
                            }
                            else if (row.GetCell(j).CellType == CellType.String)
                            {
                                dataRow[j] = row.GetCell(j).StringCellValue;
                            }

                        }//同理，没有数据的单元格都默认是null
                    }
                    data.Rows.Add(dataRow);
                }
                return data;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 将excel中的数据导入到DataTable中
        /// </summary>
        /// <param name="sheetName">excel工作薄sheet的名称</param>
        /// <param name="isFirstRowColumn">第一行是否是DataTable的列名</param>
        /// <returns>返回的DataTable</returns>
        public DataTable ExcelToDataTable(string sheetName, bool isFirstRowColumn, out DataTable result)
        {
            ISheet sheet = m_main;
            DataTable data = new DataTable();
            int startRow = 0;
            try
            {
                if (sheet is null)
                {
                    result = null;
                    return null;
                }
                IRow firstRow = sheet.GetRow(0);
                int cellCount = firstRow.LastCellNum; //一行最后一个cell的编号 即总的列数

                if (isFirstRowColumn)
                {

                    for (int i = firstRow.FirstCellNum; i < cellCount; ++i)
                    {
                        ICell cell = firstRow.GetCell(i);
                        if (cell != null)
                        {
                            string cellValue = cell.StringCellValue;
                            if (cellValue != null)
                            {
                                DataColumn column = new DataColumn(cellValue);
                                data.Columns.Add(column);
                            }
                        }
                    }
                    startRow = sheet.FirstRowNum + 1;
                }
                else
                {
                    startRow = sheet.FirstRowNum;
                }

                //最后一列的标号
                int rowCount = sheet.GetLastRow();
                for (int i = startRow; i <= rowCount; ++i)
                {
                    IRow row = sheet.GetRow(i);
                    if (row == null) continue; //没有数据的行默认是null　　　　　　　

                    DataRow dataRow = data.NewRow();
                    for (int j = row.FirstCellNum; j < cellCount; ++j)
                    {
                        if (row.GetCell(j) != null)
                        {
                            if (row.GetCell(j).CellType == CellType.Numeric)
                            {
                                if (HSSFDateUtil.IsCellDateFormatted(row.GetCell(j)))
                                {
                                    dataRow[j] = row.GetCell(j).DateCellValue;
                                }
                                else
                                {
                                    dataRow[j] = row.GetCell(j).NumericCellValue;
                                }
                            }
                            else
                            {
                                dataRow[j] = row.GetCell(j).StringCellValue;
                            }


                        }//同理，没有数据的单元格都默认是null
                    }
                    data.Rows.Add(dataRow);
                }
                result = data;
                return data;
            }
            catch (Exception ex)
            {
                result = null;
                return null;
            }
        }

        public List<Employee> CheckDuplicateWithSelf(int IdCol)
        {
            var result = new List<Employee>();
            DataTable dt;
            this.ExcelToDataTable("Sheet1", true, out dt);
            for (int i = 0; i < dt.Rows.Count - 1; i++)
            {
                for (int j = i + 1; j < dt.Rows.Count; j++)
                {
                    if (dt.Rows[j][IdCol].ToString() == dt.Rows[i][IdCol].ToString())
                    {
                        DataRow destRow = dt.Rows[i];
                        Employee employee = new Employee()
                        {
                            ID = destRow[IdCol].ToString(),
                            Name = destRow[0].ToString(),
                            Job = destRow[2].ToString(),
                            DataDesc = "所提交的表格中存在重复人员：" + destRow[IdCol].ToString(),
                            Valid = false
                        };
                        result.Add(employee);
                        break;
                    }
                }
            }
            return result;
        }

        public List<Employee> CheckDuplcateWithSummary(DataTable sourceTable, int sourceIdCol, int thisIdCol, string mode)
        {
            var result = new List<Employee>();
            string err = string.Empty;
            bool valid = false;
            //读取excel中数据

            DataTable summary = sourceTable;

            DataTable destDT;
            this.ExcelToDataTable("Sheet1", true, out destDT);
            if (summary is null || destDT is null)
            {
                return null;
            }
            foreach (DataRow destRow in destDT.Rows)
            {
                err = string.Empty;
                valid = true;
                bool found = false;
                foreach (DataRow sourceRow in summary.Rows)
                {
                    if (destRow[thisIdCol].ToString() == sourceRow[sourceIdCol].ToString())//如果身份证相等，则复制信息
                    {
                        if (mode == "add")
                        {
                            err = "该员工已存在：" + destRow[thisIdCol].ToString();
                            valid = false;
                        }
                        Employee employee = new Employee()
                        {
                            ID = destRow[thisIdCol].ToString(),
                            Name = destRow[0].ToString(),
                            Job = destRow[3].ToString(),
                            JobType = destRow[2].ToString(),
                            StartDate = sourceRow[6].ToString(),
                            DataDesc = err,
                            Valid = valid
                        };
                        found = true;
                        result.Add(employee);
                        break;
                    }
                }
                if (!found)
                {
                    if (mode == "sub")
                    {
                        err = "该员工不存在：" + destRow[thisIdCol].ToString();
                        valid = false;
                    }
                    Employee employee = new Employee()
                    {
                        ID = destRow[thisIdCol].ToString(),
                        Name = destRow[0].ToString(),
                        Job = destRow[3].ToString(),
                        JobType = destRow[2].ToString(),
                        DataDesc = err,
                        Valid = valid
                    };
                    result.Add(employee);
                }
            }
            return result;
        }

        private void ClearAllRows()
        {
            for (int i = 1; i <= m_main.GetLastRow(); i++)
            {
                m_main.RemoveRow(m_main.GetRow(i));
            }
        }

        /// <summary>
        /// 将DataTable中的数据导入当前Excel的m_main Sheet中
        /// </summary>
        /// <param name="tbl_summary"></param>
        public void DatatableToExcel(DataTable tbl_summary)
        {
            int colNum = tbl_summary.Columns.Count;
            ClearAllRows();
            for (int row = 0; row < tbl_summary.Rows.Count; row++)
            {
                int excel_row = row + 1;
                m_main.CreateRow(excel_row);
                m_main.GetRow(excel_row).CreateCell(0);
                m_main.GetRow(excel_row).GetCell(0).SetCellValue(excel_row);
                for (int column = 1; column < colNum; column++) // 列：公司，姓名，ID，职业类别，工种，生效日期
                {
                    m_main.GetRow(excel_row).CreateCell(column);
                    m_main.GetRow(excel_row).GetCell(column).SetCellValue(tbl_summary.Rows[row][column].ToString());
                }
            }
            Save();
        }
        public void RawDatatableToExcel(DataTable tbl)
        {
            int colNum = tbl.Columns.Count;
            ClearAllRows();
            for (int row = 0; row < tbl.Rows.Count; row++)
            {
                int excel_row = row + 1;
                m_main.CreateRow(excel_row);
                for (int column = 0; column < colNum; column++) // 列：公司，姓名，ID，职业类别，工种，生效日期
                {
                    m_main.GetRow(excel_row).CreateCell(column);
                    m_main.GetRow(excel_row).GetCell(column).SetCellValue(tbl.Rows[row][column].ToString());
                }
            }
            Save();
        }
        public void RemoveById(string id)
        {
            DataTable source;
            ExcelToDataTable("Sheet1", true, out source);
            foreach (DataRow row in source.Rows)
            {
                if (row[3].ToString() == id) //身份证号码列
                {
                    int rowNum = source.Rows.IndexOf(row) + 1;
                    if (rowNum == m_main.GetLastRow())
                    {

                    }
                    else
                    {
                        m_main.ShiftRows(rowNum + 1, m_main.GetLastRow(), -1);
                    }

                    break;
                }
            }
        }

        public void RemoveByRowNum(int num)
        {
            DataTable source;
            ExcelToDataTable("Sheet1", true, out source);
            int rowNum = num;
            if (rowNum == m_main.GetLastRow())
            {
                m_main.RemoveRow(m_main.GetRow(num));
            }
            else
            {
                m_main.ShiftRows(rowNum + 1, m_main.GetLastRow(), -1);
            }
        }

        public Employee SelectByID(string id)
        {
            Employee em = null;
            DataTable source;
            ExcelToDataTable("Sheet1", true, out source);
            foreach (DataRow row in source.Rows)
            {
                if (row["身份证"].ToString() == id)
                {
                    em = new Employee();
                    em.Name = row["姓名"].ToString();
                    em.ID = id;
                    em.Job = row["工种"].ToString();
                    em.JobType = row["职业类别"].ToString();
                    try
                    {
                        em.StartDate = row["生效日期"].ToString();
                    }
                    catch
                    {
                        em.StartDate = row["保障开始时间"].ToString();
                    }

                    try
                    {
                        em.EndDate = row["离职日期"].ToString();
                    }
                    catch
                    {
                        em.EndDate = row["保障结束时间"].ToString();
                    }
                    em.Company = Path.GetFileNameWithoutExtension(fileName);
                    break;
                }
            }
            return em;
        }

        public DataTable SelectPeopleByNameAndID(string name, int nameCol, string id, int idCol)
        {
            DataTable result = new DataTable();
            result.Columns.Add(new DataColumn("company"));
            result.Columns.Add(new DataColumn("name"));
            result.Columns.Add(new DataColumn("id"));
            result.Columns.Add(new DataColumn("job"));
            result.Columns.Add(new DataColumn("type"));
            result.Columns.Add(new DataColumn("start_date"));
            result.Columns.Add(new DataColumn("end_date"));

            DataTable source;
            ExcelToDataTable("Sheet1", true, out source);
            if (source is null)
            {
                DataRow newRow = result.NewRow();
                newRow["name"] = "未找到符合条件的人员";
                newRow["id"] = string.Empty;
                result.Rows.Add(newRow);
                return result;
            }
            if (string.IsNullOrEmpty(id))
            {
                foreach (DataRow row in source.Rows)
                {
                    if (row["姓名"].ToString().IndexOf(name) >= 0)
                    {
                        DataRow newRow = result.NewRow();
                        newRow["name"] = row["姓名"];
                        newRow["id"] = row["身份证"];
                        newRow["job"] = row["工种"];
                        newRow["type"] = row["职业类别"];
                        newRow["start_date"] = row["保障开始时间"];
                        newRow["end_date"] = row["保障结束时间"];
                        newRow["company"] = new FileInfo(fileName).Directory.Parent.Name;
                        result.Rows.Add(newRow);
                    }
                }
            }
            else
            {
                foreach (DataRow row in source.Rows)
                {
                    if (row["身份证"].ToString() == id)
                    {
                        DataRow newRow = result.NewRow();
                        newRow["name"] = row["姓名"];
                        newRow["id"] = id;
                        newRow["job"] = row["工种"];
                        newRow["type"] = row["职业类别"];
                        newRow["start_date"] = row["保障开始时间"];
                        newRow["end_date"] = row["保障结束时间"];
                        newRow["company"] = new FileInfo(fileName).Directory.Parent.Name;
                        result.Rows.Add(newRow);
                    }
                }
            }
            if (result.Rows.Count <= 0)
            {
                DataRow newRow = result.NewRow();
                newRow["name"] = "未找到符合条件的人员";
                newRow["id"] = string.Empty;
                result.Rows.Add(newRow);
            }
            return result;
        }

        public static bool CreateNewCompanyTable(NewUserModel user, out string companyDir)
        {
            bool result = true;
            string name = user.CompanyName;
            string dir = string.Empty;
            UserInfoModel father = DatabaseService.SelectUser(user.Father);
            try
            {
                string fatherDir = Directory.GetDirectories(Utility.Instance.ExcelRoot, father.CompanyName, SearchOption.AllDirectories).FirstOrDefault();
                dir = Path.Combine(fatherDir, name);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var plans = user._Plan.Split("_");
                foreach (string plan in plans)
                {
                    string planDir = Path.Combine(dir, plan);
                    if (!Directory.Exists(planDir))
                    {
                        Directory.CreateDirectory(planDir);
                        File.Copy(Path.Combine(Utility.Instance.WebRootFolder, "Excel", "SummaryTemplate.xls"), Path.Combine(planDir, name + ".xls"), true); //创建 公司名.xls
                        File.CreateText(Path.Combine(planDir, name + "_0.txt"));
                    }

                }

            }
            catch
            {
                result = false;
            }
            companyDir = dir;
            return result;
        }

        public List<Employee> CheckJobType(int jobCol)
        {
            List<string> jobs = new List<string>() {
                "1-4类",
                "4类以上"
            };
            List<Employee> result = new List<Employee>();
            IRow currentRow;
            int lastRowNum = m_main.GetLastRow();
            for (int i = 1; i <= lastRowNum; i++) //从第二行开始遍历，第一行为标题行
            {
                currentRow = m_main.GetRow(i);
                if (jobs.IndexOf(currentRow.Cells[jobCol].ToString()) < 0)
                {
                    Employee e = new Employee()
                    {
                        ID = currentRow.Cells[1].ToString(),
                        Name = currentRow.Cells[0].ToString(),
                        Job = currentRow.Cells[jobCol].ToString(),
                        DataDesc = "职业类别错误，1-4类 或 4类以上",
                        Valid = false
                    };
                    result.Add(e);
                }
                else
                {
                    Employee e = new Employee()
                    {
                        ID = currentRow.Cells[1].ToString(),
                        Name = currentRow.Cells[0].ToString(),
                        Job = currentRow.Cells[jobCol].ToString(),
                        DataDesc = "",
                        Valid = true
                    };
                    result.Add(e);
                }
            }
            return result;
        }

        public List<Employee> ValidateIDs(int idCol)
        {
            List<Employee> result = new List<Employee>();
            IRow currentRow;
            string id;
            int lastRowNum = m_main.GetLastRow();
            for (int i = 1; i <= lastRowNum; i++) //从第二行开始遍历，第一行为标题行
            {
                currentRow = m_main.GetRow(i);
                id = currentRow.Cells[idCol].ToString();
                IDCardValidation idTool = new IDCardValidation();
                if (!idTool.CheckIDCard(id))
                {
                    Employee e = new Employee()
                    {
                        ID = id,
                        Name = currentRow.Cells[0].ToString(),
                        JobType = currentRow.Cells[2].ToString(),
                        Job = currentRow.Cells[3].ToString(),
                        DataDesc = "身份证疑似错误：" + id,
                        Valid = false
                    };
                    result.Add(e);
                }
                else
                {
                    Employee e = new Employee()
                    {
                        ID = id,
                        Name = currentRow.Cells[0].ToString(),
                        JobType = currentRow.Cells[2].ToString(),
                        Job = currentRow.Cells[3].ToString(),
                        DataDesc = "",
                        Valid = true
                    };
                    result.Add(e);
                }
            }
            return result;
        }

        public void Dispose()
        {
            m_main = null;
            workbook = null;
        }

        #region Properties
        public string FilePath
        {
            get
            {
                return fileName;
            }
        }
        #endregion
    }
}
