using Ecom.Domain.Entity;

namespace Ecom.Application.Services.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(AppUsers user);
        Task<string> GenerateTokenAsync(AppUsers user);
        string GenerateRefreshToken();
        bool ValidateToken(string token);
    }
}
