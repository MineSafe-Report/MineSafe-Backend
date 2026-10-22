// The application's entry point. It does two things:
//   1. Registers the services the app uses.
//   2. Sets up the pipeline, which is the order every request passes through.

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MineSafety.Api.Data;
using MineSafety.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- 1. Services ----------

// SQL Server, using the connection string from appsettings.json.
// The tables must already exist (see scripts/create-local-db.ps1).
builder.Services.AddDbContext<MineSafetyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MineSafetyDb")));

// Our own services. Scoped means a new instance is created for every request.
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

// The JWT signing key is a secret, so it isn't in appsettings.json (which is committed).
// In development it comes from appsettings.Development.json, and anywhere else from an
// environment variable called Jwt__Key. Without a key the app refuses to start.
var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key is missing or shorter than 32 characters.");

// Authentication: requests carry a JWT token, and this is how we check that it's genuine.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep the short claim names ("sub", "role") we put in the token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true, // reject tokens that weren't signed with our key
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,         // reject expired tokens
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"           // [Authorize(Roles = ...)] checks the "role" claims
        };
        // Once the token is valid, reload the account's current roles from the database so that
        // deactivated accounts and removed roles stop working straight away (see RoleRefresher.cs).
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = RoleRefresher.OnTokenValidated
        };
    });
builder.Services.AddAuthorization();

// Controllers. Validation errors use the same shape as every other error:
// { "code": "VALIDATION_FAILED", "message": "...", "errors": { "field": ["..."] } }
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

// Swagger, the API test page. The security settings add an Authorize button for pasting a token.
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

// CORS lets the web admin app, which runs on a different address, call this API.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------- 2. Pipeline (the order matters) ----------

if (app.Environment.IsDevelopment())
{
    // Development only: add test data if the database is empty.
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

    // Check that the columns added by database/migrations/003_AddEvidenceTracking.sql exist.
    try
    {
        _ = db.Reports.Select(r => r.ExpectedImageCount).FirstOrDefault();
        _ = db.ReportAttachments.Select(a => a.ClientAttachmentId).FirstOrDefault();
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex,
            "The database is missing the evidence-tracking columns. Run " +
            "database/migrations/003_AddEvidenceTracking.sql against this database.");
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication(); // 1. who is making the request (reads the token, then RoleRefresher runs)
app.UseAuthorization();  // 2. are they allowed to do this ([Authorize] and roles)
app.MapControllers();    // 3. send the request to the matching controller method

// GET /health needs no login. The app calls it to check whether it can reach the server.
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
