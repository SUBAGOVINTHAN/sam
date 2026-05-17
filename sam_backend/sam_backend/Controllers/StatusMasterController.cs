// sam/backend/Controllers/StatusMasterController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusMasterController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;

        public StatusMasterController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        private bool IsLoggedIn() =>
            !string.IsNullOrEmpty(HttpContext.Session.GetString("username"));

        // ── GET /api/statusmaster ──────────────────────────────────────────
        [HttpGet]
        public IActionResult GetAll()
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            DB db = new DB(_settings);
            DataTable dt = db.GetDataTable(
                "SELECT id, status_name FROM status_master WHERE delete_status = 'No' ORDER BY id;");

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            var list = new List<object>();
            foreach (DataRow r in dt.Rows)
                list.Add(new { id = Convert.ToInt32(r["id"]), statusName = r["status_name"].ToString() });

            return Ok(list);
        }

        // ── POST /api/statusmaster ─────────────────────────────────────────
        [HttpPost]
        public IActionResult Create([FromBody] StatusRequest req)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(req.StatusName))
                return BadRequest(new { success = false, message = "Status name is required." });

            DB db = new DB(_settings);

            // Duplicate check only among active records
            DataTable dtCheck = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM status_master WHERE LOWER(status_name) = LOWER(@name) AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.StatusName.Trim() } });

            if (dtCheck != null && Convert.ToInt32(dtCheck.Rows[0]["cnt"]) > 0)
                return Conflict(new { success = false, message = $"'{req.StatusName}' already exists." });

            int result = db.ExecQryParam(
                "INSERT INTO status_master (status_name, delete_status) VALUES (@name, 'No');",
                new Dictionary<string, object> { { "@name", req.StatusName.Trim() } });

            if (result < 0 || db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            return Ok(new { success = true, message = "Status created successfully." });
        }

        // ── PUT /api/statusmaster/{id} ─────────────────────────────────────
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] StatusRequest req)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(req.StatusName))
                return BadRequest(new { success = false, message = "Status name is required." });

            DB db = new DB(_settings);

            // Duplicate check excluding self, only among active records
            DataTable dtCheck = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM status_master WHERE LOWER(status_name) = LOWER(@name) AND id <> @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.StatusName.Trim() }, { "@id", id } });

            if (dtCheck != null && Convert.ToInt32(dtCheck.Rows[0]["cnt"]) > 0)
                return Conflict(new { success = false, message = $"'{req.StatusName}' already exists." });

            int result = db.ExecQryParam(
                "UPDATE status_master SET status_name = @name WHERE id = @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.StatusName.Trim() }, { "@id", id } });

            if (result <= 0 || db.DBErr != "")
                return db.DBErr != ""
                    ? StatusCode(500, new { success = false, message = db.DBErr })
                    : NotFound(new { success = false, message = "Status not found." });

            return Ok(new { success = true, message = "Status updated successfully." });
        }

        // ── DELETE /api/statusmaster/{id} ──────────────────────────────────
        // Soft-delete: set delete_status = 'Yes' instead of removing the row
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            DB db = new DB(_settings);

            int result = db.ExecQryParam(
                "UPDATE status_master SET delete_status = 'Yes' WHERE id = @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@id", id } });

            if (result <= 0 || db.DBErr != "")
                return db.DBErr != ""
                    ? StatusCode(500, new { success = false, message = db.DBErr })
                    : NotFound(new { success = false, message = "Status not found." });

            return Ok(new { success = true, message = "Status deleted successfully." });
        }
    }
}