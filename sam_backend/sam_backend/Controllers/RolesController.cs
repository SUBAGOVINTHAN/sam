using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;
        private const string SESSION_ROLE = "role";

        public RolesController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        private bool IsAdmin() =>
    string.Equals(HttpContext.Session.GetString(SESSION_ROLE), "admin", StringComparison.OrdinalIgnoreCase);

        // ── GET /api/roles ────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult GetRoles()
        {
            if (!IsAdmin())
                return StatusCode(403, new { success = false, message = "Admin role required." });

            DB db = new DB(_settings);
            DataTable dt = db.GetDataTable(
                "SELECT r.role_name, r.created_at, COUNT(m.access_id) AS access_count " +
                "FROM roles r " +
                "LEFT JOIN map_master m ON m.role = r.role_name " +
                "GROUP BY r.role_name, r.created_at " +
                "ORDER BY r.role_name;");

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            var list = new List<object>();
            foreach (DataRow r in dt.Rows)
                list.Add(new
                {
                    roleName = r["role_name"].ToString(),
                    createdAt = r["created_at"].ToString(),
                    accessCount = Convert.ToInt32(r["access_count"])
                });

            return Ok(list);
        }

        // ── POST /api/roles ───────────────────────────────────────────────────
        [HttpPost]
        public IActionResult AddRole([FromBody] RoleNameRequest req)
        {
            if (!IsAdmin())
                return StatusCode(403, new { success = false, message = "Admin role required." });

            if (string.IsNullOrWhiteSpace(req.RoleName))
                return BadRequest(new { success = false, message = "Role name is required." });

            DB db = new DB(_settings);

            DataTable chk = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM roles WHERE LOWER(role_name) = LOWER(@rname);",
                new Dictionary<string, object> { { "@rname", req.RoleName.Trim() } });

            if (chk != null && chk.Rows.Count > 0 && Convert.ToInt32(chk.Rows[0]["cnt"]) > 0)
                return Conflict(new { success = false, message = $"Role '{req.RoleName}' already exists." });

            int rows = db.ExecQryParam(
                "INSERT INTO roles (role_name) VALUES (@rname);",
                new Dictionary<string, object> { { "@rname", req.RoleName.Trim() } });

            if (rows < 0 || db.DBErr != "")
                return StatusCode(500, new { success = false, message = "Failed to create role: " + db.DBErr });

            return Ok(new { success = true, message = "Role created.", roleName = req.RoleName.Trim() });
        }

        // ── PUT /api/roles/{roleName} ─────────────────────────────────────────
        [HttpPut("{roleName}")]
        public IActionResult UpdateRole(string roleName, [FromBody] RoleNameRequest req)
        {
            if (!IsAdmin())
                return StatusCode(403, new { success = false, message = "Admin role required." });

            if (string.IsNullOrWhiteSpace(req.RoleName))
                return BadRequest(new { success = false, message = "New role name is required." });

            DB db = new DB(_settings);

            // roles table (cascade updates map_master via ON UPDATE CASCADE)
            int rows = db.ExecQryParam(
                "UPDATE roles SET role_name = @newname WHERE role_name = @oldname;",
                new Dictionary<string, object>
                {
                    { "@newname", req.RoleName.Trim() },
                    { "@oldname", roleName }
                });

            if (rows == 0)
                return NotFound(new { success = false, message = "Role not found." });

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = "Failed to update role: " + db.DBErr });

            return Ok(new { success = true, message = "Role updated.", roleName = req.RoleName.Trim() });
        }

        // ── DELETE /api/roles/{roleName} ──────────────────────────────────────
        [HttpDelete("{roleName}")]
        public IActionResult DeleteRole(string roleName)
        {
            if (!IsAdmin())
                return StatusCode(403, new { success = false, message = "Admin role required." });

            DB db = new DB(_settings);

            // map_master rows removed via ON DELETE CASCADE on the FK
            int rows = db.ExecQryParam(
                "DELETE FROM roles WHERE role_name = @rname;",
                new Dictionary<string, object> { { "@rname", roleName } });

            if (rows == 0)
                return NotFound(new { success = false, message = "Role not found." });

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = "Failed to delete role: " + db.DBErr });

            return Ok(new { success = true, message = "Role deleted." });
        }

        // ── GET /api/roles/{roleName}/access ──────────────────────────────────
        [HttpGet("{roleName}/access")]
        public IActionResult GetRoleAccess(string roleName)
        {
            if (!IsAdmin())
                return StatusCode(403, new { success = false, message = "Admin role required." });

            DB db = new DB(_settings);
            DataTable dt = db.GetDataTableParam(
                "SELECT access_id FROM map_master WHERE role = @rname;",
                new Dictionary<string, object> { { "@rname", roleName } });

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            var ids = new List<int>();
            foreach (DataRow r in dt.Rows)
                ids.Add(Convert.ToInt32(r["access_id"]));

            return Ok(ids);
        }

        // ── POST /api/roles/{roleName}/access ─────────────────────────────────
        // Replaces the full access list for the role (delete-then-insert)
        [HttpPost("{roleName}/access")]
        public IActionResult SaveRoleAccess(string roleName, [FromBody] AccessSaveRequest req)
        {
            if (!IsAdmin())
                return StatusCode(403, new { success = false, message = "Admin role required." });

            DB db = new DB(_settings);

            // Verify role exists
            DataTable chk = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM roles WHERE role_name = @rname;",
                new Dictionary<string, object> { { "@rname", roleName } });

            if (chk == null || Convert.ToInt32(chk.Rows[0]["cnt"]) == 0)
                return NotFound(new { success = false, message = "Role not found." });

            // Remove existing assignments
            db.ExecQryParam(
                "DELETE FROM map_master WHERE role = @rname;",
                new Dictionary<string, object> { { "@rname", roleName } });

            // Insert selected access items
            foreach (var id in req.AccessIds)
            {
                db.ExecQryParam(
                    "INSERT INTO map_master (role, access_id) VALUES (@rname, @aid) ON CONFLICT DO NOTHING;",
                    new Dictionary<string, object>
                    {
                        { "@rname", roleName },
                        { "@aid",   id }
                    });

                if (db.DBErr != "")
                    return StatusCode(500, new { success = false, message = "Failed to save access: " + db.DBErr });
            }

            return Ok(new { success = true, message = "Access saved successfully.", count = req.AccessIds.Count });
        }
    }

    // ── GET /api/access ───────────────────────────────────────────────────────
    [ApiController]
    [Route("api/access")]
    public class AccessController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;

        public AccessController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
              if (!string.Equals(HttpContext.Session.GetString("role"), "admin", StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, new { success = false, message = "Admin role required." });

            DB db = new DB(_settings);
            DataTable dt = db.GetDataTable(
                "SELECT access_id, access, module FROM access_master ORDER BY module, access;");

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            var list = new List<object>();
            foreach (DataRow r in dt.Rows)
                list.Add(new
                {
                    accessId = Convert.ToInt32(r["access_id"]),
                    access = r["access"].ToString(),
                    module = r["module"].ToString()
                });

            return Ok(list);
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────
    public class RoleNameRequest
    {
        public string RoleName { get; set; } = "";
    }

    public class AccessSaveRequest
    {
        public List<int> AccessIds { get; set; } = new();
    }
}