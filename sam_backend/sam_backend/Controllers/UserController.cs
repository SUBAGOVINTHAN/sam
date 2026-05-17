using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;
        private const string SESSION_ROLE = "role";

        public UsersController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        // ✅ FIXED — case-insensitive
        private bool IsAdmin() =>
            string.Equals(
                HttpContext.Session.GetString(SESSION_ROLE),
                "admin",
                StringComparison.OrdinalIgnoreCase
            );

        private bool IsLoggedIn() =>
            !string.IsNullOrEmpty(HttpContext.Session.GetString("username"));

        // ── language column is now a plain comma-separated string ────────────
        private static object RowToUser(DataRow r) => new
        {
            id = r["id"].ToString(),
            username = r["username"].ToString(),
            email = r["email"].ToString(),
            role = r["role"].ToString(),
            activestatus = r["activestatus"].ToString(),
            language = r["language"] == DBNull.Value ? "" : r["language"].ToString(),
            createdBy = r["created_by"].ToString(),
            createdAt = r["created_timestamp"].ToString(),
        };

        private static object Ok(bool success, string title, string message, object? data = null) =>
            new { success, title, message, data };

        private static object Fail(string title, string message) =>
            new { success = false, title, message };

        // ── GET /api/users ───────────────────────────────────────────────────
        // FIXED: Any logged-in user can read the users list
        // (needed for the Contact Person dropdown in Add/Edit Lead modal)
        [HttpGet]
        public IActionResult GetAll()
        {
            if (!IsLoggedIn())
                return StatusCode(401, Fail("Unauthorized", "Please log in."));

            DB db = new DB(_settings);
            DataTable dt = db.GetDataTable(
                "SELECT id, username, email, role, activestatus, language, created_by, created_timestamp " +
                "FROM users ORDER BY created_timestamp DESC;");

            if (db.DBErr != "")
                return StatusCode(500, Fail("Database Error", "Failed to load users: " + db.DBErr));

            var list = new List<object>();
            foreach (DataRow row in dt.Rows) list.Add(RowToUser(row));
            return base.Ok(list);
        }

        // ── GET /api/users/{id} ──────────────────────────────────────────────
        // FIXED: Any logged-in user can read a single user record
        [HttpGet("{id}")]
        public IActionResult GetById(string id)
        {
            if (!IsLoggedIn())
                return StatusCode(401, Fail("Unauthorized", "Please log in."));

            DB db = new DB(_settings);
            DataTable dt = db.GetDataTableParam(
                "SELECT id, username, email, role, activestatus, language, created_by, created_timestamp " +
                "FROM users WHERE id = CAST(@id AS INTEGER);",
                new Dictionary<string, object> { { "@id", id } });

            if (db.DBErr != "" || dt == null || dt.Rows.Count == 0)
                return NotFound(Fail("Not Found", "User not found."));

            return base.Ok(RowToUser(dt.Rows[0]));
        }

        // ── POST /api/users ──────────────────────────────────────────────────
        [HttpPost]
        public IActionResult Create([FromBody] CreateUserRequest request)
        {
            if (!IsAdmin())
                return StatusCode(403, Fail("Access Denied", "Admin role required to create users."));

            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(Fail("Validation Error", "Username is required."));

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(Fail("Validation Error", "Email address is required."));

            string rawPassword = string.IsNullOrWhiteSpace(request.Password)
                ? request.Username.Trim()
                : request.Password;

            DB db = new DB(_settings);

            // Duplicate username check
            DataTable dtCheck = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM users WHERE username = @uname;",
                new Dictionary<string, object> { { "@uname", request.Username.Trim() } });

            if (dtCheck != null && dtCheck.Rows.Count > 0 &&
                Convert.ToInt32(dtCheck.Rows[0]["cnt"]) > 0)
                return Conflict(Fail("Username Taken",
                    $"Username '{request.Username}' already exists. Please choose another."));

            // Duplicate email check
            DataTable dtEmailCheck = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM users WHERE email = @email;",
                new Dictionary<string, object> { { "@email", request.Email.Trim() } });

            if (dtEmailCheck != null && dtEmailCheck.Rows.Count > 0 &&
                Convert.ToInt32(dtEmailCheck.Rows[0]["cnt"]) > 0)
                return Conflict(Fail("Email Taken",
                    $"Email '{request.Email}' is already registered to another user."));

            string createdBy = HttpContext.Session.GetString("username") ?? "system";
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: 11);

            // Join selected languages into a comma-separated string
            string languageStr = string.Join(",", request.Languages
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l)));

            int rows = db.ExecQryParam(
                "INSERT INTO users (username, password, role, email, activestatus, language, created_by) " +
                "VALUES (@uname, @pwd, @role, @email, @status, @lang, @createdby);",
                new Dictionary<string, object>
                {
                    { "@uname",     request.Username.Trim() },
                    { "@pwd",       hashedPassword           },
                    { "@role",      request.Role             },
                    { "@email",     request.Email.Trim()     },
                    { "@status",    request.ActiveStatus     },
                    { "@lang",      languageStr              },
                    { "@createdby", createdBy                },
                });

            if (rows < 0 || db.DBErr != "")
                return StatusCode(500, Fail("Create Failed", "Failed to create user: " + db.DBErr));

            return base.Ok(Ok(true,
                "User Created",
                $"@{request.Username.Trim()} has been added successfully. Default password is their username.",
                new { username = request.Username.Trim() }));
        }

        // ── PUT /api/users/{id} ──────────────────────────────────────────────
        [HttpPut("{id}")]
        public IActionResult Update(string id, [FromBody] UpdateUserRequest request)
        {
            if (!IsAdmin())
                return StatusCode(403, Fail("Access Denied", "Admin role required to edit users."));

            // Join selected languages into a comma-separated string
            string languageStr = string.Join(",", request.Languages
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l)));

            DB db = new DB(_settings);

            int rows = db.ExecQryParam(
                "UPDATE users SET email = @email, role = @role, activestatus = @status, language = @lang " +
                "WHERE id = CAST(@id AS INTEGER);",
                new Dictionary<string, object>
                {
                    { "@email",  request.Email        },
                    { "@role",   request.Role         },
                    { "@status", request.ActiveStatus },
                    { "@lang",   languageStr          },
                    { "@id",     id                   },
                });

            if (rows == 0)
                return NotFound(Fail("User Not Found", "No user was found with the given ID."));

            if (rows < 0 || db.DBErr != "")
                return StatusCode(500, Fail("Update Failed", "Failed to update user: " + db.DBErr));

            return base.Ok(Ok(true, "User Updated", "User details have been updated successfully."));
        }

        // ── POST /api/users/{id}/reset-password ──────────────────────────────
        [HttpPost("{id}/reset-password")]
        public IActionResult ResetPassword(string id, [FromBody] ResetPasswordRequest request)
        {
            if (!IsAdmin())
                return StatusCode(403, Fail("Access Denied", "Admin role required to reset passwords."));

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(Fail("Validation Error", "New password is required."));

            string hashed = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 11);

            DB db = new DB(_settings);
            int rows = db.ExecQryParam(
                "UPDATE users SET password = @pwd WHERE id = CAST(@id AS INTEGER);",
                new Dictionary<string, object>
                {
                    { "@pwd", hashed },
                    { "@id",  id     },
                });

            if (rows == 0)
                return NotFound(Fail("User Not Found", "No user was found with the given ID."));

            if (rows < 0 || db.DBErr != "")
                return StatusCode(500, Fail("Reset Failed", "Failed to reset password: " + db.DBErr));

            return base.Ok(Ok(true, "Password Reset", "Password has been reset successfully."));
        }

        // ── POST /api/users/{id}/toggle-block ────────────────────────────────
        [HttpPost("{id}/toggle-block")]
        public IActionResult ToggleBlock(string id)
        {
            if (!IsAdmin())
                return StatusCode(403, Fail("Access Denied", "Admin role required to block/unblock users."));

            DB db = new DB(_settings);

            DataTable dt = db.GetDataTableParam(
                "SELECT activestatus, username FROM users WHERE id = CAST(@id AS INTEGER);",
                new Dictionary<string, object> { { "@id", id } });

            if (db.DBErr != "" || dt == null || dt.Rows.Count == 0)
                return NotFound(Fail("User Not Found", "No user was found with the given ID."));

            string currentStatus = dt.Rows[0]["activestatus"].ToString()!;
            string username = dt.Rows[0]["username"].ToString()!;
            string newStatus = currentStatus == "active" ? "blocked" : "active";

            int rows = db.ExecQryParam(
                "UPDATE users SET activestatus = @status WHERE id = CAST(@id AS INTEGER);",
                new Dictionary<string, object>
                {
                    { "@status", newStatus },
                    { "@id",     id        },
                });

            if (rows < 0 || db.DBErr != "")
                return StatusCode(500, Fail("Action Failed", "Failed to update user status: " + db.DBErr));

            string actionLabel = newStatus == "blocked" ? "blocked" : "unblocked";
            return base.Ok(Ok(true,
                newStatus == "blocked" ? "User Blocked" : "User Unblocked",
                $"@{username} has been {actionLabel} successfully.",
                new { newStatus }));
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────
    public class CreateUserRequest
    {
        public string Username { get; set; } = "";
        public string? Password { get; set; }
        public string Email { get; set; } = "";
        public string Role { get; set; } = "user";
        public string ActiveStatus { get; set; } = "active";
        public List<string> Languages { get; set; } = new();
    }

    public class UpdateUserRequest
    {
        public string Email { get; set; } = "";
        public string Role { get; set; } = "user";
        public string ActiveStatus { get; set; } = "active";
        public List<string> Languages { get; set; } = new();
    }

    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = "";
    }
}