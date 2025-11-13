using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace TunewaveAPI.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _configuredApiKey;
        private const string ApiKeyHeaderName = "x-api-key";

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuredApiKey = configuration["ApiKey"]!;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip Auth endpoints
            if (context.Request.Path.StartsWithSegments("/api/Auth") ||
                context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/api/ApiKey"))
            {
                await _next(context);
                return;
            }

            // Check if header is present
            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { message = "❌ API Key is missing" });
                return;
            }

            // Compare with configured static key
            if (!_configuredApiKey.Equals(extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { message = "❌ Invalid API Key" });
                return;
            }

            await _next(context);
        }
    }
}
