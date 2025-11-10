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
    public class ArtistController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public ArtistController(IConfiguration cfg)
        {
            _cfg = cfg;
            _connStr = _cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost("add")]
        public IActionResult AddArtist([FromBody] Artist req, [FromHeader(Name = "x-api-key")] string apiKey)
        {
            // 🔹 API Key validation
            if (!ValidateApiKey(apiKey))
                return Unauthorized("❌ Invalid API Key");

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // 🔹 Role validation
            if (role != "LabelAdmin" && role != "SuperAdmin" && role != "Artist")
                return Forbid();

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_AddArtist", conn) { CommandType = CommandType.StoredProcedure };

            cmd.Parameters.AddWithValue("@LabelID", req.LabelID);
            cmd.Parameters.AddWithValue("@ArtistName", req.ArtistName);
            cmd.Parameters.AddWithValue("@Email", req.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Country", req.Country ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Genre", req.Genre ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RevenueShare", req.RevenueShare);
            cmd.Parameters.AddWithValue("@CreatedBy", userId);

            conn.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { message = "✅ Artist added successfully" });
        }

        [HttpGet("all")]
        public IActionResult GetArtists([FromHeader(Name = "x-api-key")] string apiKey)
        {
            // 🔹 API Key validation
            if (!ValidateApiKey(apiKey))
                return Unauthorized("❌ Invalid API Key");

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            using var conn = new SqlConnection(_connStr);
            SqlCommand cmd;

            if (role == "LabelAdmin")
            {
                cmd = new SqlCommand("sp_GetArtistsByLabel", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@LabelID", userId);
            }
            else if (role == "Artist")
            {
                cmd = new SqlCommand("sp_GetArtistByID", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@ArtistID", userId);
            }
            else
            {
                cmd = new SqlCommand("sp_GetAllArtists", conn) { CommandType = CommandType.StoredProcedure };
            }

            conn.Open();
            using var reader = cmd.ExecuteReader();

            var artists = new List<object>();
            while (reader.Read())
            {
                artists.Add(new
                {
                    ArtistID = reader["ArtistID"],
                    ArtistName = reader["ArtistName"],
                    Email = reader["Email"],
                    Country = reader["Country"],
                    Genre = reader["Genre"],
                    RevenueShare = reader["RevenueShare"],
                    LabelID = reader["LabelID"],
                    CreatedBy = reader["CreatedBy"],
                    CreatedAt = reader["CreatedAt"]
                });
            }

            return Ok(new { message = "✅ Artists fetched successfully", count = artists.Count, artists });
        }

        private bool ValidateApiKey(string apiKey) =>
            !string.IsNullOrEmpty(apiKey) && apiKey == _cfg["ApiKey"];
    }
}
