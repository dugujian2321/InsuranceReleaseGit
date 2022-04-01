using System;
using System.Data;
using System.Data.SqlClient;
using VirtualCredit.LogServices;

namespace DatabaseHelper
{
    public static class SQLServerHelper
    {
        public static string ConnectionString { get; set; }

        static bool IsConnectionStringEmpty()
        {
            return ConnectionString == string.Empty || ConnectionString == null;
        }

        public static DataTable ExecuteReader(string commandText, params SqlParameter[] sqlParameter)
        {
            DataSet ds = new DataSet();
            try
            {
                if (IsConnectionStringEmpty()) //若连接字符串为空，则返回null
                    return null;

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = commandText;
                        if (sqlParameter != null)
                            cmd.Parameters.AddRange(sqlParameter);
                        SqlDataAdapter sqlDataAdapter = new SqlDataAdapter();
                        sqlDataAdapter.SelectCommand = cmd;
                        conn.Open();
                        sqlDataAdapter.Fill(ds);
                        if (ds.Tables != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                        {
                            return ds.Tables[0];
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return null;
            }
        }

        public static int ExecuteDelete(string commandText, params SqlParameter[] sqlParam)
        {
            if (IsConnectionStringEmpty()) //若连接字符串为空，则返回false
                return 0;
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = commandText;
                        cmd.Parameters.AddRange(sqlParam);
                        conn.Open();
                        return cmd.ExecuteNonQuery(); ;
                    }
                }
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return 0;
            }
        }
        public static bool BulkInsert(string tblName, DataTable dt)
        {
            SqlBulkCopy sqlBulkCopy;
            try
            {
                if (dt == null || dt.Rows.Count == 0)
                {
                    return false;
                }
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    sqlBulkCopy = new SqlBulkCopy(conn);
                    sqlBulkCopy.DestinationTableName = tblName;
                    sqlBulkCopy.BatchSize = dt.Rows.Count;
                    conn.Open();
                    sqlBulkCopy.WriteToServer(dt);
                }
                return true;
            }
            catch(Exception e)
            {
                return false;
            }
        }
        public static bool ExecuteNonQuery(string commandText, params SqlParameter[] sqlParam)
        {
            if (IsConnectionStringEmpty()) //若连接字符串为空，则返回false
                return false;
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = commandText;
                        cmd.Parameters.AddRange(sqlParam);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                LogService.Log(e.Message);
                return false;
            }

        }

    }


}
