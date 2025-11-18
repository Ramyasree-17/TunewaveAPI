using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TunewaveAPI.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace TunewaveAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public AuthController(IConfiguration cfg)
        {
            _cfg = cfg;
            _connStr = _cfg.GetConnectionString("DefaultConnection")!;
        }

        // ================================================
        // REGISTER USER
        // ================================================
        [AllowAnonymous]
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Email and Password are required." });

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Email", req.Email);
                    int exists = (int)checkCmd.ExecuteScalar();

                    if (exists > 0)
                        return Conflict(new { message = "⚠️ Email already registered." });
                }

                using var cmd = new SqlCommand("sp_RegisterUser", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@FullName", req.FullName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Email", req.Email);
                cmd.Parameters.AddWithValue("@PasswordHash", BCrypt.Net.BCrypt.HashPassword(req.Password));
                cmd.Parameters.AddWithValue("@Role", req.Role ?? "User");
                cmd.Parameters.AddWithValue("@MobileNumber", req.MobileNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@CountryCode", req.CountryCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ThresholdValue", req.ThresholdValue);
                cmd.Parameters.AddWithValue("@RevenueShare", req.RevenueShare);
                cmd.Parameters.AddWithValue("@AgreementStartDate", req.AgreementStartDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@AgreementEndDate", req.AgreementEndDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PublicProfileName", req.PublicProfileName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LabelName", req.LabelName ?? (object)DBNull.Value);

                cmd.ExecuteNonQuery();

                return Ok(new { message = "✅ User registered successfully" });
            }
            catch (SqlException ex)
            {
                return BadRequest(new { error = "SQL Error: " + ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ================================================
        // CHECK EMAIL
        // ================================================
        [AllowAnonymous]
        [HttpGet("check-email")]
        public IActionResult CheckEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "❌ Email is required." });

            using var conn = new SqlConnection(_connStr);

            using var cmd = new SqlCommand(
                "SELECT FullName, Email, PublicProfileName AS UserName, Role FROM Users WHERE Email = @Email",
                conn
            );

            cmd.Parameters.AddWithValue("@Email", email);

            conn.Open();
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                string role = "";

                try
                {
                    role = reader["Role"]?.ToString() ?? "";
                }
                catch
                {
                    role = "";
                }

                return Ok(new
                {
                    exists = true,
                    display_name = reader["FullName"]?.ToString(),
                    email = reader["Email"]?.ToString(),
                    username = reader["UserName"]?.ToString(),
                    role = role
                });
            }

            return Ok(new { exists = false });
        }

        // ================================================
        // LOGIN
        // ================================================
        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Email and Password are required." });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_LoginUser", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Email", req.Email);

            conn.Open();
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return Unauthorized(new { message = "❌ Invalid credentials (Email not found)" });

            int userId = (int)reader["UserID"];
            string email = reader["Email"].ToString()!;
            string fullName = reader["FullName"]?.ToString() ?? "";
            string hash = reader["PasswordHash"]?.ToString() ?? "";

            string role = reader["Role"]?.ToString() ?? "User";

            if (!BCrypt.Net.BCrypt.Verify(req.Password, hash))
                return Unauthorized(new { message = "❌ Invalid credentials (Incorrect password)" });

            reader.Close();

            using var updateCmd = new SqlCommand(
                "UPDATE Users SET LastSignIn = GETDATE() WHERE UserID = @UserID", conn
            );
            updateCmd.Parameters.AddWithValue("@UserID", userId);
            updateCmd.ExecuteNonQuery();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, fullName),
                new Claim(ClaimTypes.Role, role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _cfg["Jwt:Issuer"],
                audience: _cfg["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(_cfg["Jwt:ExpireMinutes"] ?? "120")),
                signingCredentials: creds
            );

            return Ok(new
            {
                message = "✅ Login successful",
                token = new JwtSecurityTokenHandler().WriteToken(token),
                role,
                fullName,
                email
            });
        }

        // ================================================
        // FORGOT PASSWORD
        // ================================================
        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "❌ Email is required." });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", conn);

            cmd.Parameters.AddWithValue("@Email", email);

            conn.Open();
            int count = (int)cmd.ExecuteScalar();

            if (count == 0)
                return NotFound(new { message = "❌ Email not found." });

            return Ok(new { message = "✅ Reset link (mock) sent successfully." });
        }

        // ================================================
        // RESET PASSWORD
        // ================================================
        [AllowAnonymous]
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { message = "❌ Email and New Password are required." });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand(
                "UPDATE Users SET PasswordHash = @PasswordHash WHERE Email = @Email",
                conn
            );

            cmd.Parameters.AddWithValue("@Email", req.Email);
            cmd.Parameters.AddWithValue("@PasswordHash", BCrypt.Net.BCrypt.HashPassword(req.NewPassword));

            conn.Open();
            int rows = cmd.ExecuteNonQuery();

            if (rows == 0)
                return NotFound(new { message = "❌ Email not found." });

            return Ok(new { message = "✅ Password reset successful." });
        }

        // ================================================
        // VERIFY TOKEN
        // ================================================
        [HttpGet("verify")]
        [Authorize]
        public IActionResult Verify()
        {
            string? email = User.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(email))
                return Unauthorized(new { message = "❌ Invalid or missing token" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_VerifyUserByEmail", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Email", email);

            conn.Open();
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return NotFound(new { message = "❌ User not found" });

            var user = new
            {
                UserID = reader["UserID"],
                FullName = reader["FullName"],
                Email = reader["Email"],
                Role = reader["Role"],
                MobileNumber = reader["MobileNumber"],
                CountryCode = reader["CountryCode"],
                ThresholdValue = reader["ThresholdValue"],
                RevenueShare = reader["RevenueShare"],
                AgreementStartDate = reader["AgreementStartDate"],
                AgreementEndDate = reader["AgreementEndDate"],
                PublicProfileName = reader["PublicProfileName"],
                LabelName = reader["LabelName"],
                LastSignIn = reader["LastSignIn"],
                Status = reader["Status"]
            };

            return Ok(new { message = "✅ Token verified successfully", user });
        }
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }
}