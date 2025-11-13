using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Threading.Tasks;

namespace TunewaveAPI.Services
{
    public interface IApiKeyService
    {
        string GenerateApiKey();
        Task<int> AddKeyAsync(string key, string? owner, string? role, int? createdBy, DateTime? expiry);
        Task<(bool IsValid, string? Role)> ValidateAsync(string key);
        Task RevokeAsync(int apiKeyId);
    }

    public class ApiKeyService : IApiKeyService
    {
        private readonly string _connStr;
        public ApiKeyService(IConfiguration cfg) => _connStr = cfg.GetConnectionString("DefaultConnection")!;

        public string GenerateApiKey() => Guid.NewGuid().ToString("N") + "-" + Guid.NewGuid().ToString("N");

        public async Task<int> AddKeyAsync(string key, string? owner, string? role, int? createdBy, DateTime? expiry)
        {
            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_AddApiKey", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@KeyValue", key);
            cmd.Parameters.AddWithValue("@Owner", owner ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Role", role ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", createdBy ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExpiryDate", expiry ?? (object)DBNull.Value);

            await conn.OpenAsync();
            var id = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(id);
        }

        public async Task<(bool, string?)> ValidateAsync(string key)
        {
            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_ValidateApiKey", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@KeyValue", key);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.Read()) return (false, null);
            return (true, reader["Role"]?.ToString());
        }

        public async Task RevokeAsync(int apiKeyId)
        {
            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_RevokeApiKey", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ApiKeyID", apiKeyId);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
