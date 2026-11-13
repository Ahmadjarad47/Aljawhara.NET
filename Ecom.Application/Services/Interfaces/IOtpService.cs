using Ecom.Domain.Entity;

namespace Ecom.Application.Services.Interfaces;

public interface IOtpService
{
    Task<string?> IssueAsync(AppUsers user, AccountChallengePurpose purpose);
    Task<bool> ConsumeAsync(AppUsers user, AccountChallengePurpose purpose, string code);
}
