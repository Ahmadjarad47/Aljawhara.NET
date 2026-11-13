namespace Ecom.Domain.Entity;

public enum AccountChallengePurpose { EmailVerification = 1, PasswordRecovery = 2 }

public sealed class AccountChallenge
{
    public string UserId { get; set; } = null!;
    public AccountChallengePurpose Purpose { get; set; }
    public string CodeHash { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastIssuedAtUtc { get; set; }
    public DateTime WindowStartedAtUtc { get; set; }
    public int IssuedCount { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}
