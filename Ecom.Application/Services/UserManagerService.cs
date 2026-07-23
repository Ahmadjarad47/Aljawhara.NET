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

        // Authentication Methods
        public async Task<(bool Success, string UserId, string Message)> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
                if (existingUser != null)
                {
                    return (false, string.Empty, "User with this email already exists");
                }

                // Check if username already exists
                var existingUsername = await _userManager.FindByNameAsync(registerDto.Username);
                if (existingUsername != null)
                {
                    return (false, string.Empty, "Username is already taken");
                }

                // Create new user
                var user = new AppUsers
                {
                    UserName = registerDto.Username,
                    Email = registerDto.Email,
                    PhoneNumber = registerDto.PhoneNumber,
                    EmailConfirmed = false,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);
                
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return (false, string.Empty, $"Registration failed: {errors}");
                }

                // Generate and send OTP for email verification
                var otp = await _otpService.GenerateOtpAsync(registerDto.Email);
                await _emailService.SendOtpEmailAsync(user, otp);

                _logger.LogInformation("User registered successfully: {UserId}", user.Id);
                return (true, user.Id, "Registration successful. Please check your email for verification code.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return (false, string.Empty, "An error occurred during registration");
            }
        }

        public async Task<(bool Success, AppUsers? User, string Message)> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(loginDto.Email);
                if (user == null)
                {
                    return (false, null, "Invalid email or password");
                }

                // Check if user is blocked
                if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
                {
                    return (false, null, "Account is blocked. Please contact support.");
                }

                // Check if email is confirmed
                if (!user.EmailConfirmed)
                {
                    var newResendVerificationDto = new ResendVerificationDto
                    {
                        Email = loginDto.Email,
                    };
                    await ResendVerificationAsync(newResendVerificationDto);
                    return (false, null, "Please verify your email before logging in");
                }

                var isValidPassword = await _userManager.CheckPasswordAsync(user, loginDto.Password);
                if (!isValidPassword)
                {
                    // Increment access failed count
                    await _userManager.AccessFailedAsync(user);
                    return (false, null, "Invalid email or password");
                }

                // Reset access failed count on successful login
                await _userManager.ResetAccessFailedCountAsync(user);

                _logger.LogInformation("User logged in successfully: {UserId}", user.Id);
                return (true, user, "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return (false, null, "An error occurred during login");
            }
        }

        public async Task<(bool Success, string Message)> VerifyAccountAsync(VerifyAccountDto verifyDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(verifyDto.Email);
                if (user == null)
                {
                    return (false, "User not found");
                }

                if (user.EmailConfirmed)
                {
                    return (false, "Account is already verified");
                }

                var isOtpValid = await _otpService.VerifyOtpAsync(verifyDto.Email, verifyDto.Otp);
                if (!isOtpValid)
                {
                    return (false, "Invalid or expired verification code");
                }

                // Confirm email
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var result = await _userManager.ConfirmEmailAsync(user, token);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return (false, $"Email confirmation failed: {errors}");
                }

                _logger.LogInformation("Account verified successfully: {UserId}", user.Id);
                return (true, "Account verified successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during account verification");
                return (false, "An error occurred during verification");
            }
        }

        public async Task<(bool Success, string Message)> ResendVerificationAsync(ResendVerificationDto resendDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(resendDto.Email);
                if (user == null)
                {
                    return (false, "User not found");
                }

                if (user.EmailConfirmed)
                {
                    return (false, "Account is already verified");
                }

                // Generate and send new OTP
                var otp = await _otpService.GenerateOtpAsync(resendDto.Email);
                await _emailService.SendOtpEmailAsync(user, otp);

                _logger.LogInformation("Verification code resent to: {Email}", resendDto.Email);
                return (true, "Verification code has been resent to your email");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending verification code");
                return (false, "An error occurred while resending verification code");
            }
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return (false, "User not found");
                }

                var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
                
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return (false, $"Password change failed: {errors}");
                }

                _logger.LogInformation("Password changed successfully for user: {UserId}", userId);
                return (true, "Password changed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
                return (false, "An error occurred while changing password");
            }
        }

        public async Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
                if (user == null)
                {
                    // Don't reveal if user exists or not for security
                    return (true, "If the email exists, a password reset code has been sent");
                }

                // Generate and send OTP for password reset
                var otp = await _otpService.GenerateOtpAsync(forgotPasswordDto.Email);
                await _emailService.SendPasswordResetEmailAsync(user, otp);

                _logger.LogInformation("Password reset code sent to: {Email}", forgotPasswordDto.Email);
                return (true, "If the email exists, a password reset code has been sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset code");
                return (false, "An error occurred while sending password reset code");
            }
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
                if (user == null)
                {
                    return (false, "User not found");
                }

                var isOtpValid = await _otpService.VerifyOtpAsync(resetPasswordDto.Email, resetPasswordDto.Otp);
                if (!isOtpValid)
                {
                    return (false, "Invalid or expired reset code");
                }

                // Remove current password and set new one
                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                {
                    return (false, "Failed to reset password");
                }

                var addPasswordResult = await _userManager.AddPasswordAsync(user, resetPasswordDto.NewPassword);
                if (!addPasswordResult.Succeeded)
                {
                    var errors = string.Join(", ", addPasswordResult.Errors.Select(e => e.Description));
                    return (false, $"Password reset failed: {errors}");
                }

                _logger.LogInformation("Password reset successfully for user: {UserId}", user.Id);
                return (true, "Password reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return (false, "An error occurred while resetting password");
            }
        }

        public async Task<(bool Success, string Message)> LogoutAsync(string userId)
        {
            try
            {
                // In a real application, you might want to:
                // 1. Invalidate refresh tokens
                // 2. Log the logout event
                // 3. Clear any cached user data
                
                _logger.LogInformation("User logged out: {UserId}", userId);
                return (true, "Logged out successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for user: {UserId}", userId);
                return (false, "An error occurred during logout");
            }
        }

        public async Task<UserResponseDto?> GetUserDetailsAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return null;
                }

                return new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    EmailConfirmed = user.EmailConfirmed,
                    IsActive = !user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow,
                    CreatedAt = user.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user details for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<(bool Success, string Message)> UpdateUsernameAsync(string userId, string newUsername)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return (false, "User not found");
                }

                var existing = await _userManager.FindByNameAsync(newUsername);
                if (existing != null && existing.Id != userId)
                {
                    return (false, "Username is already taken");
                }

                user.UserName = newUsername;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return (false, $"Failed to update username: {errors}");
                }

                _logger.LogInformation("Username updated for user: {UserId}", userId);
                return (true, "Username updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating username for user: {UserId}", userId);
                return (false, "An error occurred while updating username");
            }
        }

        public async Task<(bool Success, string Message)> UpdatePhoneNumberAsync(string userId, string newPhoneNumber)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return (false, "User not found");
                }

                user.PhoneNumber = newPhoneNumber;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return (false, $"Failed to update phone number: {errors}");
                }

                _logger.LogInformation("Phone number updated for user: {UserId}", userId);
                return (true, "Phone number updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating phone number for user: {UserId}", userId);
                return (false, "An error occurred while updating phone number");
            }
        }

        // Missing interface implementations
        public async Task<bool> SendEmailConfirmationAsync(SendEmailConfirmationDto sendEmailDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(sendEmailDto.UserId);
                if (user == null)
                    return false;

                var otp = await _otpService.GenerateOtpAsync(user.Email!);
                await _emailService.SendOtpEmailAsync(user, otp);
                
                _logger.LogInformation("Email confirmation sent to user: {UserId}", sendEmailDto.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email confirmation for user: {UserId}", sendEmailDto.UserId);
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

                // Remove current password
                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                    return false;

                // Add new password
                var addPasswordResult = await _userManager.AddPasswordAsync(user, newPassword);
                
                if (addPasswordResult.Succeeded)
                {
                    _logger.LogInformation("Password reset successfully for user: {UserId}", userId);
                }

                return addPasswordResult.Succeeded;
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
                    return false;

                var otp = await _otpService.GenerateOtpAsync(sendOtpDto.Email);
                await _emailService.SendOtpEmailAsync(user, otp);
                
                _logger.LogInformation("OTP sent to: {Email}", sendOtpDto.Email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP to: {Email}", sendOtpDto.Email);
                return false;
            }
        }

        public async Task<bool> VerifyOtpAsync(VerifyOtpDto verifyOtpDto)
        {
            try
            {
                var isValid = await _otpService.VerifyOtpAsync(verifyOtpDto.Email, verifyOtpDto.Otp);
                
                if (isValid)
                {
                    _logger.LogInformation("OTP verified successfully for: {Email}", verifyOtpDto.Email);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for: {Email}", verifyOtpDto.Email);
                return false;
            }
        }

        public async Task<bool> ChangePasswordWithOtpAsync(ChangePasswordWithOtpDto changePasswordDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(changePasswordDto.Email);
                if (user == null)
                    return false;

                var isOtpValid = await _otpService.VerifyOtpAsync(changePasswordDto.Email, changePasswordDto.Otp);
                if (!isOtpValid)
                    return false;

                // Remove current password and set new one
                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                    return false;

                var addPasswordResult = await _userManager.AddPasswordAsync(user, changePasswordDto.NewPassword);
                
                if (addPasswordResult.Succeeded)
                {
                    _logger.LogInformation("Password changed with OTP successfully for user: {UserId}", user.Id);
                }

                return addPasswordResult.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password with OTP for: {Email}", changePasswordDto.Email);
                return false;
            }
        }

        // Legacy methods for backward compatibility
        public async Task<(bool Success, string UserId, string Message)> CreateUserAsync(RegisterDto registerDto)
        {
            return await RegisterAsync(registerDto);
        }

        public async Task<(bool Success, AppUsers? User, string Message)> ValidateLoginAsync(LoginDto loginDto)
        {
            return await LoginAsync(loginDto);
        }

        public async Task<string?> GetUserByRefreshTokenAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return null;
                }

                var now = DateTime.UtcNow;
                var user = await _userManager.Users
                    .Where(u => u.RefreshToken == refreshToken && u.RefreshTokenExpiresAtUtc.HasValue && u.RefreshTokenExpiresAtUtc > now)
                    .FirstOrDefaultAsync();

                return user?.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by refresh token");
                return null;
            }
        }

        public async Task<bool> SetRefreshTokenAsync(string userId, string refreshToken, DateTime expiresAtUtc)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiresAtUtc = expiresAtUtc;
                var result = await _userManager.UpdateAsync(user);
                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting refresh token for user: {UserId}", userId);
                return false;
            }
        }
    }
}
