using Microsoft.Extensions.Options;
using SamErpBackend.Models;
using SamErpBackend.Data;
using System.Data;

namespace SamErpBackend
{
    public class Helper
    {
        // ── Access helper (same pattern as RHEOv7) ────────────────────────────
        public static DataTable GetAccess(string User, IOptions<AppSettingsModel> settings)
        {
            DB db = new DB(settings);
            string qry = "SELECT access, case when username is null then 'false' else 'true' end as status " +
                         "FROM access_master LEFT JOIN map_master " +
                         "ON access_master.access_id = map_master.access_id " +
                         "AND username = '" + User + "';";
            DataTable data = db.GetDataTable(qry);
            return data;
        }

        // ── Convert DataTable rows → List<Dictionary> ─────────────────────────
        public static List<Dictionary<string, object>> GetTableRows(DataTable dtData)
        {
            List<Dictionary<string, object>> lstRows = new List<Dictionary<string, object>>();
            Dictionary<string, object> dictRow = null!;
            if (dtData != null && dtData.Rows.Count > 0)
            {
                foreach (DataRow dr in dtData.Rows)
                {
                    dictRow = new Dictionary<string, object>();
                    foreach (DataColumn col in dtData.Columns)
                    {
                        dictRow.Add(col.ColumnName, dr[col].ToString()!);
                    }
                    lstRows.Add(dictRow);
                }
            }
            return lstRows;
        }

        // ── Convert DataSet → array of List<Dictionary> ───────────────────────
        public static List<Dictionary<string, object>>[] GetSetRows(DataSet dtData)
        {
            List<Dictionary<string, object>>[] lstSet = new List<Dictionary<string, object>>[dtData.Tables.Count];
            for (int i = 0; i < dtData.Tables.Count; i++)
            {
                List<Dictionary<string, object>> lstRows = new List<Dictionary<string, object>>();
                Dictionary<string, object> dictRow = null!;
                if (dtData != null && dtData.Tables[i].Rows.Count > 0)
                {
                    foreach (DataRow dr in dtData.Tables[i].Rows)
                    {
                        dictRow = new Dictionary<string, object>();
                        foreach (DataColumn col in dtData.Tables[i].Columns)
                        {
                            dictRow.Add(col.ColumnName, dr[col].ToString()!);
                        }
                        lstRows.Add(dictRow);
                    }
                }
                lstSet[i] = lstRows;
            }
            return lstSet;
        }

        // ── Convert DataTable → strongly typed List<T> ────────────────────────
        public static List<T> ConvertToList<T>(DataTable dt)
        {
            var columnNames = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            var properties  = typeof(T).GetProperties();
            return dt.AsEnumerable().Select(row =>
            {
                var objT = Activator.CreateInstance<T>();
                foreach (var pro in properties)
                {
                    if (columnNames.Contains(pro.Name.ToLower()))
                    {
                        try { pro.SetValue(objT, row[pro.Name]); }
                        catch (Exception) { }
                    }
                }
                return objT;
            }).ToList();
        }
    }
}
