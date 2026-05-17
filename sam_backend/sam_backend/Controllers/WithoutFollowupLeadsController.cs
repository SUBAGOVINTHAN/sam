using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/leads/without-followup")]
    public class WithoutFollowupLeadsController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;
        private const string SESSION_USERNAME = "username";
        private const string SESSION_ROLE = "role";

        public WithoutFollowupLeadsController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        [HttpGet]
        public IActionResult GetWithoutFollowup()
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            string? role = HttpContext.Session.GetString(SESSION_ROLE);
            bool isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);

            try
            {
                DB db = new DB(_settings);

                string baseQuery = @"
                    SELECT
                        lm.lead_id,
                        lm.lead_code,
                        TO_CHAR(lm.lead_date, 'YYYY-MM-DD')        AS lead_date,
                        lm.customer_name,
                        lm.contact_no,
                        lm.product_id,
                        pm.product_name,
                        lm.status_id,
                        sm.status_name,
                        lm.state,
                        lm.moc,
                        lm.created_by,
                        lm.contact_person,
                        latest.outcome                              AS latest_outcome,
                        TO_CHAR(latest.event_date, 'YYYY-MM-DD')   AS latest_event_date
                    FROM lead_master lm
                    LEFT JOIN status_master  sm ON lm.status_id  = sm.id
                    LEFT JOIN product_master pm ON lm.product_id = pm.id
                    JOIN LATERAL (
                        SELECT outcome, event_date
                        FROM   event_master
                        WHERE  lead_id = lm.lead_id
                        ORDER  BY event_id DESC
                        LIMIT  1
                    ) latest ON TRUE
                    WHERE lm.status = 'No'
                      AND latest.outcome IN ('Busy', 'Not Interested', 'NA')
                ";

                string sql;
                Dictionary<string, object> p;

                if (isAdmin)
                {
                    sql = baseQuery + " ORDER BY lm.lead_id DESC;";
                    p = new Dictionary<string, object>();
                }
                else
                {
                    sql = baseQuery + @"
                      AND (lm.created_by = @user OR lm.contact_person = @user)
                    ORDER BY lm.lead_id DESC;";
                    p = new Dictionary<string, object> { { "@user", user } };
                }

                DataTable dt = db.GetDataTableParam(sql, p);

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { success = false, message = db.DBErr });

                var list = new List<Dictionary<string, object?>>();
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        var d = new Dictionary<string, object?>();
                        foreach (DataColumn col in dt.Columns)
                            d[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                        list.Add(d);
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }
    }
}