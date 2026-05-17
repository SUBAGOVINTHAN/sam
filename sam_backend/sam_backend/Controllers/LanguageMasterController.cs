// sam/backend/Controllers/LanguageMasterController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LanguageMasterController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;

        public LanguageMasterController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        private bool IsLoggedIn() =>
            !string.IsNullOrEmpty(HttpContext.Session.GetString("username"));

        // ── GET /api/languagemaster ────────────────────────────────────────
        [HttpGet]
        public IActionResult GetAll()
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            DB db = new DB(_settings);
            DataTable dt = db.GetDataTable(
                "SELECT id, language_name FROM language_master WHERE delete_status = 'No' ORDER BY id;");

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            var list = new List<object>();
            foreach (DataRow r in dt.Rows)
                list.Add(new { id = Convert.ToInt32(r["id"]), languageName = r["language_name"].ToString() });

            return Ok(list);
        }

        // ── POST /api/languagemaster ───────────────────────────────────────
        [HttpPost]
        public IActionResult Create([FromBody] LanguageRequest req)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(req.LanguageName))
                return BadRequest(new { success = false, message = "Language name is required." });

            DB db = new DB(_settings);

            // Duplicate check only among active records
            DataTable dtCheck = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM language_master WHERE LOWER(language_name) = LOWER(@name) AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.LanguageName.Trim() } });

            if (dtCheck != null && Convert.ToInt32(dtCheck.Rows[0]["cnt"]) > 0)
                return Conflict(new { success = false, message = $"'{req.LanguageName}' already exists." });

            int result = db.ExecQryParam(
                "INSERT INTO language_master (language_name, delete_status) VALUES (@name, 'No');",
                new Dictionary<string, object> { { "@name", req.LanguageName.Trim() } });

            if (result < 0 || db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            return Ok(new { success = true, message = "Language created successfully." });
        }

        // ── PUT /api/languagemaster/{id} ───────────────────────────────────
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] LanguageRequest req)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(req.LanguageName))
                return BadRequest(new { success = false, message = "Language name is required." });

            DB db = new DB(_settings);

            // Duplicate check excluding self, only among active records
            DataTable dtCheck = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM language_master WHERE LOWER(language_name) = LOWER(@name) AND id <> @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.LanguageName.Trim() }, { "@id", id } });

            if (dtCheck != null && Convert.ToInt32(dtCheck.Rows[0]["cnt"]) > 0)
                return Conflict(new { success = false, message = $"'{req.LanguageName}' already exists." });

            int result = db.ExecQryParam(
                "UPDATE language_master SET language_name = @name WHERE id = @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.LanguageName.Trim() }, { "@id", id } });

            if (result <= 0 || db.DBErr != "")
                return db.DBErr != ""
                    ? StatusCode(500, new { success = false, message = db.DBErr })
                    : NotFound(new { success = false, message = "Language not found." });

            return Ok(new { success = true, message = "Language updated successfully." });
        }

        // ── DELETE /api/languagemaster/{id} ───────────────────────────────
        // Soft-delete: set delete_status = 'Yes' instead of removing the row
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            DB db = new DB(_settings);

            int result = db.ExecQryParam(
                "UPDATE language_master SET delete_status = 'Yes' WHERE id = @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@id", id } });

            if (result <= 0 || db.DBErr != "")
                return db.DBErr != ""
                    ? StatusCode(500, new { success = false, message = db.DBErr })
                    : NotFound(new { success = false, message = "Language not found." });

            return Ok(new { success = true, message = "Language deleted successfully." });
        }
    }
}