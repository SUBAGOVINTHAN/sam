using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeadsController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;
        private const string SESSION_USERNAME = "username";
        private const string SESSION_ROLE = "role";

        // ── Base SELECT ───────────────────────────────────────────────────────
        private const string SELECT_LEAD = @"
            SELECT
                lm.lead_id,
                lm.lead_code,
                TO_CHAR(lm.lead_date, 'YYYY-MM-DD') AS lead_date,
                lm.customer_name,
                lm.language_id,
                lang.language_name,
                lm.contact_no,
                lm.location,
                lm.status_id,
                sm.status_name,
                lm.product_id,
                pm.product_name,
                lm.state,
                lm.moc,
                lm.lead_remarks,
                lm.contact_person,
                lm.created_by,
                lm.created_timestamp,
                lm.status,
                (
                    SELECT event_status
                    FROM   event_master
                    WHERE  lead_id = lm.lead_id
                    ORDER  BY event_id DESC
                    LIMIT  1
                ) AS event_status,
                (
                    SELECT outcome
                    FROM   event_master
                    WHERE  lead_id = lm.lead_id
                    ORDER  BY event_id DESC
                    LIMIT  1
                ) AS latest_outcome
            FROM lead_master lm
            LEFT JOIN language_master lang ON lm.language_id = lang.id
            LEFT JOIN status_master   sm   ON lm.status_id   = sm.id
            LEFT JOIN product_master  pm   ON lm.product_id  = pm.id
        ";

        public LeadsController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        // ── Helper: is current session user an admin? ─────────────────────────
        private bool IsAdmin()
        {
            string? role = HttpContext.Session.GetString(SESSION_ROLE);
            return string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        }

        // ── DEBUG: GET /api/leads/debug ───────────────────────────────────────
        [HttpGet("debug")]
        public IActionResult Debug()
        {
            try
            {
                DB db = new DB(_settings);

                DataTable t1 = db.GetDataTableParam(
                    "SELECT lead_id, lead_code, customer_name, status FROM lead_master LIMIT 3;",
                    new Dictionary<string, object>());

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { step = "1_bare_lead_master", error = db.DBErr });

                DataTable t2 = db.GetDataTableParam(
                    SELECT_LEAD + " WHERE lm.status = 'No' LIMIT 3;",
                    new Dictionary<string, object>());

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { step = "2_full_join_query", error = db.DBErr });

                return Ok(new
                {
                    step = "all_ok",
                    bare_rows_found = t1?.Rows.Count ?? 0,
                    joined_rows_found = t2?.Rows.Count ?? 0,
                    sample = t2 != null && t2.Rows.Count > 0
                                ? RowToDict(t2.Rows[0], t2)
                                : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    step = "exception",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        // ── GET /api/leads ────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult GetAll()
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            try
            {
                DB db = new DB(_settings);

                string sql;
                Dictionary<string, object> p;

                if (IsAdmin())
                {
                    sql = SELECT_LEAD + @"
                   WHERE lm.status = 'No'
                    ORDER BY
                        CASE WHEN lm.contact_person = @user THEN 0 ELSE 1 END ASC,
                        lm.lead_id DESC;";
                    p = new Dictionary<string, object> { { "@user", user } };
                }
                else
                {
                     sql = SELECT_LEAD + @"
                    WHERE lm.status = 'No'
                      AND lm.contact_person = @user
                    ORDER BY lm.lead_id DESC;";
                    p = new Dictionary<string, object> { { "@user", user } };
                }

                DataTable dt = db.GetDataTableParam(sql, p);

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { success = false, message = db.DBErr });

                return Ok(TableToList(dt));
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

        // ── GET /api/leads/{id} ───────────────────────────────────────────────
        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            try
            {
                DB db = new DB(_settings);

                string sql;
                Dictionary<string, object> p;

                if (IsAdmin())
                {
                    sql = SELECT_LEAD + " WHERE lm.lead_id = @id AND lm.status = 'No';";
                    p = new Dictionary<string, object> { { "@id", id } };
                }
                else
                {
                    sql = SELECT_LEAD + " WHERE lm.lead_id = @id AND lm.status = 'No' AND lm.contact_person= @user;";
                    p = new Dictionary<string, object> { { "@id", id }, { "@user", user } };
                }

                DataTable dt = db.GetDataTableParam(sql, p);

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { success = false, message = db.DBErr });

                if (dt == null || dt.Rows.Count == 0)
                    return NotFound(new { success = false, message = "Lead not found." });

                return Ok(RowToDict(dt.Rows[0], dt));
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

        // ── POST /api/leads ───────────────────────────────────────────────────
        [HttpPost]
        public IActionResult Create([FromBody] CreateLeadRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CustomerName))
                return BadRequest(new { success = false, message = "customer_name is required." });

            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            try
            {
                DB db = new DB(_settings);

                string qry = @"
                    INSERT INTO lead_master
                        (lead_date, customer_name, language_id, contact_no, location,
                         status_id, product_id, state, moc, lead_remarks, contact_person,
                         created_by, status)
                    VALUES
                        (@lead_date, @customer_name, @language_id, @contact_no, @location,
                         @status_id, @product_id, @state, @moc, @lead_remarks, @contact_person,
                         @created_by, 'No')
                    RETURNING lead_id, lead_code;";

                var p = new Dictionary<string, object>
                {
                    { "@lead_date",      ParseDate(req.LeadDate) },
                    { "@customer_name",  req.CustomerName.Trim() },
                    { "@language_id",    (object?)req.LanguageId    ?? DBNull.Value },
                    { "@contact_no",     (object?)req.ContactNo     ?? DBNull.Value },
                    { "@location",       (object?)req.Location      ?? DBNull.Value },
                    { "@status_id",      DBNull.Value },
                    { "@product_id",     (object?)req.ProductId     ?? DBNull.Value },
                    { "@state",          (object?)req.State         ?? DBNull.Value },
                    { "@moc",            (object?)req.Moc           ?? DBNull.Value },
                    { "@lead_remarks",   (object?)req.LeadRemarks   ?? DBNull.Value },
                    { "@contact_person", (object?)req.ContactPerson ?? DBNull.Value },
                    { "@created_by",     user }
                };

                DataTable dt = db.GetDataTableParam(qry, p);

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { success = false, message = db.DBErr });

                if (dt == null || dt.Rows.Count == 0)
                    return StatusCode(500, new { success = false, message = "Insert did not return a new lead id." });

                int newId = Convert.ToInt32(dt.Rows[0]["lead_id"]);
                string newCode = dt.Rows[0]["lead_code"]?.ToString() ?? "";

                return CreatedAtAction(nameof(GetById), new { id = newId },
                    new { success = true, lead_id = newId, lead_code = newCode });
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

        // ── PUT /api/leads/{id} ───────────────────────────────────────────────
        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] UpdateLeadRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CustomerName))
                return BadRequest(new { success = false, message = "customer_name is required." });

            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            try
            {
                DB db = new DB(_settings);

                string qry;
                var p = new Dictionary<string, object>
                {
                    { "@lead_id",        id },
                    { "@lead_date",      ParseDate(req.LeadDate) },
                    { "@customer_name",  req.CustomerName.Trim() },
                    { "@language_id",    (object?)req.LanguageId    ?? DBNull.Value },
                    { "@contact_no",     (object?)req.ContactNo     ?? DBNull.Value },
                    { "@location",       (object?)req.Location      ?? DBNull.Value },
                    { "@product_id",     (object?)req.ProductId     ?? DBNull.Value },
                    { "@state",          (object?)req.State         ?? DBNull.Value },
                    { "@moc",            (object?)req.Moc           ?? DBNull.Value },
                    { "@lead_remarks",   (object?)req.LeadRemarks   ?? DBNull.Value },
                    { "@contact_person", (object?)req.ContactPerson ?? DBNull.Value }
                };

                if (IsAdmin())
                {
                    qry = @"
                        UPDATE lead_master SET
                            lead_date      = @lead_date,
                            customer_name  = @customer_name,
                            language_id    = @language_id,
                            contact_no     = @contact_no,
                            location       = @location,
                            product_id     = @product_id,
                            state          = @state,
                            moc            = @moc,
                            lead_remarks   = @lead_remarks,
                            contact_person = @contact_person
                        WHERE lead_id = @lead_id AND status = 'No';";
                }
                else
                {
                    qry = @"
                        UPDATE lead_master SET
                            lead_date      = @lead_date,
                            customer_name  = @customer_name,
                            language_id    = @language_id,
                            contact_no     = @contact_no,
                            location       = @location,
                            product_id     = @product_id,
                            state          = @state,
                            moc            = @moc,
                            lead_remarks   = @lead_remarks,
                            contact_person = @contact_person
                        WHERE lead_id = @lead_id AND status = 'No' AND contact_person = @user;";

                    p.Add("@user", user);
                }

                int rows = db.ExecQryParam(qry, p);

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { success = false, message = db.DBErr });

                if (rows == 0)
                    return NotFound(new { success = false, message = "Lead not found." });

                return Ok(new { success = true, message = "Lead updated." });
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

        // ── DELETE /api/leads/{id} ────────────────────────────────────────────
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            try
            {
                DB db = new DB(_settings);

                string checkSql;
                Dictionary<string, object> checkP;

                if (IsAdmin())
                {
                    checkSql = "SELECT lead_id FROM lead_master WHERE lead_id = @id AND status = 'No';";
                    checkP = new Dictionary<string, object> { { "@id", id } };
                }
                else
                {
                    checkSql = "SELECT lead_id FROM lead_master WHERE lead_id = @id AND status = 'No' AND contact_person = @user;";
                    checkP = new Dictionary<string, object> { { "@id", id }, { "@user", user } };
                }

                DataTable check = db.GetDataTableParam(checkSql, checkP);

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { success = false, message = db.DBErr });

                if (check == null || check.Rows.Count == 0)
                    return NotFound(new { success = false, message = "Lead not found." });

                // Hard-delete all events for this lead
                db.ExecQryParam(
                    "DELETE FROM event_master WHERE lead_id = @id;",
                    new Dictionary<string, object> { { "@id", id } });

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { success = false, message = "Failed to delete events: " + db.DBErr });

                // Soft-delete the lead
                int rows = db.ExecQryParam(
                    "UPDATE lead_master SET status = 'Yes' WHERE lead_id = @id AND status = 'No';",
                    new Dictionary<string, object> { { "@id", id } });

                if (!string.IsNullOrEmpty(db.DBErr))
                    return StatusCode(500, new { success = false, message = db.DBErr });

                if (rows == 0)
                    return NotFound(new { success = false, message = "Lead not found." });

                return Ok(new { success = true, message = "Lead deleted." });
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

        // ── Helpers ───────────────────────────────────────────────────────────
        private static DateTime ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DateTime.Today;
            return DateTime.TryParse(s, out var d) ? d.Date : DateTime.Today;
        }

        private static List<Dictionary<string, object?>> TableToList(DataTable? dt)
        {
            var list = new List<Dictionary<string, object?>>();
            if (dt == null) return list;
            foreach (DataRow row in dt.Rows)
                list.Add(RowToDict(row, dt));
            return list;
        }

        private static Dictionary<string, object?> RowToDict(DataRow row, DataTable dt)
        {
            var dict = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns)
                dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
            return dict;
        }
    }
}