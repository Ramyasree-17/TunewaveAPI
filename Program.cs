using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Controllers
builder.Services.AddControllers();

// 🔹 CORS Policy – allow all origins, headers, and methods
builder.Services.AddCors(options =>
{
    options.AddPolicy("OpenCors", policy =>
    {
        policy
            .AllowAnyOrigin()   // allow requests from any domain
            .AllowAnyHeader()   // allow any HTTP header
            .AllowAnyMethod();  // allow GET, POST, PUT, DELETE, etc.
    });
});

// 🔹 Swagger with JWT Security
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TunewaveAPI",
        Version = "v1"
    });

    // JWT Authorization
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter token like: Bearer {your_token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 🔹 JWT Authentication Setup
// Replace these with your appsettings values if needed
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SuperSecretKey123!");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://spacestation.tunewave.in",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "https://spacestation.tunewave.in",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 🔹 Exception Handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error"); // optional: map an ErrorController
    app.UseHsts();
}

// 🔹 HTTPS & Static files
app.UseHttpsRedirection();
app.UseStaticFiles();

// 🔹 Apply CORS
app.UseCors("OpenCors");

// 🔹 Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 🔹 Swagger (accessible with JWT)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TunewaveAPI v1");
    c.RoutePrefix = "swagger";
});

// Optional: protect Swagger UI
app.UseWhen(context => context.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
{
    appBuilder.UseAuthentication();
    appBuilder.UseAuthorization();
});

// 🔹 Map Controllers
app.MapControllers();

// 🔹 Run
app.Run();
