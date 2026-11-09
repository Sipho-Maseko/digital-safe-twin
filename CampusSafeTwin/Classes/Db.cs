using System;
using System.Configuration;
using System.Data;
using Npgsql;

namespace CampusSafeTwin.Classes
{
    public static class Db
    {
        // gets the connection string from web config...
        private static string ConnectionString
        {
            get
            {
                var connSetting = ConfigurationManager.ConnectionStrings["CampusSafe"];
                if (connSetting == null || string.IsNullOrWhiteSpace(connSetting.ConnectionString))
                {
                    throw new InvalidOperationException("Connection string 'CampusSafe' is missing or empty in Web.config.");
                }
                return connSetting.ConnectionString;
            }
        }

        // opens connection to postgres db,,,
        public static NpgsqlConnection GetConnection()
        {
            var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        // runs update or insert queries without returning rows...
        public static int ExecuteNonQuery(string sql, params NpgsqlParameter[] parameters)
        {
            using (var conn = GetConnection())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    cmd.Parameters.AddRange(parameters);
                }
                return cmd.ExecuteNonQuery();
            }
        }

        // runs query that returns a single value,,,
        public static object ExecuteScalar(string sql, params NpgsqlParameter[] parameters)
        {
            using (var conn = GetConnection())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    cmd.Parameters.AddRange(parameters);
                }
                return cmd.ExecuteScalar();
            }
        }

        // gets data table from sql query...
        public static DataTable ExecuteDataTable(string sql, params NpgsqlParameter[] parameters)
        {
            using (var conn = GetConnection())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    cmd.Parameters.AddRange(parameters);
                }
                using (var da = new NpgsqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    da.Fill(dt);
                    return dt;
                }
            }
        }

        // makes sql parameter helper...
        public static NpgsqlParameter Param(string name, object value)
        {
            return new NpgsqlParameter(name, value ?? DBNull.Value);
        }
    }
}
