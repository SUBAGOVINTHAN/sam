using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/lead/{leadId}/event")]
    public class EventController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;
        private const string SESSION_USERNAME = "username";

        public EventController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        // ── GET /api/lead/{leadId}/event ───────────────────────────────────────
        [HttpGet]
        public IActionResult GetAll(int leadId)
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            DB db = new DB(_settings);
            string qry = $@"
                SELECT
                    event_id,
                    lead_id,
                    TO_CHAR(event_date,          'YYYY-MM-DD')         AS event_date,
                    event_type,
                    outcome,
                    TO_CHAR(next_follow_up_date, 'YYYY-MM-DD')         AS next_follow_up_date,
                    event_remarks,
                    event_status,
                    handled_by,
                    created_by,
                    TO_CHAR(created_timestamp,   'YYYY-MM-DD HH24:MI') AS created_timestamp
                FROM event_master
                WHERE lead_id = {leadId}
                ORDER BY event_id ASC;";

            DataTable dt = db.GetDataTable(qry);
            if (db.DBErr != "") return StatusCode(500, new { success = false, message = db.DBErr });

            var list = new List<Dictionary<string, object?>>();
            foreach (DataRow row in dt.Rows)
            {
                var d = new Dictionary<string, object?>();
                foreach (DataColumn col in dt.Columns)
                    d[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                list.Add(d);
            }
            return Ok(list);
        }

        // ── POST /api/lead/{leadId}/event ──────────────────────────────────────
        [HttpPost]
        public IActionResult Create(int leadId, [FromBody] Dictionary<string, object?> body)
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            if (!body.ContainsKey("eventType") || string.IsNullOrWhiteSpace(body["eventType"]?.ToString()))
                return BadRequest(new { success = false, message = "Event type is required." });

            DB db = new DB(_settings);

            // ── NEW: Check latest event_status for this lead ──────────────────────────
            DataTable latestDt = db.GetDataTableParam(@"
                SELECT event_status
                FROM   event_master
                WHERE  lead_id = @lead_id
                ORDER  BY event_id DESC
                LIMIT  1;",
                new Dictionary<string, object> { { "@lead_id", leadId } });

            if (!string.IsNullOrEmpty(db.DBErr))
                return StatusCode(500, new { success = false, message = db.DBErr });

            if (latestDt != null && latestDt.Rows.Count > 0)
            {
                // There is a previous event — check its status
                string? latestStatus = latestDt.Rows[0]["event_status"] == DBNull.Value
                    ? null
                    : latestDt.Rows[0]["event_status"]?.ToString();

                if (!string.Equals(latestStatus, "Closed", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Cannot add a new event. The previous event must be closed before adding another."
                    });
                }
            }
            // If latestDt.Rows.Count == 0 → no events yet → allow (first event)
            // ─────────────────────────────────────────────────────────────────────────

            string qry = @"
        INSERT INTO event_master
            (lead_id, event_date, event_type, outcome,
             next_follow_up_date, event_remarks, created_by)
        VALUES
            (@lead_id,
             @event_date::date,
             @event_type,
             @outcome,
             @next_follow_up::date,
             @remarks,
             @created_by)
        RETURNING event_id;";

            var p = new Dictionary<string, object>
    {
        { "@lead_id",        leadId },
        { "@event_date",     NullOrDate(body, "eventDate") },
        { "@event_type",     body["eventType"]?.ToString() ?? "" },
        { "@outcome",        body.GetValueOrDefault("outcome")?.ToString() ?? "" },
        { "@next_follow_up", NullOrDate(body, "nextFollowUpDate") },
        { "@remarks",        body.GetValueOrDefault("eventRemarks")?.ToString() ?? "" },
        { "@created_by",     user },
    };

            DataTable dt = db.GetDataTableParam(qry, p);
            if (db.DBErr != "") return StatusCode(500, new { success = false, message = db.DBErr });

            string outcome = body.GetValueOrDefault("outcome")?.ToString() ?? "";
            UpdateLeadStatusByOutcome(leadId, outcome);

            int newId = Convert.ToInt32(dt.Rows[0]["event_id"]);


            return Ok(new { success = true, eventId = newId, message = "Event added." });
        }

        // ── PUT /api/lead/{leadId}/event/{eventId} ─────────────────────────────
        [HttpPut("{eventId}")]
        public IActionResult Update(int leadId, int eventId, [FromBody] Dictionary<string, object?> body)
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            DB db = new DB(_settings);

            string qry = @"
                UPDATE event_master SET
                    event_date          = @event_date::date,
                    event_type          = @event_type,
                    outcome             = @outcome,
                    next_follow_up_date = @next_follow_up::date,
                    event_remarks       = @remarks
                WHERE event_id = @event_id AND lead_id = @lead_id;";

            var p = new Dictionary<string, object>
            {
                { "@event_id",       eventId },
                { "@lead_id",        leadId },
                { "@event_date",     NullOrDate(body, "eventDate") },
                { "@event_type",     body.GetValueOrDefault("eventType")?.ToString() ?? "" },
                { "@outcome",        body.GetValueOrDefault("outcome")?.ToString() ?? "" },
                { "@next_follow_up", NullOrDate(body, "nextFollowUpDate") },
                { "@remarks",        body.GetValueOrDefault("eventRemarks")?.ToString() ?? "" },
            };

            int rows = db.ExecQryParam(qry, p);
            if (db.DBErr != "") return StatusCode(500, new { success = false, message = db.DBErr });

            // Auto-update lead status when outcome is Booked or Closed
            string outcome = body.GetValueOrDefault("outcome")?.ToString() ?? "";
            UpdateLeadStatusByOutcome(leadId, outcome);

            return Ok(new { success = true, message = "Event updated." });
        }

        // ── DELETE /api/lead/{leadId}/event/{eventId} ──────────────────────────
        [HttpDelete("{eventId}")]
        public IActionResult Delete(int leadId, int eventId)
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            DB db = new DB(_settings);
            int rows = db.ExecQryParam(
                "DELETE FROM event_master WHERE event_id = @eid AND lead_id = @lid;",
                new Dictionary<string, object> { { "@eid", eventId }, { "@lid", leadId } });

            if (db.DBErr != "") return StatusCode(500, new { success = false, message = db.DBErr });
            return Ok(new { success = true, message = "Event deleted." });
        }

        // ── PATCH /api/lead/{leadId}/event/{eventId}/status ───────────────────
        // Updates only the event_status field on a specific event.
        // Body: { "eventStatus": "Closed" }  — pass null / "" to clear it.
        [HttpPatch("{eventId}/status")]
        public IActionResult UpdateStatus(int leadId, int eventId, [FromBody] Dictionary<string, object?> body)
        {
            string? user = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(user)) return Unauthorized();

            string? eventStatus = body.GetValueOrDefault("eventStatus")?.ToString();

            DB db = new DB(_settings);
            bool isClosing = string.Equals(eventStatus, "Closed", StringComparison.OrdinalIgnoreCase);
            bool isClearing = string.IsNullOrWhiteSpace(eventStatus);

            var p = new Dictionary<string, object>
            {
                { "@event_status", string.IsNullOrWhiteSpace(eventStatus) ? (object)DBNull.Value : eventStatus.Trim() },

                { "@handled_by",   isClosing  ? (object)user         : DBNull.Value         },
                { "@set_handled",  isClosing || isClearing ? 1 : 0 }, // whether to touch handled_by
                { "@event_id",     eventId },
                { "@lead_id",      leadId  }
            };
            int rows = db.ExecQryParam(
                @"UPDATE event_master
               SET event_status = @event_status,
              handled_by   = CASE WHEN @set_handled = 1 THEN @handled_by ELSE handled_by END
              WHERE event_id = @event_id
              AND lead_id  = @lead_id;",
                p);

            if (!string.IsNullOrEmpty(db.DBErr))
                return StatusCode(500, new { success = false, message = db.DBErr });

            if (rows == 0)
                return NotFound(new { success = false, message = "Event not found." });

            Console.WriteLine(
                $"[EventStatus] lead_id={leadId} event_id={eventId} " +
                $"event_status='{eventStatus}' rows_affected={rows}");

            return Ok(new { success = true, message = "Event status updated." });
        }

        // ── Auto-update lead status when outcome is Booked or Closed ──────────
        private void UpdateLeadStatusByOutcome(int leadId, string outcome)
        {
            var outcomeToStatus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Booked", "Booked" },
                { "Closed", "Closed" },
            };

            if (!outcomeToStatus.TryGetValue(outcome, out string? targetStatusName))
                return;

            DB db = new DB(_settings);

            string qry = @"
                UPDATE lead_master
                SET    status_id = (
                           SELECT id
                           FROM   status_master
                           WHERE  LOWER(status_name) = LOWER(@status_name)
                           LIMIT  1
                       )
                WHERE  lead_id = @lead_id
                AND    EXISTS (
                           SELECT 1
                           FROM   status_master
                           WHERE  LOWER(status_name) = LOWER(@status_name)
                       );";

            int rowsAffected = db.ExecQryParam(qry, new Dictionary<string, object>
            {
                { "@lead_id",     leadId },
                { "@status_name", targetStatusName },
            });

            Console.WriteLine(
                $"[AutoStatus] lead_id={leadId} outcome='{outcome}' " +
                $"target='{targetStatusName}' rows_affected={rowsAffected} db_err='{db.DBErr}'");
        }

        // ── Helper: return the date string for ::date cast, or DBNull if empty ─
        private static object NullOrDate(Dictionary<string, object?> body, string key)
        {
            if (!body.TryGetValue(key, out var v) || v == null || v.ToString() == "")
                return DBNull.Value;
            return v.ToString()!;
        }
    }
}