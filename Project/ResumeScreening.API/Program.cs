using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;
using ResumeScreening.API.Data;
using ResumeScreening.API.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey   = jwtSettings["SecretKey"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Map JWT claim names (e.g. "role", "sub") to ClaimTypes so [Authorize(Roles)] and GetUserId work reliably.
    options.MapInboundClaims = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwtSettings["Issuer"],
        ValidAudience            = jwtSettings["Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        RoleClaimType            = System.Security.Claims.ClaimTypes.Role,
        NameClaimType            = System.Security.Claims.ClaimTypes.Name,
    };
});

builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────────────────────────────
var originsRaw = builder.Configuration["AllowedOrigins"]
                 ?? "http://localhost:4200;https://localhost:4200;http://127.0.0.1:4200";
var allowedOrigins = originsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Resume Screening API",
        Version = "v1",
        Description = "AI-Powered Resume Screening System — MCA Major Project"
    });

    // Add JWT auth to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT token. Example: Bearer {your_token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Register Services (add more here as you build them) ───────────────────────
builder.Services.AddScoped<IBlobService, BlobService>();
builder.Services.AddScoped<ScoringService>();
builder.Services.AddHttpClient<AiScoringService>();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

// ── Auto-apply migrations on startup (useful for development) ─────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (SqlException ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine("  Database connection failed (SQL error " + ex.Number + ").");
        Console.WriteLine("  1) Start SQL Server: services.msc → SQL Server (MSSQLSERVER) or SQL Server (SQLEXPRESS)");
        Console.WriteLine("  2) Set ConnectionStrings:DefaultConnection in appsettings.Development.json, e.g.:");
        Console.WriteLine("     Server=localhost;Trusted_Connection=True;TrustServerCertificate=True;");
        Console.WriteLine("     Server=localhost\\\\SQLEXPRESS;...   (SQL Express)");
        Console.WriteLine("     Server=(localdb)\\\\mssqllocaldb;... (Visual Studio LocalDB)");
        Console.WriteLine();
        Console.ResetColor();
        throw;
    }
}

// ── Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Resume Screening API v1");
        c.RoutePrefix = string.Empty; // Swagger at root: https://localhost:5001/
    });
}

// CORS before auth. Do NOT redirect HTTP→HTTPS in Development: Angular proxy uses http://localhost:5109
// and browsers strip Authorization on the redirect follow-up → 401 on POST /api/jobs.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("AngularPolicy");

// Serve files saved by BlobService local-disk fallback (Development): /uploads/**
var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads",
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
