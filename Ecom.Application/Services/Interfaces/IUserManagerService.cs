using Ecom.Application.DTOs.Auth;
using Ecom.Application.DTOs.Common;
using Ecom.Domain.Entity;

namespace Ecom.Application.Services.Interfaces
{
    public interface IUserManagerService
    {
        // Admin user management methods
        Task<PagedResult<UserManagerDto>> GetUsersAsync(UserSearchDto searchDto);
        Task<UserManagerDto?> GetUserByIdAsync(string userId);
        Task<bool> BlockUserAsync(BlockUserDto blockUserDto);
        Task<bool> UnblockUserAsync(UnblockUserDto unblockUserDto);
        Task<bool> ChangeUserPasswordAsync(ChangeUserPasswordDto changePasswordDto);
        Task<bool> ChangeUserEmailAsync(ChangeUserEmailDto changeEmailDto);
        Task<bool> SendEmailConfirmationAsync(SendEmailConfirmationDto sendEmailDto);
        Task<bool> ConfirmUserEmailAsync(string userId, string token);
        Task<bool> ResetUserPasswordAsync(string userId, string newPassword);
        Task<bool> SendOtpAsync(SendOtpDto sendOtpDto);
        Task<bool> VerifyOtpAsync(VerifyOtpDto verifyOtpDto);
        Task<bool> ChangePasswordWithOtpAsync(ChangePasswordWithOtpDto changePasswordDto);
        
        // Authentication methods
        Task<(bool Success, string UserId, string Message)> RegisterAsync(RegisterDto registerDto);
        Task<(bool Success, AppUsers? User, string Message)> LoginAsync(LoginDto loginDto);
        Task<(bool Success, string Message)> VerifyAccountAsync(VerifyAccountDto verifyDto);
        Task<(bool Success, string Message)> ResendVerificationAsync(ResendVerificationDto resendDto);
        Task<(bool Success, string Message)> ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto);
        Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);
        Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
        Task<(bool Success, string Message)> LogoutAsync(string userId);
        Task<UserResponseDto?> GetUserDetailsAsync(string userId);
        Task<(bool Success, string Message)> UpdateUsernameAsync(string userId, string newUsername);
        Task<(bool Success, string Message)> UpdatePhoneNumberAsync(string userId, string newPhoneNumber);
        
        // Legacy methods for backward compatibility
        Task<(bool Success, string UserId, string Message)> CreateUserAsync(RegisterDto registerDto);
        Task<(bool Success, AppUsers? User, string Message)> ValidateLoginAsync(LoginDto loginDto);
        Task<string?> GetUserByRefreshTokenAsync(string refreshToken);
        Task<bool> SetRefreshTokenAsync(string userId, string refreshToken, DateTime expiresAtUtc);
    }
}
