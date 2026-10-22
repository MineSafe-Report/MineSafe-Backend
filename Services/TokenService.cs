using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(Account account, IReadOnlyList<string> roleNames);
}

// Creates the JWT token that the app receives after a successful login.
// The token contains a few facts about the user (claims) and is signed with our secret key.
// Anyone can read a token, but changing it would break the signature.
public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(Account account, IReadOnlyList<string> roleNames)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        // Rounded to whole seconds, so the JSON looks like "2026-10-20T06:30:00Z", which the app expects.
        var now = DateTime.UtcNow;
        var expiresAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc)
            .AddHours(_config.GetValue("Jwt:ExpiryHours", 12));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _config["Jwt:Issuer"],     // who created the token (this API)
            Audience = _config["Jwt:Audience"], // who the token is for (our apps)
            Expires = expiresAt,                // 12 hours by default, one shift
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = account.AccountId.ToString(), // the subject, meaning which account this is
                ["name"] = account.FullName,
                ["workerId"] = account.WorkerId,
                // A list, because an account can have several roles. Each one becomes a "role" claim.
                // RoleRefresher.cs checks them against the database on every request,
                // so a removed role stops working straight away.
                ["role"] = roleNames.ToArray()
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiresAt);
    }
}
