using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(Account account, IReadOnlyList<string> roleNames);
}

// Makes the JWT token (the "wristband") after a successful login.
// The token holds a few facts about the user (claims) and is SIGNED with our secret key.
// Anyone can read a token, but nobody can change it without breaking the signature.
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
        var expiresAt = DateTime.UtcNow.AddHours(_config.GetValue("Jwt:ExpiryHours", 12));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _config["Jwt:Issuer"],          // who made the token (us)
            Audience = _config["Jwt:Audience"],      // who it's meant for (our apps)
            Expires = expiresAt,                     // 12 hours = one shift
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = account.AccountId.ToString(), // "subject": which account this is
                ["name"] = account.FullName,
                ["workerId"] = account.WorkerId,
                // A list, because an account can have several roles. Each becomes a "role" claim.
                // (On every request these are re-checked against the database anyway,
                //  see RoleRefresher.cs, so a removed role stops working straight away.)
                ["role"] = roleNames.ToArray()
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiresAt);
    }
}
