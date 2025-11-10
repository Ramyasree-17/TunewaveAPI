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
    public class LabelController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public LabelController(IConfiguration cfg)
        {
            _cfg = cfg;
            _connStr = _cfg.GetConnectionString("DefaultConnection")!;
        }

        // 🔹 Add new label (EnterpriseAdmin + valid API key)
        [HttpPost("add")]
        public IActionResult AddLabel([FromBody] Label req, [FromHeader(Name = "x-api-key")] string apiKey)
        {
            if (!ValidateApiKey(apiKey))
                return Unauthorized(new { message = "❌ Invalid API Key" });

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (role != "EnterpriseAdmin")
                return StatusCode(403, new { message = "❌ Only EnterpriseAdmin can create labels" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_AddLabel", conn) { CommandType = CommandType.StoredProcedure };

            cmd.Parameters.AddWithValue("@EnterpriseID", req.EnterpriseID);
            cmd.Parameters.AddWithValue("@LabelName", req.LabelName);
            cmd.Parameters.AddWithValue("@Domain", req.Domain ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PlanType", req.PlanType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RevenueShare", req.RevenueShare);
            cmd.Parameters.AddWithValue("@QCRequired", req.QCRequired);
            cmd.Parameters.AddWithValue("@CreatedBy", userId);

            conn.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { message = "✅ Label created successfully" });
        }

        // 🔹 Get all labels (API key required)
        [HttpGet("all")]
        public IActionResult GetLabels([FromHeader(Name = "x-api-key")] string apiKey)
        {
            if (!ValidateApiKey(apiKey))
                return Unauthorized(new { message = "❌ API Key is missing or invalid" });

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            using var conn = new SqlConnection(_connStr);
            SqlCommand cmd;

            if (role == "EnterpriseAdmin")
            {
                cmd = new SqlCommand("sp_GetLabelsByCreatedBy", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@CreatedBy", userId);
            }
            else
            {
                cmd = new SqlCommand("sp_GetAllLabels", conn) { CommandType = CommandType.StoredProcedure };
            }

            conn.Open();
            using var reader = cmd.ExecuteReader();
            var labels = new List<object>();

            while (reader.Read())
            {
                labels.Add(new
                {
                    LabelID = reader["LabelID"],
                    LabelName = reader["LabelName"],
                    Domain = reader["Domain"],
                    PlanType = reader["PlanType"],
                    RevenueShare = reader["RevenueShare"],
                    QCRequired = reader["QCRequired"],
                    EnterpriseID = reader["EnterpriseID"],
                    CreatedBy = reader["CreatedBy"],
                    CreatedAt = reader["CreatedAt"]
                });
            }

            return Ok(new { message = "✅ Labels fetched successfully", count = labels.Count, labels });
        }

        // 🔒 Helper: Validate API key
        private bool ValidateApiKey(string apiKey)
            => !string.IsNullOrEmpty(apiKey) && apiKey == _cfg["ApiKey"];
    }
}
