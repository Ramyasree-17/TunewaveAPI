using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPI.Models;

namespace TunewaveAPI.Controllers
{
    [ApiController]
    [Route("api/labels")]
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

        // GET /api/labels
        [HttpGet]
        public IActionResult GetLabels([FromQuery] string? status, [FromQuery] int? page = 1, [FromQuery] int? limit = 50)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var enterpriseIdClaim = User.FindFirst("EnterpriseId")?.Value;
            int? enterpriseId = !string.IsNullOrEmpty(enterpriseIdClaim) ? int.Parse(enterpriseIdClaim) : null;

            using var conn = new SqlConnection(_connStr);
            conn.Open();

            var query = "SELECT LabelID, LabelName, Domain, PlanType, QCRequired, RevenueShare, Status, CreatedAt " +
                        "FROM Labels WHERE 1=1 ";

            if (role == "EnterpriseAdmin" && enterpriseId.HasValue)
            {
                query += "AND EnterpriseID = @EnterpriseID ";
            }

            if (!string.IsNullOrEmpty(status))
            {
                query += "AND Status = @Status ";
            }

            query += "ORDER BY CreatedAt DESC " +
                     "OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

            using var cmd = new SqlCommand(query, conn);

            if (role == "EnterpriseAdmin" && enterpriseId.HasValue)
                cmd.Parameters.AddWithValue("@EnterpriseID", enterpriseId.Value);

            cmd.Parameters.AddWithValue("@Offset", ((page ?? 1) - 1) * (limit ?? 50));
            cmd.Parameters.AddWithValue("@Limit", limit ?? 50);

            if (!string.IsNullOrEmpty(status))
                cmd.Parameters.AddWithValue("@Status", status);

            var labels = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                labels.Add(new
                {
                    labelId = reader["LabelID"],
                    labelName = reader["LabelName"],
                    domain = reader["Domain"],
                    planType = reader["PlanType"],
                    qcRequired = reader["QCRequired"],
                    revenueShare = reader["RevenueShare"],
                    status = reader["Status"],
                    createdAt = reader["CreatedAt"]
                });
            }

            return Ok(new
            {
                total = labels.Count,
                labels
            });
        }

        // POST /api/labels
        [HttpPost]
        [Authorize(Roles = "EnterpriseAdmin")]
        public IActionResult CreateLabel([FromBody] LabelCreateRequest model)
        {
            if (model == null)
                return BadRequest("Invalid request body.");

            int createdBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_CreateLabel", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseId", model.EnterpriseID);
            cmd.Parameters.AddWithValue("@LabelName", model.LabelName);
            cmd.Parameters.AddWithValue("@PlanType", (object?)model.PlanType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@QCRequired", model.QCRequired);
            cmd.Parameters.AddWithValue("@Domain", (object?)model.Domain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RevenueShare", model.RevenueShare);
            cmd.Parameters.AddWithValue("@CreatedByUserId", createdBy);

            conn.Open();
            int labelId = Convert.ToInt32(cmd.ExecuteScalar());

            return StatusCode(201, new
            {
                status = "success",
                labelId,
                message = "Label created successfully",
                revenueShare = model.RevenueShare
            });
        }

        // PUT /api/labels/{labelId}
        [HttpPut("{labelId}")]
        public IActionResult UpdateLabel(int labelId, [FromBody] LabelUpdateRequest model)
        {
            int updatedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_UpdateLabel", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", labelId);
            cmd.Parameters.AddWithValue("@Domain", (object?)model.Domain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@QCRequired", (object?)model.QCRequired ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RevenueShare", (object?)model.RevenueShare ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)model.Status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UpdatedByUserId", updatedBy);

            conn.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { status = "updated", message = "Label details updated successfully" });
        }

        // POST /api/labels/transfer
        [HttpPost("transfer")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult TransferLabel([FromBody] LabelTransferRequest model)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_TransferLabel", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", model.LabelId);
            cmd.Parameters.AddWithValue("@OldEnterpriseId", model.OldEnterpriseId);
            cmd.Parameters.AddWithValue("@NewEnterpriseId", model.NewEnterpriseId);
            cmd.Parameters.AddWithValue("@Reason", model.Reason);
            cmd.Parameters.AddWithValue("@TransferredByUserId", userId);

            conn.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { status = "success", message = "Label transferred successfully to new enterprise" });
        }

        // POST /api/labels/transfer-request
        [HttpPost("transfer-request")]
        [Authorize(Roles = "EnterpriseAdmin")]
        public IActionResult RequestTransfer([FromBody] LabelTransferRequest model)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_RequestLabelTransfer", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", model.LabelId);
            cmd.Parameters.AddWithValue("@TargetEnterpriseId", model.NewEnterpriseId);
            cmd.Parameters.AddWithValue("@Reason", model.Reason);
            cmd.Parameters.AddWithValue("@RequestedByUserId", userId);

            conn.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { status = "pending", message = "Transfer request submitted for SuperAdmin review" });
        }

        // POST /api/labels/status
        [HttpPost("status")]
        public IActionResult ChangeStatus([FromBody] LabelStatusRequest model)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_ChangeLabelStatus", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", model.LabelId);
            cmd.Parameters.AddWithValue("@Status", model.Status);
            cmd.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
            cmd.Parameters.AddWithValue("@ChangedByUserId", userId);

            conn.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { status = "success", message = $"Label status changed to {model.Status}" });
        }
    }
}
