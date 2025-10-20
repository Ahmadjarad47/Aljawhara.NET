using Ecom.Application.DTOs.Auth;
using Ecom.Application.DTOs.Common;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Ecom.Application.Services
{
    public class UserManagerService : IUserManagerService
    {
        private readonly UserManager<AppUsers> _userManager;
        private readonly IEmailService _emailService;
        private readonly IOtpService _otpService;
        private readonly ILogger<UserManagerService> _logger;

        public UserManagerService(
            UserManager<AppUsers> userManager,
            IEmailService emailService,
            IOtpService otpService,
            ILogger<UserManagerService> logger)
        {
            _userManager = userManager;
            _emailService = emailService;
            _otpService = otpService;
            _logger = logger;
        }

        public async Task<PagedResult<UserManagerDto>> GetUsersAsync(UserSearchDto searchDto)
        {
            try
            {
                var query = _userManager.Users.AsQueryable();

                // Apply search filters
                if (!string.IsNullOrEmpty(searchDto.SearchTerm))
                {
                    query = query.Where(u => 
                        u.UserName!.Contains(searchDto.SearchTerm) ||
                        u.Email!.Contains(searchDto.SearchTerm) ||
                        u.PhoneNumber!.Contains(searchDto.SearchTerm));
                }

                if (searchDto.IsBlocked.HasValue)
                {
                    if (searchDto.IsBlocked.Value)
                    {
                        query = query.Where(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow);
                    }
                    else
                    {
                        query = query.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.UtcNow);
                    }
                }

                if (searchDto.EmailConfirmed.HasValue)
                {
                    query = query.Where(u => u.EmailConfirmed == searchDto.EmailConfirmed.Value);
                }

                var totalCount = await query.CountAsync();

                var users = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((searchDto.Page - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .Select(u => new UserManagerDto
                    {
                        Id = u.Id,
                        Username = u.UserName!,
                        Email = u.Email!,
                        PhoneNumber = u.PhoneNumber!,
                        EmailConfirmed = u.EmailConfirmed,
                        IsBlocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                        LockoutEnd = u.LockoutEnd.HasValue ? u.LockoutEnd.Value.DateTime : null,
                        CreatedAt = u.CreatedAt,
                        LastLoginAt = null, // This would need to be tracked separately
                        AccessFailedCount = u.AccessFailedCount,
                        TwoFactorEnabled = u.TwoFactorEnabled
                    })
                    .ToListAsync();

                return new PagedResult<UserManagerDto>
                {
                    Items = users,
                    TotalCount = totalCount,
                    Page = searchDto.Page,
                    PageSize = searchDto.PageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / searchDto.PageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users with search criteria");
                throw;
            }
        }

        public async Task<UserManagerDto?> GetUserByIdAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return null;

                return new UserManagerDto
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    PhoneNumber = user.PhoneNumber!,
                    EmailConfirmed = user.EmailConfirmed,
                    IsBlocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                    LockoutEnd = user.LockoutEnd?.DateTime,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = null, // This would need to be tracked separately
                    AccessFailedCount = user.AccessFailedCount,
                    TwoFactorEnabled = user.TwoFactorEnabled
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> BlockUserAsync(BlockUserDto blockUserDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(blockUserDto.UserId);
                if (user == null)
                    return false;

                var lockoutEnd = blockUserDto.BlockUntil.HasValue 
                    ? DateTimeOffset.FromUnixTimeSeconds(((DateTimeOffset)blockUserDto.BlockUntil).ToUnixTimeSeconds())
                    : DateTimeOffset.MaxValue;

                var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
                
                if (result.Succeeded)
                {
                    await _emailService.SendUserBlockedNotificationAsync(user, blockUserDto.Reason, blockUserDto.BlockUntil);
                    _logger.LogInformation("User {UserId} blocked successfully", blockUserDto.UserId);
                }

                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user: {UserId}", blockUserDto.UserId);
                return false;
            }
        }

        public async Task<bool> UnblockUserAsync(UnblockUserDto unblockUserDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(unblockUserDto.UserId);
                if (user == null)
                    return false;

                var result = await _userManager.SetLockoutEndDateAsync(user, null);
                
                if (result.Succeeded)
                {
                    // Reset access failed count
                    await _userManager.ResetAccessFailedCountAsync(user);
                    await _emailService.SendUserUnblockedNotificationAsync(user);
                    _logger.LogInformation("User {UserId} unblocked successfully", unblockUserDto.UserId);
                }

                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking user: {UserId}", unblockUserDto.UserId);
                return false;
            }
        }

        public async Task<bool> ChangeUserPasswordAsync(ChangeUserPasswordDto changePasswordDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(changePasswordDto.UserId);
                if (user == null)
                    return false;

                // Remove current password
                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                    return false;

                // Add new password
                var addPasswordResult = await _userManager.AddPasswordAsync(user, changePasswordDto.NewPassword);
                
                if (addPasswordResult.Succeeded)
                {
                    _logger.LogInformation("Password changed successfully for user: {UserId}", changePasswordDto.UserId);
                }

                return addPasswordResult.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", changePasswordDto.UserId);
                return false;
            }
        }

        public async Task<bool> ChangeUserEmailAsync(ChangeUserEmailDto changeEmailDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(changeEmailDto.UserId);
                if (user == null)
                    return false;

                // Generate email confirmation token
                var token = await _userManager.GenerateChangeEmailTokenAsync(user, changeEmailDto.NewEmail);
                
                // Send confirmation email
                var emailSent = await _emailService.SendEmailChangeConfirmationAsync(user, changeEmailDto.NewEmail, token);
                
                if (emailSent)
                {
                    _logger.LogInformation("Email change confirmation sent to user: {UserId}", changeEmailDto.UserId);
                }

                return emailSent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing email for user: {UserId}", changeEmailDto.UserId);
                return false;
            }
        }

        public async Task<bool> SendEmailConfirmationAsync(SendEmailConfirmationDto sendEmailDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(sendEmailDto.UserId);
                if (user == null)
                    return false;

                if (user.EmailConfirmed)
                {
                    _logger.LogWarning("User {UserId} email is already confirmed", sendEmailDto.UserId);
                    return false;
                }

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var emailSent = await _emailService.SendEmailConfirmationAsync(user, token);
                
                if (emailSent)
                {
                    _logger.LogInformation("Email confirmation sent to user: {UserId}", sendEmailDto.UserId);
                }

                return emailSent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email confirmation to user: {UserId}", sendEmailDto.UserId);
                return false;
            }
        }

        public async Task<bool> ConfirmUserEmailAsync(string userId, string token)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return false;

                var result = await _userManager.ConfirmEmailAsync(user, token);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("Email confirmed successfully for user: {UserId}", userId);
                }

                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming email for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ResetUserPasswordAsync(string userId, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return false;

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("Password reset successfully for user: {UserId}", userId);
                }

                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> SendOtpAsync(SendOtpDto sendOtpDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(sendOtpDto.Email);
                if (user == null)
                {
                    _logger.LogWarning("User not found for email: {Email}", sendOtpDto.Email);
                    return false;
                }

                var otp = await _otpService.GenerateOtpAsync(sendOtpDto.Email);
                var emailSent = await _emailService.SendOtpEmailAsync(user, otp);
                
                if (emailSent)
                {
                    _logger.LogInformation("OTP sent successfully to email: {Email}", sendOtpDto.Email);
                }

                return emailSent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP to email: {Email}", sendOtpDto.Email);
                return false;
            }
        }

        public async Task<bool> VerifyOtpAsync(VerifyOtpDto verifyOtpDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(verifyOtpDto.Email);
                if (user == null)
                {
                    _logger.LogWarning("User not found for email: {Email}", verifyOtpDto.Email);
                    return false;
                }

                var isValid = await _otpService.VerifyOtpAsync(verifyOtpDto.Email, verifyOtpDto.Otp);
                
                if (isValid)
                {
                    _logger.LogInformation("OTP verified successfully for email: {Email}", verifyOtpDto.Email);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for email: {Email}", verifyOtpDto.Email);
                return false;
            }
        }

        public async Task<bool> ChangePasswordWithOtpAsync(ChangePasswordDto changePasswordDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(changePasswordDto.Email);
                if (user == null)
                {
                    _logger.LogWarning("User not found for email: {Email}", changePasswordDto.Email);
                    return false;
                }

                // Verify OTP first
                var isOtpValid = await _otpService.VerifyOtpAsync(changePasswordDto.Email, changePasswordDto.Otp);
                if (!isOtpValid)
                {
                    _logger.LogWarning("Invalid OTP for email: {Email}", changePasswordDto.Email);
                    return false;
                }

                // Remove current password
                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                {
                    _logger.LogError("Failed to remove current password for user: {UserId}", user.Id);
                    return false;
                }

                // Add new password
                var addPasswordResult = await _userManager.AddPasswordAsync(user, changePasswordDto.NewPassword);
                
                if (addPasswordResult.Succeeded)
                {
                    // Send confirmation email
                    await _emailService.SendPasswordChangeConfirmationAsync(user);
                    _logger.LogInformation("Password changed successfully for user: {UserId}", user.Id);
                }

                return addPasswordResult.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for email: {Email}", changePasswordDto.Email);
                return false;
            }
        }

        public async Task<(bool Success, string UserId, string Message)> CreateUserAsync(RegisterDto registerDto)
        {
            try
            {
                var user = new AppUsers
                {
                    UserName = registerDto.Username,
                    Email = registerDto.Email,
                    PhoneNumber = registerDto.PhoneNumber,
                    EmailConfirmed = false
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);

                if (result.Succeeded)
                {
                    // Send email verification
                    var emailSent = await SendEmailConfirmationAsync(new SendEmailConfirmationDto { UserId = user.Id });
                    
                    if (emailSent)
                    {
                        return (true, user.Id, "User registered successfully. Please check your email to verify your account.");
                    }
                    else
                    {
                        return (true, user.Id, "User registered successfully, but failed to send verification email. Please contact support.");
                    }
                }

                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, string.Empty, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with email: {Email}", registerDto.Email);
                return (false, string.Empty, "An error occurred while creating the user.");
            }
        }

        public async Task<(bool Success, AppUsers? User, string Message)> ValidateLoginAsync(LoginDto loginDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(loginDto.Email);
                if (user == null)
                {
                    return (false, null, "Invalid email or password");
                }

                var result = await _userManager.CheckPasswordAsync(user, loginDto.Password);
                if (!result)
                {
                    return (false, null, "Invalid email or password");
                }

                // Check if email is verified
                if (!user.EmailConfirmed)
                {
                    // Send verification email
                    var emailSent = await SendEmailConfirmationAsync(new SendEmailConfirmationDto { UserId = user.Id });
                    
                    var message = emailSent 
                        ? "Please verify your email address before logging in. A verification email has been sent to your email."
                        : "Please verify your email address before logging in. Failed to send verification email, please contact support.";
                    
                    return (false, null, message);
                }

                return (true, user, "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating login for email: {Email}", loginDto.Email);
                return (false, null, "An error occurred while validating login.");
            }
        }
    }
}
