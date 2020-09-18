using DatabaseHelper;
using Insurance.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using VirtualCredit.LogServices;
using VirtualCredit.Models;
using VirtualCredit.Services;

namespace VirtualCredit
{
    enum BuildStringType
    {
        update,
        parameter,
        value
    }

    public enum OnlineState
    {
        Off = 0,
        On = 1
    }


    public static class DatabaseService
    {
        public static string ConnStr;
        public static string userInfoTableName;
        static DatabaseService()
        {
            userInfoTableName = "UserInfo";
        }

        public static bool CreateNewCompanyTable(string tblName)
        {
            SQLServerHelper.ConnectionString = ConnStr;
            string cmdText = $"Create table {tblName}(Name nvarchar(20),IDCard nvarchar(50),Company nvarchar(50),Type nvarchar(50),Job nvarchar(50),StartDate date,ResignDate date,TotalPrice nvarchar(50),PayBack nvarchar(50))";
            return SQLServerHelper.ExecuteNonQuery(cmdText);
        }

        public static List<string> GetAllCompanies()
        {
            List<string> res = new List<string>();
            SQLServerHelper.ConnectionString = ConnStr;
            string cmdText = "select Name from sysobjects where xtype = 'U '";
            DataTable dt = SQLServerHelper.ExecuteReader(cmdText);
            if (dt is null || dt.Rows.Count <= 0)
            {
                return null;
            }
            foreach (DataRow r in dt.Rows)
            {
                res.Add(r["Name"].ToString());
            }
            return res;
        }

        /// <summary>
        /// 添加一个新故事
        /// </summary>
        /// <param name="tableName">往tableName表中添加数据</param>
        /// <param name="story">要添加的故事</param>
        /// <returns></returns>
        public static bool InsertDailyDetail(DailyDetailModel model)
        {
            try
            {
                SQLServerHelper.ConnectionString = ConnStr;
                string cmdText = $"Insert into DailyDetail (Date,Company,SubmittedBy,TotalPrice,Product,NewAdd,Reduce) values ('{model.Date.ToString("yyyy-MM-dd")}','{model.Company}','{model.SubmittedBy}',{model.TotalPrice},'{model.Product}',{model.NewAdd},{model.Reduce})";
                return SQLServerHelper.ExecuteNonQuery(cmdText);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return false;
            }

        }

        public static bool BulkInsert(string tblName, DataTable dataTable)
        {
            return SQLServerHelper.BulkInsert(tblName, dataTable);
        }


        /// <summary>
        /// 添加一个新故事
        /// </summary>
        /// <param name="tableName">往tableName表中添加数据</param>
        /// <param name="story">要添加的故事</param>
        /// <returns></returns>
        public static bool InsertStory(string tableName, object story)
        {
            try
            {
                SQLServerHelper.ConnectionString = ConnStr;
                List<string> propList = GetDatabaseProperties(story, typeof(DatabasePropAttribute));
                if (propList is null)
                {
                    return false;
                }
                string propNamePattern = BuildString(propList, BuildStringType.parameter);
                string valuePattern = BuildString(propList, BuildStringType.value);
                SqlParameter[] sqlParameters = BuildSqlParameter(propList, story);

                if (propNamePattern == string.Empty || valuePattern == string.Empty)
                {
                    return false;
                }

                string cmdText = $"Insert into {tableName} ({propNamePattern}) values ({valuePattern})";
                return SQLServerHelper.ExecuteNonQuery(cmdText, sqlParameters);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return false;
            }

        }
        public static UserInfoModel SelectUserByCompanyAndPlan(string companyname, string plan)
        {
            UserInfoModel uim = new UserInfoModel();
            string[] cols = new string[] { "CompanyName", "_Plan" };
            string[] values = new string[] { companyname, plan };
            DataTable dt = SelectMultiPropFromTable("UserInfo", cols, values);
            if (dt != null && dt.Rows.Count > 0)
            {
                DataRow row = dt.Rows[0];
                foreach (DataColumn col in dt.Columns)
                {
                    foreach (var prop in uim.GetType().GetProperties())
                    {
                        if (prop.Name.Equals(col.ColumnName, StringComparison.CurrentCultureIgnoreCase))
                        {
                            object obj = new object();
                            obj = row[col.ColumnName];
                            if (row[col.ColumnName] is DBNull)
                            {
                                obj = null;
                            }
                            prop.SetValue(uim, obj);
                            break;
                        }
                    }
                }
            }
            else
            {
                uim = null;
            }
            return uim;
        }
        public static UserInfoModel SelectUserByCompany(string companyname)
        {
            UserInfoModel uim = new UserInfoModel();
            DataTable dt = SelectPropFromTable("UserInfo", "CompanyName", companyname);
            if (dt != null && dt.Rows.Count > 0)
            {
                DataRow row = dt.Rows[0];
                foreach (DataColumn col in dt.Columns)
                {
                    foreach (var prop in uim.GetType().GetProperties())
                    {
                        if (prop.Name.Equals(col.ColumnName, StringComparison.CurrentCultureIgnoreCase))
                        {
                            object obj = new object();
                            obj = row[col.ColumnName];
                            if (row[col.ColumnName] is DBNull)
                            {
                                obj = null;
                            }
                            prop.SetValue(uim, obj);
                            break;
                        }
                    }
                }
            }
            else
            {
                uim = null;
            }
            return uim;
        }

        /// <summary>
        /// 验证用户名密码是否匹配
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static UserInfoModel UserMatchUserNamePassword(IUser user)
        {
            try
            {
                return SelectLoginUser(user);
            }
            catch
            {
                return null;
            }

        }

        public static DataTable SelectChildAccounts(UserInfoModel currUser)
        {
            DataTable res = new DataTable();

            SQLServerHelper.ConnectionString = ConnStr;
            if (currUser is null)
            {
                return null;
            }
            try
            {
                string cmd = string.Empty;
                DataTable temp = new DataTable();
                if (currUser.AccessLevel == 0) //如果是超管
                {
                    cmd = "select * from UserInfo where userName=@userName";
                    SqlParameter sp = new SqlParameter("@userName", currUser.UserName);
                    res = SQLServerHelper.ExecuteReader(cmd, sp);
                    cmd = $"select * from UserInfo where AccessLevel > 0";
                    temp = SQLServerHelper.ExecuteReader(cmd);
                    if (temp != null && temp.Rows.Count > 0)
                    {
                        foreach (DataRow row in temp.Rows)
                        {
                            DataRow newRow = res.NewRow();
                            newRow.ItemArray = row.ItemArray;
                            res.Rows.Add(newRow);
                        }
                    }
                }
                else
                {
                    cmd = $"select * from UserInfo where (Father = @Father and userName <> '{currUser.UserName}')";
                    SqlParameter sp1 = new SqlParameter("@Father", currUser.UserName);
                    temp = SQLServerHelper.ExecuteReader(cmd, sp1);
                    if (temp != null && temp.Rows.Count > 0)
                    {
                        res = temp.Clone();
                        foreach (DataRow row in temp.Rows)
                        {
                            DataRow newRow = res.NewRow();
                            newRow.ItemArray = row.ItemArray;
                            res.Rows.Add(newRow);
                        }
                    }
                }
                return res;
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return null;
            }
        }


        /// <summary>
        /// 查找user的用户名是否存在
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static bool UserMatchUserNameOnly(IUser user)
        {
            try
            {
                if (SelectUser(user.UserName) != null)
                    return true;
                else
                    return false;
            }
            catch
            {
                return true;
            }
        }

        public static int Delete(string table, string userName)
        {
            SQLServerHelper.ConnectionString = ConnStr;
            if (userInfoTableName == string.Empty)
            {
                return 0;
            }
            try
            {
                string cmd = $"delete from {table} where userName=@userName";
                SqlParameter userNamePara = new SqlParameter("@userName", userName);
                return SQLServerHelper.ExecuteDelete(cmd, userNamePara);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return 0;
            }
        }

        public static bool IsMailUsed(string mail)
        {
            SQLServerHelper.ConnectionString = ConnStr;
            if (userInfoTableName == string.Empty)
            {
                return false;
            }
            string tableName = userInfoTableName;
            DataTable dt = new DataTable();
            try
            {
                string cmd = $"select * from {tableName} where MailAddress=@mailAddress";
                SqlParameter userNamePara = new SqlParameter("@mailAddress", mail);
                dt = SQLServerHelper.ExecuteReader(cmd, userNamePara);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return false;
            }
        }


        /// <summary>
        /// 查找user的用户名
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static UserInfoModel SelectUser(string userName)
        {
            SQLServerHelper.ConnectionString = ConnStr;
            if (userInfoTableName == string.Empty)
            {
                return null;
            }

            string tableName = userInfoTableName;
            DataTable dt = new DataTable();
            try
            {
                string cmd = $"select * from {tableName} where userName=@userName";
                SqlParameter userNamePara = new SqlParameter("@userName", userName);
                dt = SQLServerHelper.ExecuteReader(cmd, userNamePara);
                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    UserInfoModel uim = new UserInfoModel();
                    CreateModelFromDataRow(row, uim);
                    return uim;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void CreateModelFromDataRow(DataRow row, ViewModelBase model)
        {
            foreach (DataColumn col in row.Table.Columns)
            {
                foreach (var prop in model.GetType().GetProperties())
                {
                    if (prop.Name.Equals(col.ColumnName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        object obj = new object();
                        obj = row[col.ColumnName];
                        if (row[col.ColumnName] is DBNull)
                        {
                            obj = null;
                        }
                        prop.SetValue(model, obj);
                        break;
                    }
                }
            }
        }

        public static UserInfoModel UserInfo(UserInfoModel user)
        {
            SQLServerHelper.ConnectionString = ConnStr;
            if (userInfoTableName == string.Empty)
            {
                return null;
            }
            string tableName = userInfoTableName;
            DataTable dt = new DataTable();
            try
            {
                string cmd = $"select * from {tableName} where userName=@userName";
                SqlParameter userNamePara = new SqlParameter("@userName", user.UserName);
                dt = SQLServerHelper.ExecuteReader(cmd, userNamePara);
                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    UserInfoModel uim = new UserInfoModel();
                    uim.UserName = row["userName"].ToString();
                    uim.IsOnline = row["isOnline"].ToString();
                    uim.IPAddress = row["IPAddress"].ToString();
                    return uim;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static UserInfoModel SelectLoginUser(IUser user)
        {
            SQLServerHelper.ConnectionString = ConnStr;
            if (userInfoTableName == string.Empty)
            {
                return null;
            }
            string tableName = userInfoTableName;
            DataTable dt = new DataTable();
            try
            {
                string cmd = $"select * from {tableName} where userName=@userName and userPassword collate Chinese_PRC_CS_AS=@userPassword";
                SqlParameter userNamePara = new SqlParameter("@userName", user.UserName);
                SqlParameter userPwdPara = new SqlParameter("@userPassword", user.userPassword);
                dt = SQLServerHelper.ExecuteReader(cmd, userNamePara, userPwdPara);

                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    UserInfoModel uim = new UserInfoModel();
                    uim.UserName = row["userName"].ToString();
                    uim.userPassword = row["userPassword"].ToString();
                    uim.IsOnline = row["isOnline"].ToString();
                    uim.CompanyName = row["CompanyName"].ToString();
                    uim.AccessLevel = (int)row["AccessLevel"];
                    uim.CompanyNameAbb = row["CompanyNameAbb"].ToString();
                    uim.Mail = row["Mail"].ToString();
                    uim.TaxNum = row["TaxNum"].ToString();
                    uim.Telephone = row["Telephone"].ToString();
                    uim.Name = row["Name"].ToString();
                    uim.AllowCreateAccount = row["AllowCreateAccount"].ToString();
                    uim.RecipeAccount = row["RecipeAccount"].ToString();
                    uim.RecipeAddress = row["RecipeAddress"].ToString();
                    uim.RecipeBank = row["RecipeBank"].ToString();
                    uim.RecipeCompany = row["RecipeCompany"].ToString();
                    uim.RecipePhone = row["RecipePhone"].ToString();
                    uim.RecipeType = row["RecipeType"].ToString();
                    uim.Father = row["Father"].ToString();
                    uim._Plan = row["_Plan"].ToString();
                    uim.ChildAccounts = new List<UserInfoModel>();
                    if (!string.IsNullOrEmpty(row["UnitPrice"].ToString()))
                    {
                        uim.UnitPrice = Convert.ToInt32(row["UnitPrice"].ToString());
                    }
                    else
                    {
                        uim.UnitPrice = 30;
                    }

                    if (!string.IsNullOrEmpty(row["DaysBefore"].ToString()))
                    {
                        uim.DaysBefore = Convert.ToInt16(row["DaysBefore"].ToString());
                    }
                    else
                    {
                        uim.DaysBefore = 0;
                    }

                    return uim;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool IsUserOnline(UserInfoModel user, HttpContext httpContext)
        {
            UserInfoModel uim = SelectLoginUser(user);
            if (uim == null) return false;

            if (uim.IsOnline == httpContext.Session.Id)
                return true;
            else
                return false;
        }
        public static DataTable SelectDailyDetailByDatetime(List<DateTime> dateTimes, List<string> companies, List<string> plans)
        {
            string dateList = "(";
            foreach (var date in dateTimes)
            {
                dateList += $"'{date.ToString("yyyy-MM-dd")}',";
            }
            dateList = dateList.Remove(dateList.LastIndexOf(","));
            dateList += ")";

            string companyList = "(";
            foreach (var comp in companies)
            {
                companyList += $"'{comp}',";
            }
            companyList = companyList.Remove(companyList.LastIndexOf(","));
            companyList += ")";

            string planList = "(";
            foreach (var plan in plans)
            {
                planList += $"'{plan}',";
            }
            planList = planList.Remove(planList.LastIndexOf(","));
            planList += ")";

            string cmd = $"Select YMDDate,Sum(DailyPrice),Sum(HeadCount) from DailyDetailData where YMDDate in {dateList} and Company in {companyList} and Product in {planList} group by YMDDate";
            return SQLServerHelper.ExecuteReader(cmd, null);
        }



        public static DataTable Select(string tableName)
        {
            try
            {
                SQLServerHelper.ConnectionString = ConnStr;
                string cmd = $"select * from {tableName}";
                DataTable dt = new DataTable();
                dt = SQLServerHelper.ExecuteReader(cmd);
                //LogService.Log(dt.Rows.Count.ToString());
                if (dt == null || dt.Rows.Count == 0)
                {
                    return null;
                }
                return dt;
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
            }
            return null;
        }

        /// <summary>
        /// 更新一个表中的某行数据
        /// </summary>
        /// <param name="model"></param>
        /// <param name="tableName"></param>
        /// <param name="colName"></param>
        /// <param name="colValue"></param>
        public static bool UpdateOneRowInTable(ViewModelBase model, string tableName, string colName, object colValue)
        {
            try
            {
                //获取含有DatabaseProp特性的属性
                List<string> paraList = GetDatabaseProperties(model, typeof(DatabasePropAttribute));
                string para = BuildString(paraList, BuildStringType.update);//生成update语句 set 部分
                SqlParameter[] sqlParameters = new SqlParameter[paraList.Count + 1];//新建sqlParameters数组对象
                BuildSqlParameter(paraList, model).CopyTo(sqlParameters, 0);//生成SqlParameter参数链表
                sqlParameters[paraList.Count] = new SqlParameter("@colName", colValue);

                if (userInfoTableName == string.Empty)
                {
                    return false;
                }
                SQLServerHelper.ConnectionString = ConnStr;
                string cmd = $"update {tableName} set {para} where {colName}=@colName";
                return SQLServerHelper.ExecuteNonQuery(cmd, sqlParameters);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return false;
            }
        }

        public static bool UpdateOneColumn(string tableName, string flagCol, string flagColName, string targetColName, object targetValue)
        {
            try
            {
                if (userInfoTableName == string.Empty)
                {
                    return false;
                }
                SQLServerHelper.ConnectionString = ConnStr;
                string cmd = $"update {tableName} set {targetColName} = @targetColValue where {flagCol}=@colName";
                SqlParameter parameter = new SqlParameter("@colName", flagColName);
                SqlParameter parameter1 = new SqlParameter("@targetColValue", targetValue);
                return SQLServerHelper.ExecuteNonQuery(cmd, parameter, parameter1);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return false;
            }
        }


        /// <summary>
        /// 更新model中，paraList指定要更新的参数，更新为model中的属性值
        /// </summary>
        /// <param name="user"></param>
        /// <param name="paraList"></param>
        public static bool UpdateUserInfo(UserInfoModel user, List<string> paraList)
        {
            string para = BuildString(paraList, BuildStringType.update);
            SqlParameter[] sqlParameters = new SqlParameter[paraList.Count + 1];
            BuildSqlParameter(paraList, user).CopyTo(sqlParameters, 0);
            sqlParameters[paraList.Count] = new SqlParameter("@userName", user.UserName);

            if (userInfoTableName == string.Empty)
            {
                return false;
            }
            string tableName = userInfoTableName;
            try
            {
                SQLServerHelper.ConnectionString = ConnStr;
                string cmd = $"update {tableName} set {para} where userName=@userName";
                return SQLServerHelper.ExecuteNonQuery(cmd, sqlParameters);
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return false;
            }
        }

        public static DataTable SelectPropFromTable(string tableName, string colName, string colValue)
        {
            if (colName is null)
            {
                return null;
            }
            if (string.IsNullOrEmpty(SQLServerHelper.ConnectionString) || SQLServerHelper.ConnectionString != ConnStr)
            {
                SQLServerHelper.ConnectionString = ConnStr;
            }
            string cmdText = $"select * from {tableName} where {colName}= @value";
            SqlParameter parameter = new SqlParameter("@value", colValue);
            return SQLServerHelper.ExecuteReader(cmdText, parameter);
        }
        public static DataTable SelectMultiPropFromTable(string tableName, string[] colNames, string[] colValues)
        {
            if (colNames == null || colNames.Length <= 0)
            {
                return null;
            }
            if (string.IsNullOrEmpty(SQLServerHelper.ConnectionString) || SQLServerHelper.ConnectionString != ConnStr)
            {
                SQLServerHelper.ConnectionString = ConnStr;
            }

            string cmdText = $"select * from {tableName} where 1=1";
            List<string> colNamesList = new List<string>(colNames);
            foreach (var item in colNames)
            {
                cmdText += $" and {item}=@{item}";
            }
            List<SqlParameter> sqlParameters = new List<SqlParameter>();
            foreach (var item in colNamesList)
            {
                var para = new SqlParameter($"@{item}", colValues[colNamesList.IndexOf(item)]);
                sqlParameters.Add(para);
            }
            return SQLServerHelper.ExecuteReader(cmdText, sqlParameters.ToArray());
        }

        public static DataTable SelectPeopleByNameAndID(string tableName, string name, string id)
        {
            if (tableName is null)
            {
                return null;
            }
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(id))
            {
                return null;
            }
            if (string.IsNullOrEmpty(SQLServerHelper.ConnectionString) || SQLServerHelper.ConnectionString != ConnStr)
            {
                SQLServerHelper.ConnectionString = ConnStr;
            }
            string cmdText = $"select * from {tableName} where (Name like @value or IDCard like @IdCard)";
            SqlParameter parameter1 = new SqlParameter("@value", $"%{name}%");
            SqlParameter parameter2 = new SqlParameter("@IdCard", $"{id}");
            return SQLServerHelper.ExecuteReader(cmdText, parameter1, parameter2);
        }

        public static DataTable SelectStoryAndUpdateViews(string tableName, string colName, string colValue)
        {
            string cmdTxt = $"update {tableName} set Views = Views + 1 where {colName} = @value";
            LogService.Log(cmdTxt.Replace("@value", colValue));
            SqlParameter parameter = new SqlParameter("@value", colValue);
            SQLServerHelper.ExecuteNonQuery(cmdTxt, parameter);
            return SelectPropFromTable(tableName, colName, colValue);
        }

        public static DataTable SelectStory(string tableName, string roleName)
        {
            if (roleName == string.Empty)
            {
                return null;
            }
            SQLServerHelper.ConnectionString = ConnStr;
            string cmdText = $"select * from {tableName} where IdRole=@IdRole";
            SqlParameter sqlParameter = new SqlParameter("@IdRole", roleName);
            return SQLServerHelper.ExecuteReader(cmdText, sqlParameter);
        }

        private static SqlParameter[] BuildSqlParameter(List<string> propList, object obj)
        {
            SqlParameter[] sqlParameters = new SqlParameter[propList.Count];
            for (int i = 0; i < sqlParameters.Length; i++)
            {
                var val = obj.GetType().GetProperty(propList[i]).GetValue(obj);
                if (val is null)
                {
                    val = string.Empty;
                }
                sqlParameters[i] = new SqlParameter($"@{propList[i]}", val);
            }

            return sqlParameters;
        }

        private static string BuildString(List<string> propList, BuildStringType buildStringType)
        {
            string propNamePattern = string.Empty;
            string valuePattern = string.Empty;
            string updatePattern = string.Empty;
            foreach (var prop in propList)
            {
                if (propList.IndexOf(prop) != propList.Count - 1)
                {
                    propNamePattern += prop + ",";
                    valuePattern += "@" + prop + ",";
                    updatePattern += prop + "=" + "@" + prop + ",";
                }
                else
                {
                    propNamePattern += prop;
                    valuePattern += "@" + prop;
                    updatePattern += prop + "=" + "@" + prop;
                }
            }

            switch (buildStringType)
            {
                case BuildStringType.parameter:
                    return propNamePattern;
                case BuildStringType.value:
                    return valuePattern;
                case BuildStringType.update:
                    return updatePattern;
            }
            return string.Empty;
        }

        static List<string> GetDatabaseProperties(object obj, Type attributeType)
        {
            PropertyInfo[] infos = obj.GetType().GetProperties();
            List<string> list = new List<string>();
            if (infos == null || infos.Length <= 0)
            {
                return null;
            }
            foreach (var info in infos)
            {
                foreach (var att in info.GetCustomAttributes())
                {
                    if (att.GetType() == attributeType)
                    {
                        list.Add(info.Name);
                        break;
                    }
                }
            }
            return list;
        }
    }


}
