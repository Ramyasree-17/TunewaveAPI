using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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

        private string GenerateTicketTrackId()
        {
            // Prefix for ticket
            string prefix = "T";

            // Generate 2-digit random number
            var rnd = new Random();
            int randomNumber = rnd.Next(0, 100); // 0 - 99
            string randomNumberStr = randomNumber.ToString("D2"); // ensures 2 digits, e.g. 05, 12

            // Combine
            return $"{prefix}{randomNumberStr}";
        }

        private string GenerateEnterpriseId(string enterpriseName)
        {
            if (string.IsNullOrWhiteSpace(enterpriseName))
                throw new ArgumentException("Enterprise name cannot be empty");

            // First letter uppercase
            string firstLetter = enterpriseName.Substring(0, 1).ToUpper();

            // Generate 2-character alphanumeric
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var rnd = new Random();
            string nextTwo = new string(Enumerable.Range(0, 2)
                .Select(_ => chars[rnd.Next(chars.Length)]).ToArray());

            return firstLetter + nextTwo; // Example: S1F
        }






        // =====================================================================
        // 1️⃣ CREATE ENTERPRISE
        // =====================================================================
        [HttpPost]
        public IActionResult CreateEnterprise([FromBody] EnterpriseCreateV2 req)
        {
            string enterpriseIdStr = GenerateEnterpriseId(req.EnterpriseName);

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var createdBy = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (role != "SuperAdmin")
                return StatusCode(403, new { message = "Only SuperAdmin can create enterprises" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_CreateEnterprise_AutoOwner", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseName", req.EnterpriseName);
            cmd.Parameters.AddWithValue("@Domain", string.IsNullOrWhiteSpace(req.Domain) ? DBNull.Value : req.Domain);
            cmd.Parameters.AddWithValue("@RevenueShare", req.RevenueShare);
            cmd.Parameters.AddWithValue("@EnterPriseTrackId", enterpriseIdStr);
            cmd.Parameters.AddWithValue("@QCRequired", req.QCRequired);
            cmd.Parameters.AddWithValue("@OwnerEmail", req.OwnerEmail);
            cmd.Parameters.AddWithValue("@CreatedBy", createdBy);

            conn.Open();
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return BadRequest(new { message = "Enterprise creation failed" });

            return StatusCode(201, new
            {
                status = "success",
                message = "Enterprise created successfully",
                enterpriseId = reader["EnterpriseID"],
                ownerUserId = reader["OwnerUserID"],
                ownerStatus = reader["OwnerStatus"]
            });
        }

        // =====================================================================
        // 2️⃣ GET ALL ENTERPRISES
        // =====================================================================
        // =====================================================================
        // 2️⃣ GET ALL ENTERPRISES  (UPDATED FINAL VERSION)
        // =====================================================================
        [HttpGet]
        public IActionResult GetEnterprises([FromQuery] string? status, [FromQuery] string? search)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            using var conn = new SqlConnection(_connStr);
            SqlCommand cmd;

            // -----------------------------------------------------------------
            // 🔹 EnterpriseAdmin → Get ONLY the enterprise he owns
            // -----------------------------------------------------------------
            if (role == "EnterpriseAdmin")
            {
                cmd = new SqlCommand("sp_GetEnterpriseByOwner", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@OwnerId", userId);
            }
            // -----------------------------------------------------------------
            // 🔹 SuperAdmin → Get ALL enterprises with filters
            // -----------------------------------------------------------------
            else
            {
                cmd = new SqlCommand("sp_GetAllEnterprises_Filter", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@Status",
                    string.IsNullOrWhiteSpace(status) ? DBNull.Value : status);

                cmd.Parameters.AddWithValue("@Search",
                    string.IsNullOrWhiteSpace(search) ? DBNull.Value : search);
            }

            conn.Open();
            using var reader = cmd.ExecuteReader();

            var list = new List<object>();

            while (reader.Read())
            {
                list.Add(new
                {
                    enterpriseId = reader["EnterpriseID"],
                    enterpriseName = reader["EnterpriseName"],
                    domain = reader["Domain"],
                    revenueShare = reader["RevenueShare"],
                    qcRequired = reader["QCRequired"],
                    status = reader["Status"],
                    owner = new
                    {
                        userId = reader["OwnerID"],
                        fullName = reader["OwnerName"],
                        email = reader["OwnerEmail"]
                    },
                    createdBy = reader["CreatedByName"],
                    createdAt = reader["CreatedAt"]
                });
            }

            return Ok(list);
        }

        // =====================================================================
        // 3️⃣ UPDATE ENTERPRISE DETAILS
        // =====================================================================
        [HttpPut("{enterpriseId}")]
        public IActionResult UpdateEnterprise(int enterpriseId, [FromBody] EnterpriseUpdateRequest req)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (role != "SuperAdmin" && role != "EnterpriseAdmin")
                return StatusCode(403, new { message = "Unauthorized" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_UpdateEnterprise", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
            cmd.Parameters.AddWithValue("@Domain", string.IsNullOrWhiteSpace(req.Domain) ? DBNull.Value : req.Domain);
            cmd.Parameters.AddWithValue("@RevenueShare", req.RevenueShare);
            cmd.Parameters.AddWithValue("@QCRequired", req.QCRequired);
            cmd.Parameters.AddWithValue("@UpdatedBy", userId);

            conn.Open();
            var rows = cmd.ExecuteNonQuery();

            if (rows == 0)
                return NotFound(new { message = "Enterprise not found" });

            return Ok(new
            {
                status = "updated",
                message = "Enterprise details updated successfully"
            });
        }

        // =====================================================================
        // 4️⃣ UPDATE ENTERPRISE STATUS
        // =====================================================================
        [HttpPost("status")]
        public IActionResult UpdateStatus([FromQuery] int id, [FromQuery] string status)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (role != "SuperAdmin")
                return StatusCode(403, new { message = "Only SuperAdmin can update enterprise status" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_UpdateEnterpriseStatus", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // ✅ Match SP parameters exactly
            cmd.Parameters.AddWithValue("@EnterpriseId", id);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@UpdatedBy", userId);

            conn.Open();
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return NotFound(new { message = "Enterprise not found" });

            return Ok(new
            {
                enterpriseId = reader["EnterpriseID"],
                enterpriseName = reader["EnterpriseName"],
                status = reader["Status"],
                message = "Enterprise status updated successfully"
            });
        }
    }

    // =====================================================================
    // DTOs
    // =====================================================================
    public class EnterpriseCreateV2
    {
        public string EnterpriseName { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public decimal RevenueShare { get; set; }
        public bool QCRequired { get; set; }
        public string OwnerEmail { get; set; } = string.Empty;
    }

    public class EnterpriseUpdateRequest
    {
        public string? Domain { get; set; }
        public decimal RevenueShare { get; set; }
        public bool QCRequired { get; set; }
    }
}
