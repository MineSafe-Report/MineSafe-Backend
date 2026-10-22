// Program.cs is the START of the app. It does two jobs:
//   1. Register services ("here are the tools the app can use")
//   2. Build the pipeline ("here is the order every request passes through")

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MineSafety.Api.Data;
using MineSafety.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- 1. REGISTER SERVICES ----------

// Database: SQL Server, using the connection string from appsettings.
// The tables must ALREADY exist (created by Michael's scripts, see scripts/create-local-db.ps1).
builder.Services.AddDbContext<MineSafetyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MineSafetyDb")));

// Our own services. "Scoped" = one fresh instance per request.
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

// The JWT signing key is a secret, so it is NOT in appsettings.json (which is committed).
// In development it comes from appsettings.Development.json; anywhere else, from an
// environment variable called Jwt__Key. No key = the app refuses to start.
var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key is missing or shorter than 32 characters.");

// Authentication: "every request may carry a JWT token; here's how to check it's genuine"
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep claim names short ("sub", "role") as we wrote them
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,   // reject tokens not signed with OUR key
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,           // reject expired tokens
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"             // [Authorize(Roles = ...)] reads the "role" claims
        };
        // After the token checks out, re-load the account's CURRENT roles from the database,
        // so deactivations and role changes take effect immediately (see RoleRefresher.cs).
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = RoleRefresher.OnTokenValidated
        };
    });
builder.Services.AddAuthorization();

// Controllers. The extra part makes validation errors (missing/too-long fields) use the same
// error shape as everything else: { "code": "VALIDATION_FAILED", "message": "...", "errors": { field: [..] } }
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new
    {
        code = "VALIDATION_FAILED",
        message = "Some fields are missing or invalid.",
        errors = context.ModelState
            .Where(e => e.Value != null && e.Value.Errors.Count > 0)
            .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray())
    });
});

// Swagger: the test page. The extra setup adds an "Authorize" button where you paste a token.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MineSafe API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Log in with POST /api/auth/login, copy the token, paste it here (without 'Bearer')."
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

// CORS: lets a web page on a different address (the web admin app) call this API.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------- 2. THE PIPELINE (order matters) ----------

if (app.Environment.IsDevelopment())
{
    // Development only: put test data into the (script-created) database if it's empty.
    // The API never creates or changes tables itself.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MineSafetyDbContext>();
    try
    {
        DbSeeder.Seed(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex,
            "Could not seed the database. Has it been created from Michael's SQL scripts? " +
            "Run scripts/create-local-db.ps1 first.");
    }

    // The API expects the columns added by database/migrations/AddEvidenceTracking.sql.
    try
    {
        _ = db.Reports.Select(r => r.ExpectedImageCount).FirstOrDefault();
        _ = db.ReportAttachments.Select(a => a.ClientAttachmentId).FirstOrDefault();
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex,
            "The database is missing the evidence-tracking columns. Run " +
            "database/migrations/AddEvidenceTracking.sql against this database.");
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();   // 1st: WHO are you? (reads the token, then RoleRefresher)
app.UseAuthorization();    // 2nd: are you ALLOWED? (checks [Authorize] and roles)
app.MapControllers();      // 3rd: hand the request to the matching controller method

// GET /health : no login needed. The app calls this to check "can I reach the server right now?"
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
