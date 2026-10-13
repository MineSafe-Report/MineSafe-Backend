// Program.cs is the START of the app. It does two jobs:
//   1. Register services ("here are the tools the app can use")
//   2. Build the pipeline ("here is the order every request passes through")

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MineSafety.Api.Data;
using MineSafety.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- 1. REGISTER SERVICES ----------

// Database: use SQL Server with the connection string from appsettings.json
builder.Services.AddDbContext<MineSafetyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MineSafetyDb")));

// Our own services. "Scoped" = one fresh instance per request.
// When a controller asks for ILocationService, ASP.NET gives it a LocationService.
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IAccountService, AccountService>();

// Authentication: "every request may carry a JWT token; here's how to check it's genuine"
var jwt = builder.Configuration.GetSection("Jwt");
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
            ValidateLifetime = true,           // reject expired tokens
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"             // [Authorize(Roles = ...)] reads the "role" claim
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();

// Swagger: the test page. The extra setup adds an "Authorize" button where you paste a token.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Mine Safety API", Version = "v1" });
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

// CORS: lets a web page on a different address (the future web admin app) call this API.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------- 2. THE PIPELINE (order matters) ----------

if (app.Environment.IsDevelopment())
{
    // Development only: create the database if it doesn't exist, then fill it with test data.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MineSafetyDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();   // 1st: WHO are you? (reads the token)
app.UseAuthorization();    // 2nd: are you ALLOWED? (checks [Authorize] and roles)
app.MapControllers();      // 3rd: hand the request to the matching controller method

app.Run();
