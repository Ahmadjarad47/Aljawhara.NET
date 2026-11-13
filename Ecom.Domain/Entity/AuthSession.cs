namespace Ecom.Domain.Entity;

public sealed class AuthSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public AppUsers User { get; set; } = null!;
    public string SecurityStamp { get; set; } = null!;
    public string RefreshTokenHash { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
