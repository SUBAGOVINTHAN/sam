using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;

        // Session keys
        private const string SESSION_USERNAME = "username";
        private const string SESSION_ROLE = "role";
        private const string SESSION_EMAIL = "email";

        public AuthController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        // ── POST /api/auth/login ───────────────────────────────────────────────
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Username and password are required."
                });
            }

            DB db = new DB(_settings);

            // Fetch user by username using parameterized query
            string qry = "SELECT id, username, password, role, email, activestatus " +
                         "FROM users WHERE username = @uname LIMIT 1;";

            DataTable dt = db.GetDataTableParam(qry, new Dictionary<string, object>
            {
                { "@uname", request.Username.Trim() }
            });

            if (db.DBErr != "")
            {
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Database error: " + db.DBErr
                });
            }

            if (dt == null || dt.Rows.Count == 0)
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid username or password."
                });
            }

            DataRow row = dt.Rows[0];
            string dbHash = row["password"].ToString()!;
            string status = row["activestatus"].ToString()!;
            string username = row["username"].ToString()!;
            string role = row["role"].ToString()!;
            string email = row["email"].ToString()!;

            if (status != "active")
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = "Account is inactive. Please contact the administrator."
                });
            }

            // Verify BCrypt hash
            bool passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, dbHash);
            if (!passwordValid)
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid username or password."
                });
            }

            // Store in session
            HttpContext.Session.SetString(SESSION_USERNAME, username);
            HttpContext.Session.SetString(SESSION_ROLE, role);
            HttpContext.Session.SetString(SESSION_EMAIL, email);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Login successful.",
                Username = username,
                Role = role,
                Email = email
            });
        }

        // ── POST /api/auth/logout ──────────────────────────────────────────────
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { success = true, message = "Logged out successfully." });
        }

        // ── GET /api/auth/me ───────────────────────────────────────────────────
        [HttpGet("me")]
        public IActionResult Me()
        {
            string? username = HttpContext.Session.GetString(SESSION_USERNAME);

            if (string.IsNullOrEmpty(username))
            {
                return Ok(new MeResponse { IsAuthenticated = false });
            }

            return Ok(new MeResponse
            {
                IsAuthenticated = true,
                Username = username,
                Role = HttpContext.Session.GetString(SESSION_ROLE),
                Email = HttpContext.Session.GetString(SESSION_EMAIL)
            });
        }

        // ── GET /api/auth/my-permissions ──────────────────────────────────────
        // Returns the list of permission names granted to the logged-in user's role.
        // Any authenticated user can call this (not admin-only).
        [HttpGet("my-permissions")]
        public IActionResult MyPermissions()
        {
            string? role = HttpContext.Session.GetString(SESSION_ROLE);

            // Unauthenticated or no role → return empty list (no crash)
            if (string.IsNullOrEmpty(role))
                return Ok(new { permissions = new List<string>() });

            DB db = new DB(_settings);
            DataTable dt = db.GetDataTableParam(
                "SELECT am.access " +
                "FROM access_master am " +
                "INNER JOIN map_master mm ON mm.access_id = am.access_id " +
                "WHERE LOWER(mm.role) = LOWER(@role) " +
                "ORDER BY am.module, am.access;",
                new Dictionary<string, object> { { "@role", role } });

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = "DB error: " + db.DBErr });

            var perms = new List<string>();
            foreach (DataRow r in dt.Rows)
                perms.Add(r["access"].ToString()!);

            return Ok(new { permissions = perms });
        }


        // ── POST /api/auth/change-password ────────────────────────────────────
        // Allows the currently logged-in user to change their own password.
        [HttpPost("change-password")]
        public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
        {
            string? username = HttpContext.Session.GetString(SESSION_USERNAME);
            if (string.IsNullOrEmpty(username))
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { success = false, message = "All fields are required." });

            if (request.NewPassword.Length < 5)
                return BadRequest(new { success = false, message = "New password must be at least 5 characters." });

            DB db = new DB(_settings);

            // Fetch stored hash for this user
            DataTable dt = db.GetDataTableParam(
                "SELECT id, password FROM users WHERE username = @uname LIMIT 1;",
                new Dictionary<string, object> { { "@uname", username } });

            if (db.DBErr != "" || dt == null || dt.Rows.Count == 0)
                return StatusCode(500, new { success = false, message = "User not found." });

            string storedHash = dt.Rows[0]["password"].ToString()!;

            // Verify current password
            bool valid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, storedHash);
            if (!valid)
                return BadRequest(new { success = false, message = "Current password is incorrect." });

            // Hash & save new password
            string newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 11);
            int userId = Convert.ToInt32(dt.Rows[0]["id"]);

            int rows = db.ExecQryParam(
                "UPDATE users SET password = @pwd WHERE id = @id;",
                new Dictionary<string, object> { { "@pwd", newHash }, { "@id", userId } });

            if (rows <= 0 || db.DBErr != "")
                return StatusCode(500, new { success = false, message = "Failed to update password." });

            return Ok(new { success = true, message = "Password changed successfully." });
        }

        // ── POST /api/auth/seed ───────────────────────────────────────────────
        // ⚠️  DEV ONLY — remove or protect this endpoint before production!
        [HttpPost("seed")]
        public IActionResult Seed()
        {
            DB db = new DB(_settings);

            // Check if admin already exists
            string checkQry = "SELECT COUNT(*) FROM users WHERE username = 'admin';";
            object result = db.ExecScalar(checkQry);

            if (db.DBErr != "")
            {
                return StatusCode(500, new { success = false, message = "Database error: " + db.DBErr });
            }

            int count = Convert.ToInt32(result);
            if (count > 0)
            {
                return Ok(new { success = false, message = "Admin user already exists." });
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword("Admin@123", workFactor: 11);

            string insertQry = "INSERT INTO users (username, password, role, email, activestatus, created_by) " +
                               "VALUES (@uname, @pwd, @role, @email, @status, @createdby);";

            int rows = db.ExecQryParam(insertQry, new Dictionary<string, object>
            {
                { "@uname",     "admin" },
                { "@pwd",       hashedPassword },
                { "@role",      "admin" },
                { "@email",     "admin@sammachinery.com" },
                { "@status",    "active" },
                { "@createdby", "system" }
            });

            if (rows < 0 || db.DBErr != "")
            {
                return StatusCode(500, new { success = false, message = "Failed to seed admin: " + db.DBErr });
            }

            return Ok(new
            {
                success = true,
                message = "Admin user seeded successfully.",
                username = "admin",
                password = "Admin@123"
            });
        }

        // ── POST /api/auth/create-user ────────────────────────────────────────
        [HttpPost("create-user")]
        public IActionResult CreateUser([FromBody] CreateUserRequest request)
        {
            // Must be logged in as admin
            string? sessionRole = HttpContext.Session.GetString(SESSION_ROLE);
           
            if (!string.Equals(sessionRole, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { success = false, message = "Access denied. Admin role required." });
            }

            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Username and password are required." });
            }

            DB db = new DB(_settings);

            // Check if username already exists
            string checkQry = "SELECT COUNT(*) FROM users WHERE username = @uname;";
            object existing = db.ExecScalar(checkQry);

            // Note: parameterized scalar — use GetDataTableParam workaround
            DataTable dtCheck = db.GetDataTableParam(
                "SELECT COUNT(*) as cnt FROM users WHERE username = @uname;",
                new Dictionary<string, object> { { "@uname", request.Username.Trim() } });

            if (dtCheck != null && dtCheck.Rows.Count > 0)
            {
                int existCount = Convert.ToInt32(dtCheck.Rows[0]["cnt"]);
                if (existCount > 0)
                {
                    return Conflict(new { success = false, message = $"Username '{request.Username}' already exists." });
                }
            }

            string createdBy = HttpContext.Session.GetString(SESSION_USERNAME) ?? "system";
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11);

            string insertQry = "INSERT INTO users (username, password, role, email, activestatus, created_by) " +
                               "VALUES (@uname, @pwd, @role, @email, @status, @createdby);";

            int rows = db.ExecQryParam(insertQry, new Dictionary<string, object>
            {
                { "@uname",     request.Username.Trim() },
                { "@pwd",       hashedPassword },
                { "@role",      request.Role },
                { "@email",     request.Email },
                { "@status",    request.ActiveStatus },
                { "@createdby", createdBy }
            });

            if (rows < 0 || db.DBErr != "")
            {
                return StatusCode(500, new { success = false, message = "Failed to create user: " + db.DBErr });
            }

            return Ok(new
            {
                success = true,
                message = "User created successfully.",
                username = request.Username.Trim(),
                role = request.Role,
                email = request.Email
            });
        }
    }
}