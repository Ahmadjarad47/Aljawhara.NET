using Ecom.Domain.Entity;

namespace Ecom.Application.Services.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(AppUsers user);
        Task<string> GenerateTokenAsync(AppUsers user);
        Task<string?> GenerateTokenForUserIdAsync(string userId);
        string GenerateRefreshToken();
        bool ValidateToken(string token);
        bool ValidateRefreshTokenFormat(string refreshToken);
        bool TryDecodeRefreshToken(string refreshToken, out byte[] tokenBytes);
    }
}
