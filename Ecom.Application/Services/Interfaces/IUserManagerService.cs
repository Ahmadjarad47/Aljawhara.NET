using Ecom.Application.DTOs.Auth;
using Ecom.Application.DTOs.Common;

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
    }
}
