using System.Security.Cryptography;
using Ecom.Application.DTOs.Auth;
using Ecom.Domain.Entity;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecom.Application.WhatsApp;

public sealed record PhoneVerificationRequestResult(bool Accepted, string? ErrorCode = null);

public sealed class WhatsAppConsentService(
    EcomDbContext db,
    UserManager<AppUsers> users,
    IGupshupWhatsAppService sender,
    IOptions<GupshupOptions> options)
{
    private readonly GupshupOptions _options = options.Value;

    public async Task<WhatsAppPreferenceDto?> GetAsync(string userId, CancellationToken ct)
    {
        var user = await users.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        return user == null ? null : Map(user);
    }

    public async Task<PhoneVerificationRequestResult> RequestPhoneVerificationAsync(
        string userId, string phone, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId) ?? throw new ArgumentException("User not found.");
        var normalized = WhatsAppPhone.Normalize(phone, _options)
            ?? throw new ArgumentException("A valid WhatsApp phone is required; use + and the calling code for international numbers.");

        var now = DateTime.UtcNow;
        var recent = await db.WhatsAppPhoneVerifications.AsNoTracking()
            .Where(v => v.AppUserId == userId && v.CreatedAt > now.AddMinutes(-1))
            .AnyAsync(ct);
        if (recent) throw new InvalidOperationException("Please wait before requesting another verification code.");

        if (user.WhatsAppNotificationsEnabled && user.WhatsAppConsentPhone is { } previous &&
            WhatsAppPhone.Normalize(previous, _options) != normalized)
        {
            AddAudit(user.Id, null, WhatsAppPhone.Normalize(previous, _options) ?? previous,
                WhatsAppConsentAction.Revoked, "PhoneChanged", now);
            user.WhatsAppNotificationsEnabled = false;
            user.WhatsAppConsentPhone = null;
            user.WhatsAppConsentAtUtc = null;
            user.WhatsAppConsentRevokedAtUtc = now;
            await StopPendingSendsAsync(previous, now, ct);
            var changed = await users.UpdateAsync(user);
            if (!changed.Succeeded) throw new InvalidOperationException("Unable to revoke previous phone consent.");
            await db.SaveChangesAsync(ct);
        }


        await db.WhatsAppPhoneVerifications
            .Where(v => v.AppUserId == userId && v.ConsumedAtUtc == null && v.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsActive, false), ct);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var salt = RandomNumberGenerator.GetBytes(16);
        var challenge = new WhatsAppPhoneVerification
        {
            AppUserId = user.Id,
            Phone = normalized,
            CodeSalt = salt,
            CodeHash = HashCode(code, salt),
            ExpiresAtUtc = now.AddMinutes(10),
            CreatedBy = user.Id
        };
        db.WhatsAppPhoneVerifications.Add(challenge);
        await db.SaveChangesAsync(ct);

        var result = await sender.SendPhoneVerificationCodeAsync(normalized, code, ct);
        if (result.Outcome == WhatsAppSendOutcome.Accepted)
        {
            challenge.ProviderMessageId = result.MessageId;
            await db.SaveChangesAsync(ct);
            return new(true);
        }

        challenge.IsActive = false;
        await db.SaveChangesAsync(ct);
        return new(false, result.ErrorCode ?? result.Outcome.ToString());
    }

    public async Task<WhatsAppPreferenceDto> ConfirmPhoneAsync(
        string userId, string phone, string code, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId) ?? throw new ArgumentException("User not found.");
        var normalized = WhatsAppPhone.Normalize(phone, _options)
            ?? throw new ArgumentException("A valid WhatsApp phone is required; use + and the calling code for international numbers.");
        var now = DateTime.UtcNow;
        var challenge = await db.WhatsAppPhoneVerifications
            .Where(v => v.AppUserId == userId && v.Phone == normalized && v.ConsumedAtUtc == null && v.IsActive)
            .OrderByDescending(v => v.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No active phone verification exists.");
        if (challenge.ExpiresAtUtc <= now)
        {
            challenge.IsActive = false;
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("The phone verification code has expired.");
        }
        if (!CryptographicOperations.FixedTimeEquals(HashCode(code, challenge.CodeSalt), challenge.CodeHash))
        {
            challenge.FailedAttemptCount++;
            if (challenge.FailedAttemptCount >= 5) challenge.IsActive = false;
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("The phone verification code is invalid.");
        }

        var previousConsentPhone = user.WhatsAppConsentPhone;
        challenge.ConsumedAtUtc = now;
        challenge.IsActive = false;
        if (user.WhatsAppNotificationsEnabled && previousConsentPhone != null && previousConsentPhone != normalized)
        {
            AddAudit(user.Id, null, previousConsentPhone, WhatsAppConsentAction.Revoked, "PhoneChanged", now);
            await StopPendingSendsAsync(previousConsentPhone, now, ct);
            user.WhatsAppNotificationsEnabled = false;
            user.WhatsAppConsentPhone = null;
            user.WhatsAppConsentAtUtc = null;
            user.WhatsAppConsentRevokedAtUtc = now;
        }
        user.PhoneNumber = normalized;
        user.PhoneNumberConfirmed = true;
        var update = await users.UpdateAsync(user);
        if (!update.Succeeded) throw new InvalidOperationException(string.Join(", ", update.Errors.Select(e => e.Description)));
        await db.SaveChangesAsync(ct);
        return Map(user);
    }

    public async Task<WhatsAppPreferenceDto> UpdatePreferenceAsync(
        string userId, bool enabled, string? phone, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId) ?? throw new ArgumentException("User not found.");
        var now = DateTime.UtcNow;
        if (enabled)
        {
            var normalized = WhatsAppPhone.Normalize(phone ?? user.PhoneNumber, _options)
                ?? throw new ArgumentException("A valid WhatsApp phone is required; use + and the calling code for international numbers.");
            var accountPhone = WhatsAppPhone.Normalize(user.PhoneNumber, _options);
            if (!user.PhoneNumberConfirmed || accountPhone != normalized)
                throw new InvalidOperationException("Verify ownership of this phone before enabling WhatsApp notifications.");
            if (!user.WhatsAppNotificationsEnabled || user.WhatsAppConsentPhone != normalized)
            {
                user.WhatsAppNotificationsEnabled = true;
                user.WhatsAppConsentPhone = normalized;
                user.WhatsAppConsentAtUtc = now;
                user.WhatsAppConsentRevokedAtUtc = null;
                AddAudit(user.Id, null, normalized, WhatsAppConsentAction.Granted, "AccountSettings", now);
            }
        }
        else if (user.WhatsAppNotificationsEnabled && user.WhatsAppConsentPhone is { } previous)
        {
            user.WhatsAppNotificationsEnabled = false;
            user.WhatsAppConsentPhone = null;
            user.WhatsAppConsentAtUtc = null;
            user.WhatsAppConsentRevokedAtUtc = now;
            AddAudit(user.Id, null, previous, WhatsAppConsentAction.Revoked, "AccountSettings", now);
            await StopPendingSendsAsync(previous, now, ct);
        }

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        await db.SaveChangesAsync(ct);
        return Map(user);
    }

    private void AddAudit(string? userId, int? orderId, string phone, WhatsAppConsentAction action,
        string source, DateTime occurredAtUtc) => db.WhatsAppConsentAudits.Add(new WhatsAppConsentAudit
    {
        AppUserId = userId,
        OrderId = orderId,
        Phone = phone,
        Action = action,
        Source = source,
        OccurredAtUtc = occurredAtUtc,
        ProviderSyncStatus = "NotRequired",
        CreatedBy = userId ?? source
    });

    private async Task StopPendingSendsAsync(string phone, DateTime revokedAtUtc, CancellationToken ct)
    {
        await db.WhatsAppInvoices
            .Where(i => (i.Status == WhatsAppInvoiceStatus.Pending || i.Status == WhatsAppInvoiceStatus.Preparing) &&
                i.PaymentAttempt.Order.WhatsAppConsentPhone == phone &&
                i.PaymentAttempt.Order.WhatsAppConsentAtUtc <= revokedAtUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, WhatsAppInvoiceStatus.Skipped)
                .SetProperty(i => i.LastErrorCode, "CUSTOMER_CONSENT_REVOKED")
                .SetProperty(i => i.LeaseOwner, (string?)null)
                .SetProperty(i => i.LeaseUntilUtc, (DateTime?)null), ct);
    }

    private static byte[] HashCode(string code, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(code, salt, 100_000, HashAlgorithmName.SHA256, 32);

    private static WhatsAppPreferenceDto Map(AppUsers user) => new()
    {
        Enabled = user.WhatsAppNotificationsEnabled,
        PhoneNumber = user.WhatsAppConsentPhone ?? user.PhoneNumber,
        PhoneNumberConfirmed = user.PhoneNumberConfirmed,
        ConsentAtUtc = user.WhatsAppConsentAtUtc,
        RevokedAtUtc = user.WhatsAppConsentRevokedAtUtc
    };
}
