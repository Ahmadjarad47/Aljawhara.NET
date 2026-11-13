using Ecom.Application.DTOs.Auth;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Ecom.Application.Services;

public sealed class AuthSessionService(EcomDbContext db, IJwtService jwt, IConfiguration configuration, TimeProvider clock)
{
    public async Task<LoginResponseDto?> StartAsync(AppUsers authenticatedUser, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var sessionId = Guid.NewGuid();
        var secret = RandomNumberGenerator.GetBytes(32);
        var hash = Convert.ToHexString(SHA256.HashData(secret));
        var expires = now.AddDays(Math.Clamp(configuration.GetValue("JwtSettings:RefreshTokenDays", 7), 1, 90));
        var response = await BuildResponseAsync(authenticatedUser, sessionId, secret, now, expires);

        // Recheck the Identity stamp under a lock: a password reset or block that won the
        // race with login must not permit a new session based on the previous credentials.
        var written = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AuthSessions" ("Id", "UserId", "SecurityStamp", "RefreshTokenHash", "ExpiresAtUtc")
            SELECT {sessionId}, u."Id", u."SecurityStamp", {hash}, {expires}
            FROM "AspNetUsers" u
            WHERE u."Id" = {authenticatedUser.Id} AND u."SecurityStamp" = {authenticatedUser.SecurityStamp}
                AND u."IsActive" AND u."EmailConfirmed" AND (u."LockoutEnd" IS NULL OR u."LockoutEnd" <= {now})
            FOR SHARE OF u
            ON CONFLICT ("Id") DO NOTHING
            """, ct);
        return written == 1 ? response : null;
    }

    public async Task<LoginResponseDto?> RotateAsync(string token, CancellationToken ct)
    {
        if (token.Length != 44)
            return null;
        var bytes = new byte[32];
        if (!Convert.TryFromBase64String(token, bytes, out var written) || written != 32 ||
            !string.Equals(Convert.ToBase64String(bytes), token, StringComparison.Ordinal))
            return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var eligible = Active(now).Where(s => s.RefreshTokenHash == hash);
        var session = await eligible.AsNoTracking().Include(s => s.User).SingleOrDefaultAsync(ct);
        if (session is null)
            return null;

        var newSecret = RandomNumberGenerator.GetBytes(32);
        var newHash = Convert.ToHexString(SHA256.HashData(newSecret));
        var response = await BuildResponseAsync(session.User, session.Id, newSecret, now, session.ExpiresAtUtc);

        // Compare and swap in the database. A replay does not revoke the winner's session.
        var rotated = await Active(clock.GetUtcNow().UtcDateTime)
            .Where(s => s.Id == session.Id && s.RefreshTokenHash == hash)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.RefreshTokenHash, newHash), ct);
        return rotated == 1 ? response : null;
    }

    public Task<bool> IsActiveAsync(string userId, Guid sessionId, bool hasAdminClaim, CancellationToken ct)
    {
        var session = Active(clock.GetUtcNow().UtcDateTime).Where(s => s.Id == sessionId && s.UserId == userId);
        if (hasAdminClaim)
        {
            // Never honor a stale Admin grant from a signed token after role removal.
            session = session.Where(s => db.UserRoles.Any(ur => ur.UserId == s.UserId &&
                db.Roles.Any(role => role.Id == ur.RoleId && role.NormalizedName == "ADMIN")));
        }
        return session.AnyAsync(ct);
    }

    public Task<int> RevokeAsync(string userId, Guid? sessionId = null, CancellationToken ct = default) =>
        db.AuthSessions.Where(s => s.UserId == userId && s.RevokedAtUtc == null &&
                (sessionId == null || s.Id == sessionId))
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.RevokedAtUtc, clock.GetUtcNow().UtcDateTime), ct);

    private IQueryable<AuthSession> Active(DateTime now) => db.AuthSessions.Where(s =>
        s.RevokedAtUtc == null && s.ExpiresAtUtc > now && s.User.IsActive && s.User.EmailConfirmed &&
        s.SecurityStamp == s.User.SecurityStamp &&
        (s.User.LockoutEnd == null || s.User.LockoutEnd <= now));

    private async Task<LoginResponseDto> BuildResponseAsync(AppUsers user, Guid sessionId, byte[] secret,
        DateTime now, DateTime sessionExpires)
    {
        var accessExpires = now.AddMinutes(Math.Clamp(configuration.GetValue("JwtSettings:ExpiryMinutes", 60), 1, 120));
        if (sessionExpires < accessExpires)
            accessExpires = sessionExpires;
        // JWT NumericDate has second precision. Return the same instant in JSON.
        accessExpires = DateTimeOffset.FromUnixTimeSeconds(new DateTimeOffset(accessExpires).ToUnixTimeSeconds()).UtcDateTime;
        return new LoginResponseDto
        {
            Token = await jwt.GenerateTokenAsync(user, sessionId, accessExpires),
            RefreshToken = Convert.ToBase64String(secret),
            ExpiresAt = accessExpires,
            User = new UserResponseDto
            {
                Id = user.Id, Username = user.UserName ?? "", Email = user.Email ?? "", PhoneNumber = user.PhoneNumber ?? "",
                EmailConfirmed = user.EmailConfirmed, PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                WhatsAppNotificationsEnabled = user.WhatsAppNotificationsEnabled, WhatsAppConsentPhone = user.WhatsAppConsentPhone,
                IsActive = user.IsActive, CreatedAt = user.CreatedAt
            }
        };
    }
}
