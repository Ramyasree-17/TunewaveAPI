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
    public class ReleaseController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public ReleaseController(IConfiguration cfg)
        {
            _cfg = cfg;
            _connStr = _cfg.GetConnectionString("DefaultConnection")!;
        }

        // 🔹 Add new release (Artist or LabelAdmin)
        [HttpPost("add")]
        public IActionResult AddRelease([FromBody] Release req, [FromHeader(Name = "x-api-key")] string apiKey)
        {
            if (!ValidateApiKey(apiKey))
                return Unauthorized(new { message = "❌ Invalid API Key" });

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (role != "Artist" && role != "LabelAdmin")
                return StatusCode(403, new { message = "❌ Only Artists or LabelAdmins can add releases" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_AddRelease", conn) { CommandType = CommandType.StoredProcedure };

            cmd.Parameters.AddWithValue("@LabelID", req.LabelID);
            cmd.Parameters.AddWithValue("@ArtistID", req.ArtistID);
            cmd.Parameters.AddWithValue("@ReleaseTitle", req.ReleaseTitle);
            cmd.Parameters.AddWithValue("@ReleaseDate", req.ReleaseDate);
            cmd.Parameters.AddWithValue("@UPC", req.UPC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", req.Status ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", userId);

            conn.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { message = "✅ Release added successfully" });
        }

        // 🔹 Get all releases (API key required)
        [HttpGet("all")]
        public IActionResult GetReleases([FromHeader(Name = "x-api-key")] string apiKey)
        {
            if (!ValidateApiKey(apiKey))
                return Unauthorized(new { message = "❌ API Key is missing or invalid" });

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            using var conn = new SqlConnection(_connStr);
            SqlCommand cmd;

            if (role == "Artist")
            {
                cmd = new SqlCommand("sp_GetReleasesByArtist", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@CreatedBy", userId);
            }
            else if (role == "LabelAdmin")
            {
                cmd = new SqlCommand("sp_GetReleasesByLabel", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@CreatedBy", userId);
            }
            else
            {
                cmd = new SqlCommand("sp_GetAllReleases", conn) { CommandType = CommandType.StoredProcedure };
            }

            conn.Open();
            using var reader = cmd.ExecuteReader();
            var releases = new List<object>();

            while (reader.Read())
            {
                releases.Add(new
                {
                    ReleaseID = reader["ReleaseID"],
                    ReleaseTitle = reader["ReleaseTitle"],
                    ReleaseDate = reader["ReleaseDate"],
                    UPC = reader["UPC"],
                    Status = reader["Status"],
                    LabelID = reader["LabelID"],
                    ArtistID = reader["ArtistID"],
                    CreatedBy = reader["CreatedBy"],
                    CreatedAt = reader["CreatedAt"]
                });
            }

            return Ok(new { message = "✅ Releases fetched successfully", count = releases.Count, releases });
        }

        // 🔒 Helper method for API key validation
        private bool ValidateApiKey(string apiKey)
            => !string.IsNullOrEmpty(apiKey) && apiKey == _cfg["ApiKey"];
    }
}