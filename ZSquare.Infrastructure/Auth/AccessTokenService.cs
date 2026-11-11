using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ZSquare.Application.Auth;
using ZSquare.Domain;
using System.Text;

namespace ZSquare.Infrastructure.Auth;

public sealed class AccessTokenService : IAccessTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _signingCredentials;

    public AccessTokenService(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        _audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 bytes in user-secrets or the environment.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult Generate(User user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(TokenLifetime);
        var claims = new[]
        {
            new Claim("UserId", user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("email", user.Email),
            new Claim("roleId", user.RoleId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("roles", user.Role.Name),
        };
        var jwt = new JwtSecurityToken(
            _issuer,
            _audience,
            claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(jwt), new DateTimeOffset(expiresAt, TimeSpan.Zero));
    }
}
