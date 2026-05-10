using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartFashion.Api.Data;
using SmartFashion.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

static string RequiredConfig(IConfiguration config, string key)
{
    var value = config[key];
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"{key} is missing. Configure it with user secrets, environment variables, or appsettings.Development.json.");

    return value;
}

// ✅ REQUIRED for controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ MySQL (Pomelo)
var conn = RequiredConfig(builder.Configuration, "ConnectionStrings:Default");
var mysqlVersion = builder.Configuration["Database:ServerVersion"] ?? "8.0.36-mysql";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(conn, ServerVersion.Parse(mysqlVersion));
});

// ✅ JWT
builder.Services.AddScoped<JwtTokenService>();

var jwtKey = RequiredConfig(builder.Configuration, "Jwt:Key");
var issuer = RequiredConfig(builder.Configuration, "Jwt:Issuer");
var audience = RequiredConfig(builder.Configuration, "Jwt:Audience");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes for HMAC-SHA256 signing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

// CORS (dev)
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowAllDev", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<GoogleTokenValidator>();
builder.Services.AddScoped<AppleTokenValidator>();
var app = builder.Build();

app.UseCors("AllowAllDev");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartFashion.Api v1");
        options.RoutePrefix = string.Empty;
    });
    app.MapGet("/swagger", () => Results.Redirect("/"));
}

app.UseAuthentication();
app.UseAuthorization();

// ✅ Map controllers
app.MapControllers();

app.Run();
