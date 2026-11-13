using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ecom.Application.Services;

public sealed class OtpService(EcomDbContext db, IConfiguration configuration, TimeProvider clock) : IOtpService
{
    public const int ExpirationMinutes = 10;

    public async Task<string?> IssueAsync(AppUsers user, AccountChallengePurpose purpose)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var hash = Hash(user, purpose, code);
        var windowCutoff = now.AddHours(-1);
        // One row per account and purpose makes issuance durable across processes.
        // The conflict condition is re-evaluated under the row lock.
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AccountChallenges"
                ("UserId", "Purpose", "CodeHash", "ExpiresAtUtc", "LastIssuedAtUtc", "WindowStartedAtUtc", "IssuedCount", "Attempts")
            VALUES ({user.Id}, {(int)purpose}, {hash}, {now.AddMinutes(ExpirationMinutes)}, {now}, {now}, 1, 0)
            ON CONFLICT ("UserId", "Purpose") DO UPDATE SET
                "CodeHash" = EXCLUDED."CodeHash", "ExpiresAtUtc" = EXCLUDED."ExpiresAtUtc",
                "LastIssuedAtUtc" = EXCLUDED."LastIssuedAtUtc", "Attempts" = 0, "ConsumedAtUtc" = NULL,
                "WindowStartedAtUtc" = CASE WHEN "AccountChallenges"."WindowStartedAtUtc" <= {windowCutoff}
                    THEN EXCLUDED."WindowStartedAtUtc" ELSE "AccountChallenges"."WindowStartedAtUtc" END,
                "IssuedCount" = CASE WHEN "AccountChallenges"."WindowStartedAtUtc" <= {windowCutoff}
                    THEN 1 ELSE "AccountChallenges"."IssuedCount" + 1 END
            WHERE "AccountChallenges"."LastIssuedAtUtc" <= {now.AddMinutes(-1)} AND
                ("AccountChallenges"."WindowStartedAtUtc" <= {windowCutoff} OR "AccountChallenges"."IssuedCount" < 3)
            """);
        return changed == 1 ? code : null;
    }

    public async Task<bool> ConsumeAsync(AppUsers user, AccountChallengePurpose purpose, string code)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Hash(user, purpose, code);
        var eligible = db.AccountChallenges.Where(c => c.UserId == user.Id && c.Purpose == purpose &&
            c.ConsumedAtUtc == null && c.Attempts < 5 && c.ExpiresAtUtc > now);
        var consumed = await eligible.Where(c => c.CodeHash == hash)
            .ExecuteUpdateAsync(update => update.SetProperty(c => c.ConsumedAtUtc, now));
        if (consumed == 1)
            return true;

        await eligible.Where(c => c.CodeHash != hash)
            .ExecuteUpdateAsync(update => update.SetProperty(c => c.Attempts, c => c.Attempts + 1));
        return false;
    }

    private string Hash(AppUsers user, AccountChallengePurpose purpose, string code)
    {
        // Domain separation keeps low-entropy OTP hashes keyed without storing a code
        // or adding a second secret that could drift between API instances.
        var signingSecret = Encoding.UTF8.GetBytes(configuration["JwtSettings:SecretKey"]!);
        var otpKey = HMACSHA256.HashData(signingSecret, "aljawhara:account-challenge:v1"u8);
        var input = Encoding.UTF8.GetBytes($"{(int)purpose}\n{user.Id}\n{user.SecurityStamp}\n{user.NormalizedEmail}\n{code}");
        return Convert.ToHexString(HMACSHA256.HashData(otpKey, input));
    }
}
