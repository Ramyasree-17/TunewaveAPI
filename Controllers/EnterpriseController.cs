using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPI.Models;

namespace TunewaveAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EnterpriseController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public EnterpriseController(IConfiguration cfg)
        {
            _cfg = cfg;
            _connStr = _cfg.GetConnectionString("DefaultConnection")!;
        }

        // ✅ Add Enterprise (SuperAdmin + EnterpriseAdmin)
        [HttpPost("add")]
        public IActionResult AddEnterprise([FromBody] Enterprise req, [FromHeader(Name = "x-api-key")] string apiKey)
        {
            if (!ValidateApiKey(apiKey))
                return Unauthorized(new { message = "❌ Invalid API Key" });

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // ✅ Allow both SuperAdmin and EnterpriseAdmin
            if (role != "SuperAdmin" && role != "EnterpriseAdmin")
                return StatusCode(403, new { message = "❌ Only SuperAdmin or EnterpriseAdmin can create enterprises" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_AddEnterprise", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseName", req.EnterpriseName);
            cmd.Parameters.AddWithValue("@Domain", req.Domain ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RevenueShare", req.RevenueShare);
            cmd.Parameters.AddWithValue("@QCRequired", req.QCRequired);
            cmd.Parameters.AddWithValue("@CreatedBy", userId);

            conn.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { message = "✅ Enterprise created successfully" });
        }

        // ✅ Get All Enterprises (SuperAdmin sees all, EnterpriseAdmin sees their own)
        [HttpGet("all")]
        public IActionResult GetAllEnterprises([FromHeader(Name = "x-api-key")] string apiKey)
        {
            if (!ValidateApiKey(apiKey))
                return Unauthorized(new { message = "❌ Invalid API Key" });

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            using var conn = new SqlConnection(_connStr);
            SqlCommand cmd;

            if (role == "EnterpriseAdmin")
            {
                cmd = new SqlCommand("sp_GetEnterpriseByCreatedBy", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@CreatedBy", userId);
            }
            else
            {
                cmd = new SqlCommand("sp_GetAllEnterprises", conn) { CommandType = CommandType.StoredProcedure };
            }

            conn.Open();
            using var reader = cmd.ExecuteReader();
            var enterprises = new List<object>();

            while (reader.Read())
            {
                enterprises.Add(new
                {
                    EnterpriseID = reader["EnterpriseID"],
                    EnterpriseName = reader["EnterpriseName"],
                    Domain = reader["Domain"],
                    RevenueShare = reader["RevenueShare"],
                    QCRequired = reader["QCRequired"],
                    CreatedBy = reader["CreatedBy"],
                    CreatedAt = reader["CreatedAt"],
                    UpdatedAt = reader["UpdatedAt"]
                });
            }

            return Ok(new { message = "✅ Enterprises fetched successfully", count = enterprises.Count, enterprises });
        }

        private bool ValidateApiKey(string apiKey)
            => !string.IsNullOrEmpty(apiKey) && apiKey == _cfg["ApiKey"];
    }
}