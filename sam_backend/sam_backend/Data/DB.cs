using SamErpBackend.Models;
using Microsoft.Extensions.Options;
using System.Data;
using Npgsql;

namespace SamErpBackend.Data
{
    public class DB
    {
        private IOptions<AppSettingsModel> settings;
        public string DBErr = "";
        private static string server = "";
        private static string db     = "";
        private static string user   = "";
        private static string pass   = "";
        public NpgsqlConnection link;

        public DB(IOptions<AppSettingsModel> settings)
        {
            this.settings = settings;
            server = settings.Value.server;
            db     = settings.Value.db;
            user   = settings.Value.user;
            pass   = settings.Value.password;

            // PostgreSQL connection string (Npgsql)
            link = new NpgsqlConnection(
                $"Host={server};Port=5432;Database={db};Username={user};Password={pass}");
        }

        public DataTable GetDataTable(string sQry)
        {
            DataTable dataTable = new DataTable();
            this.DBErr = "";
            try
            {
                this.link.Open();
                new NpgsqlDataAdapter(new NpgsqlCommand(sQry, this.link)).Fill(dataTable);
                return dataTable;
            }
            catch (Exception ex)
            {
                this.DBErr = "GetDataTable : " + ex.Message.ToString();
                return (DataTable)null!;
            }
            finally
            {
                this.link.Close();
            }
        }

        public DataSet GetDataSet(string sQry)
        {
            DataSet dataSet = new DataSet();
            this.DBErr = "";
            try
            {
                this.link.Open();
                new NpgsqlDataAdapter(new NpgsqlCommand(sQry, this.link)).Fill(dataSet);
                return dataSet;
            }
            catch (Exception ex)
            {
                this.DBErr = "GetDataSet : " + ex.Message.ToString();
                return (DataSet)null!;
            }
            finally
            {
                this.link.Close();
            }
        }

        public object ExecScalar(string sQry)
        {
            this.DBErr = "";
            try
            {
                this.link.Open();
                return new NpgsqlCommand(sQry, this.link).ExecuteScalar()!;
            }
            catch (Exception ex)
            {
                this.DBErr = "ExecScalar : " + ex.Message.ToString();
                return null!;
            }
            finally
            {
                this.link.Close();
            }
        }

        public int ExecQry(string Qry)
        {
            this.DBErr = "";
            try
            {
                link.Open();
                return new NpgsqlCommand(Qry, link).ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                this.DBErr = "ExecQry : " + ex.Message.ToString();
                return -1;
            }
            finally
            {
                link.Close();
            }
        }

        // Parameterized query helper — use this to prevent SQL injection
        public DataTable GetDataTableParam(string sQry, Dictionary<string, object> parameters)
        {
            DataTable dataTable = new DataTable();
            this.DBErr = "";
            try
            {
                this.link.Open();
                var cmd = new NpgsqlCommand(sQry, this.link);
                foreach (var kv in parameters)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                new NpgsqlDataAdapter(cmd).Fill(dataTable);
                return dataTable;
            }
            catch (Exception ex)
            {
                this.DBErr = "GetDataTableParam : " + ex.Message.ToString();
                return (DataTable)null!;
            }
            finally
            {
                this.link.Close();
            }
        }

        public int ExecQryParam(string Qry, Dictionary<string, object> parameters)
        {
            this.DBErr = "";
            try
            {
                link.Open();
                var cmd = new NpgsqlCommand(Qry, link);
                foreach (var kv in parameters)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                this.DBErr = "ExecQryParam : " + ex.Message.ToString();
                return -1;
            }
            finally
            {
                link.Close();
            }
        }
    }
}
