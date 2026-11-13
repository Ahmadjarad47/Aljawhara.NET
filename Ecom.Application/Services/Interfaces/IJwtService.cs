using Ecom.Domain.Entity;

namespace Ecom.Application.Services.Interfaces;

public interface IJwtService
{
    Task<string> GenerateTokenAsync(AppUsers user, Guid sessionId, DateTime expiresAtUtc);
}
