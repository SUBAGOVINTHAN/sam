using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class TodayEventsController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;
        private const string SESSION_USERNAME = "username";
        private const string SESSION_ROLE = "role";

        public TodayEventsController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        [HttpGet("today")]
        public IActionResult GetToday()
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            string? role = HttpContext.Session.GetString(SESSION_ROLE);
            bool isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);

            try
            {
                DB db = new DB(_settings);

                string qry;
                Dictionary<string, object> p;

                if (isAdmin)
                {
                    // Admin sees ALL follow-ups from every user
                    qry = @"
                        SELECT
                            e.event_id,
                            e.lead_id,
                            lm.lead_code,
                            lm.customer_name,
                            lm.created_by,
                            lm.contact_person,
                            TO_CHAR(e.event_date,          'YYYY-MM-DD') AS event_date,
                            e.event_type,
                            e.outcome,
                            TO_CHAR(e.next_follow_up_date, 'YYYY-MM-DD') AS next_follow_up_date,
                            e.event_remarks,
                            e.event_status,
                            sm.status_name
                        FROM  event_master  e
                        JOIN  lead_master   lm ON e.lead_id   = lm.lead_id
                        LEFT JOIN status_master sm ON lm.status_id = sm.id
                        WHERE e.next_follow_up_date <= CURRENT_DATE
                          AND lm.status = 'No'
                        ORDER BY e.next_follow_up_date DESC, e.event_id DESC;";
                    p = new Dictionary<string, object>();
                }
                else
                {
                    // Non-admin sees only their own follow-ups
                    qry = @"
                        SELECT
                            e.event_id,
                            e.lead_id,
                            lm.lead_code,
                            lm.customer_name,
                            lm.created_by,
                            lm.contact_person,
                            TO_CHAR(e.event_date,          'YYYY-MM-DD') AS event_date,
                            e.event_type,
                            e.outcome,
                            TO_CHAR(e.next_follow_up_date, 'YYYY-MM-DD') AS next_follow_up_date,
                            e.event_remarks,
                            e.event_status,
                            sm.status_name
                        FROM  event_master  e
                        JOIN  lead_master   lm ON e.lead_id   = lm.lead_id
                        LEFT JOIN status_master sm ON lm.status_id = sm.id
                        WHERE e.next_follow_up_date <= CURRENT_DATE
                          AND lm.status = 'No'
                          AND (lm.contact_person = @user OR lm.created_by = @user)
                        ORDER BY e.next_follow_up_date DESC, e.event_id DESC;";
                    p = new Dictionary<string, object> { ["@user"] = user };
                }

                DataTable dt = db.GetDataTableParam(qry, p);

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