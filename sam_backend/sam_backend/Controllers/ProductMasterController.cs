// sam/backend/Controllers/ProductMasterController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamErpBackend.Data;
using SamErpBackend.Models;
using System.Data;

namespace SamErpBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductMasterController : ControllerBase
    {
        private readonly IOptions<AppSettingsModel> _settings;

        public ProductMasterController(IOptions<AppSettingsModel> settings)
        {
            _settings = settings;
        }

        private bool IsLoggedIn() =>
            !string.IsNullOrEmpty(HttpContext.Session.GetString("username"));

        // ── GET /api/productmaster ─────────────────────────────────────────
        [HttpGet]
        public IActionResult GetAll()
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            DB db = new DB(_settings);
            // Only return active (non-deleted) products
            DataTable dt = db.GetDataTable(
                "SELECT id, product_name FROM product_master WHERE delete_status = 'No' ORDER BY id;");

            if (db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            var list = new List<object>();
            foreach (DataRow r in dt.Rows)
                list.Add(new { id = Convert.ToInt32(r["id"]), productName = r["product_name"].ToString() });

            return Ok(list);
        }

        // ── POST /api/productmaster ────────────────────────────────────────
        [HttpPost]
        public IActionResult Create([FromBody] ProductRequest req)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(req.ProductName))
                return BadRequest(new { success = false, message = "Product name is required." });

            DB db = new DB(_settings);

            // Duplicate check only among active products
            DataTable dtCheck = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM product_master WHERE LOWER(product_name) = LOWER(@name) AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.ProductName.Trim() } });

            if (dtCheck != null && Convert.ToInt32(dtCheck.Rows[0]["cnt"]) > 0)
                return Conflict(new { success = false, message = $"'{req.ProductName}' already exists." });

            // Insert with status = 'No' (active)
            int result = db.ExecQryParam(
                "INSERT INTO product_master (product_name, delete_status) VALUES (@name, 'No');",
                new Dictionary<string, object> { { "@name", req.ProductName.Trim() } });

            if (result < 0 || db.DBErr != "")
                return StatusCode(500, new { success = false, message = db.DBErr });

            return Ok(new { success = true, message = "Product created successfully." });
        }

        // ── PUT /api/productmaster/{id} ────────────────────────────────────
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] ProductRequest req)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(req.ProductName))
                return BadRequest(new { success = false, message = "Product name is required." });

            DB db = new DB(_settings);

            // Duplicate check excluding self, only among active products
            DataTable dtCheck = db.GetDataTableParam(
                "SELECT COUNT(*) AS cnt FROM product_master WHERE LOWER(product_name) = LOWER(@name) AND id <> @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.ProductName.Trim() }, { "@id", id } });

            if (dtCheck != null && Convert.ToInt32(dtCheck.Rows[0]["cnt"]) > 0)
                return Conflict(new { success = false, message = $"'{req.ProductName}' already exists." });

            int result = db.ExecQryParam(
                "UPDATE product_master SET product_name = @name WHERE id = @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@name", req.ProductName.Trim() }, { "@id", id } });

            if (result <= 0 || db.DBErr != "")
                return db.DBErr != ""
                    ? StatusCode(500, new { success = false, message = db.DBErr })
                    : NotFound(new { success = false, message = "Product not found." });

            return Ok(new { success = true, message = "Product updated successfully." });
        }

        // ── DELETE /api/productmaster/{id} ─────────────────────────────────
        // Soft-delete: set status = 'Yes' instead of removing the row
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            if (!IsLoggedIn())
                return StatusCode(401, new { success = false, message = "Not authenticated." });

            DB db = new DB(_settings);

            int result = db.ExecQryParam(
                "UPDATE product_master SET delete_status = 'Yes' WHERE id = @id AND delete_status = 'No';",
                new Dictionary<string, object> { { "@id", id } });

            if (result <= 0 || db.DBErr != "")
                return db.DBErr != ""
                    ? StatusCode(500, new { success = false, message = db.DBErr })
                    : NotFound(new { success = false, message = "Product not found." });

            return Ok(new { success = true, message = "Product deleted successfully." });
        }
    }
}