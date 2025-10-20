using Ecom.Application.DTOs.Auth;
using Ecom.Application.DTOs.Common;
using Ecom.Domain.Entity;

namespace Ecom.Application.Services.Interfaces
{
    public interface IUserManagerService
    {
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
        Task<bool> ChangePasswordWithOtpAsync(ChangePasswordDto changePasswordDto);
        Task<(bool Success, string UserId, string Message)> CreateUserAsync(RegisterDto registerDto);
        Task<(bool Success, AppUsers? User, string Message)> ValidateLoginAsync(LoginDto loginDto);
    }
}
