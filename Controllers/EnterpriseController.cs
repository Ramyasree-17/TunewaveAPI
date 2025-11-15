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

        // =======================================================
        // ✅ Add Enterprise with Owner (SuperAdmin + SuperAdminUser only)
        // =======================================================
        [HttpPost("add")]
        public IActionResult AddEnterprise([FromBody] EnterpriseCreateRequest req)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (role != "SuperAdmin" && role != "SuperAdminUser")
                return StatusCode(403, new { message = "❌ Only SuperAdmin or SuperAdminUser can create enterprises" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_CreateEnterpriseWithOwner", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Match stored procedure parameters
            cmd.Parameters.AddWithValue("@EnterpriseName", req.EnterpriseName);
            cmd.Parameters.AddWithValue("@Domain", req.Domain ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RevenueShare", req.RevenueShare);
            cmd.Parameters.AddWithValue("@QCRequired", req.QCRequired);
            cmd.Parameters.AddWithValue("@CreatedBy", userId);
            cmd.Parameters.AddWithValue("@OwnerFullName", req.OwnerFullName);
            cmd.Parameters.AddWithValue("@OwnerEmail", req.OwnerEmail);
            cmd.Parameters.AddWithValue("@OwnerPasswordHash", BCrypt.Net.BCrypt.HashPassword(req.OwnerPasswordHash));

            conn.Open();
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                var result = new
                {
                    EnterpriseID = reader["EnterpriseID"],
                    EnterpriseName = reader["EnterpriseName"],
                    Domain = reader["Domain"],
                    RevenueShare = reader["RevenueShare"],
                    QCRequired = reader["QCRequired"],
                    Status = reader["Status"],
                    CreatedBy = reader["CreatedBy"],
                    //OwnedBy = reader["OwnedBy"],
                    CreatedAt = reader["CreatedAt"],
                    UpdatedAt = reader["UpdatedAt"],
                    OwnerID = reader["OwnerID"],
                    OwnerName = reader["OwnerName"]?.ToString(),
                    OwnerEmail = reader["OwnerEmail"]?.ToString(),
                    OwnedBy = reader["OwnedBy"]
                };
                return Ok(new { message = "✅ Enterprise created successfully", enterprise = result });
            }

            return BadRequest(new { message = "❌ Enterprise creation failed" });
        }

        // =======================================================
        // ✅ Get All Enterprises
        // =======================================================
        [HttpGet("all")]
        public IActionResult GetAllEnterprises()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            using var conn = new SqlConnection(_connStr);
            SqlCommand cmd;

            if (role == "EnterpriseAdmin")
            {
                cmd = new SqlCommand("sp_GetEnterpriseByCreatedBy", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@CreatedBy", userId);
            }
            else
            {
                cmd = new SqlCommand(@"
                    SELECT e.EnterpriseID,
                           e.EnterpriseName,
                           e.Domain,
                           e.RevenueShare,
                           e.QCRequired,
                           e.Status,
                           u.UserID AS OwnerID,
                           u.FullName AS OwnerName,
                           u.Email AS OwnerEmail,
                           e.CreatedBy,
                           e.OwnedBy,
                           e.CreatedAt,
                           e.UpdatedAt
                    FROM Enterprises e
                    LEFT JOIN Users u ON e.OwnedBy = u.UserID
                    ORDER BY e.EnterpriseID
                ", conn);
                cmd.CommandType = CommandType.Text;
            }

            conn.Open();
            using var reader = cmd.ExecuteReader();
            var list = new List<object>();

            while (reader.Read())
            {
                list.Add(new
                {
                    EnterpriseID = reader["EnterpriseID"],
                    EnterpriseName = reader["EnterpriseName"],
                    Domain = reader["Domain"],
                    RevenueShare = reader["RevenueShare"],
                    QCRequired = reader["QCRequired"],
                    Status = reader["Status"],
                    OwnerID = reader["OwnerID"],
                    OwnerName = reader["OwnerName"]?.ToString(),
                    OwnerEmail = reader["OwnerEmail"]?.ToString(),
                    CreatedBy = reader["CreatedBy"],
                    OwnedBy = reader["OwnedBy"],
                    CreatedAt = reader["CreatedAt"],
                    UpdatedAt = reader["UpdatedAt"]
                });
            }

            return Ok(new
            {
                message = "✅ Enterprises fetched successfully",
                count = list.Count,
                enterprises = list
            });
        }
    }

    // ================================
    // Request model for creating enterprise
    // ================================
    public class EnterpriseCreateRequest
    {
        public string EnterpriseName { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public decimal RevenueShare { get; set; }
        public bool QCRequired { get; set; }
        public string OwnerFullName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public string OwnerPasswordHash { get; set; } = string.Empty;
    }
}
