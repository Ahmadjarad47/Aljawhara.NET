using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ecom.Application.Services;

public sealed class JwtService(IConfiguration configuration, UserManager<AppUsers> users) : IJwtService
{
    public const string SessionClaim = "sid";

    public async Task<string> GenerateTokenAsync(AppUsers user, Guid sessionId, DateTime expiresAtUtc)
    {
        var settings = configuration.GetSection("JwtSettings");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings["SecretKey"]!)), SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(SessionClaim, sessionId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D"))
        };
        claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim(ClaimTypes.Role, role)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: settings["Issuer"], audience: settings["Audience"], claims: claims,
            expires: expiresAtUtc, signingCredentials: credentials));
    }
}
