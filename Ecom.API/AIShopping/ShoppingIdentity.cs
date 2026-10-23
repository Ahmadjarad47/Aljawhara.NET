using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ecom.AIShopping.Validation;
using Microsoft.AspNetCore.DataProtection;

namespace Ecom.API.AIShopping;

public sealed class ShoppingIdentity(IDataProtectionProvider protection, TimeProvider clock)
{
    public const string Header = "X-AI-Shopping-Session";
    private readonly IDataProtector _protector = protection.CreateProtector("Aljawhara.AIShopping.Guest.v1");

    public string CreateGuestToken() => _protector.Protect($"{clock.GetUtcNow().ToUnixTimeSeconds()}:{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}");

    public string Owner(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) throw new ShoppingException("INVALID_IDENTITY", 401);
            return Hash("user:" + userId);
        }
        var raw = context.Request.Headers[Header].ToString();
        if (raw.Length is < 50 or > 1000) throw new ShoppingException("SHOPPING_SESSION_REQUIRED", 401);
        try
        {
            var parts = _protector.Unprotect(raw).Split(':');
            var now = clock.GetUtcNow().ToUnixTimeSeconds();
            if (parts.Length != 2 || parts[1].Length != 64 || !long.TryParse(parts[0], out var issued) ||
                issued > now + 30 || now - issued > 86400) throw new CryptographicException();
            return Hash("guest:" + parts[1]);
        }
        catch (CryptographicException) { throw new ShoppingException("INVALID_SHOPPING_SESSION", 401); }
    }

    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
