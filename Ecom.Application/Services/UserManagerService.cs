using Ecom.Application.DTOs.Auth;
using Ecom.Application.DTOs.Common;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.WhatsApp;
using System.Linq;

namespace Ecom.Application.Services
{
    public class UserManagerService : IUserManagerService
    {
        private readonly UserManager<AppUsers> _userManager;
        private readonly IEmailService _emailService;
        private readonly IOtpService _otpService;
        private readonly ILogger<UserManagerService> _logger;
        private readonly EcomDbContext _db;
        private readonly AuthSessionService _sessions;
        private readonly GupshupOptions _whatsAppOptions;

        public UserManagerService(
            UserManager<AppUsers> userManager,
            IEmailService emailService,
            IOtpService otpService,
            ILogger<UserManagerService> logger,
            EcomDbContext db,
            AuthSessionService sessions,
            IOptions<GupshupOptions> whatsAppOptions)
        {
            _userManager = userManager;
            _emailService = emailService;
            _otpService = otpService;
            _logger = logger;
            _db = db;
            _sessions = sessions;
            _whatsAppOptions = whatsAppOptions.Value;
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
                    PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                    WhatsAppNotificationsEnabled = user.WhatsAppNotificationsEnabled,
                    WhatsAppConsentPhone = user.WhatsAppConsentPhone,
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

        public async Task<bool> BlockUserAsync(BlockUserDto dto)
        {
            try
            {
                var end = dto.BlockUntil.HasValue
                    ? new DateTimeOffset(dto.BlockUntil.Value).ToUniversalTime()
                    : DateTimeOffset.MaxValue;
                if (end <= DateTimeOffset.UtcNow)
                    return false;

                var blocked = await AccountTransactionAsync(async () =>
                {
                    var user = await _userManager.FindByIdAsync(dto.UserId);
                    if (user == null)
                        return null;
                    user.LockoutEnabled = true;
                    user.LockoutEnd = end;
                    // Persist the lock and a new stamp in the same Identity update.
                    if (!(await _userManager.UpdateSecurityStampAsync(user)).Succeeded)
                        return null;
                    await _sessions.RevokeAsync(user.Id);
                    return user;
                });
                if (blocked == null)
                    return false;
                try { await _emailService.SendUserBlockedNotificationAsync(blocked, dto.Reason, dto.BlockUntil); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not send account block notification"); }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user: {UserId}", dto.UserId);
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

        public Task<bool> ChangeUserPasswordAsync(ChangeUserPasswordDto dto) =>
            ResetUserPasswordAsync(dto.UserId, dto.NewPassword);

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
                    return (true, string.Empty, "If registration is available, check your email for a verification code.");
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
                    if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
                        return (true, string.Empty, "If registration is available, check your email for a verification code.");
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return (false, string.Empty, $"Registration failed: {errors}");
                }

                try { await IssueChallengeAsync(user, AccountChallengePurpose.EmailVerification); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not issue registration challenge"); }

                _logger.LogInformation("User registered successfully: {UserId}", user.Id);
                return (true, user.Id, "If registration is available, check your email for a verification code.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return (false, string.Empty, "An error occurred during registration");
            }
        }

        public async Task<(bool Success, AppUsers? User, string Message)> LoginAsync(LoginDto loginDto)
        {
            const string invalid = "Invalid email or password";
            try
            {
                var user = await _userManager.FindByEmailAsync(loginDto.Email);
                if (user == null || !user.IsActive || user.LockoutEnd > DateTimeOffset.UtcNow)
                    return (false, null, invalid);

                if (!await _userManager.CheckPasswordAsync(user, loginDto.Password))
                {
                    await _userManager.AccessFailedAsync(user);
                    return (false, null, invalid);
                }

                if (!(await _userManager.ResetAccessFailedCountAsync(user)).Succeeded)
                    return (false, null, invalid);
                if (!user.EmailConfirmed)
                    return (false, null, "Please verify your email before logging in");

                return (true, user, "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return (false, null, invalid);
            }
        }

        public async Task<(bool Success, string Message)> VerifyAccountAsync(VerifyAccountDto dto)
        {
            const string invalid = "Invalid or expired verification code";
            try
            {
                return await AccountTransactionAsync(async () =>
                {
                    var user = await _userManager.FindByEmailAsync(dto.Email);
                    if (user == null || !user.IsActive || user.EmailConfirmed || user.LockoutEnd > DateTimeOffset.UtcNow ||
                        !await _otpService.ConsumeAsync(user, AccountChallengePurpose.EmailVerification, dto.Otp))
                        return (false, invalid);
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var result = await _userManager.ConfirmEmailAsync(user, token);
                    return result.Succeeded ? (true, "Account verified successfully") : (false, invalid);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during account verification");
                return (false, invalid);
            }
        }

        public Task<(bool Success, string Message)> ResendVerificationAsync(ResendVerificationDto dto) =>
            IssuePublicChallengeAsync(dto.Email, AccountChallengePurpose.EmailVerification,
                "If the account needs verification, a code has been sent.");

        public async Task<(bool Success, string Message)> ChangePasswordAsync(string userId, ChangePasswordDto dto)
        {
            try
            {
                return await AccountTransactionAsync(async () =>
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user == null || !user.IsActive || user.LockoutEnd > DateTimeOffset.UtcNow)
                        return (false, "Password change failed");

                    var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
                    if (!result.Succeeded)
                        return (false, $"Password change failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");

                    await _sessions.RevokeAsync(user.Id);
                    return (true, "Password changed successfully");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
                return (false, "An error occurred while changing password");
            }
        }

        public Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto dto) =>
            IssuePublicChallengeAsync(dto.Email, AccountChallengePurpose.PasswordRecovery,
                "If the account is eligible, a password reset code has been sent.");

        public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto dto)
        {
            const string invalid = "Invalid or expired reset code";
            try
            {
                return await AccountTransactionAsync(async () =>
                {
                    var user = await _userManager.FindByEmailAsync(dto.Email);
                    if (user == null || !user.IsActive || !user.EmailConfirmed || user.LockoutEnd > DateTimeOffset.UtcNow ||
                        !await _otpService.ConsumeAsync(user, AccountChallengePurpose.PasswordRecovery, dto.Otp))
                        return (false, invalid);

                    // Identity validates the replacement before updating the existing password hash.
                    var result = await ResetAndRevokeAsync(user, dto.NewPassword);
                    if (result.Succeeded)
                        return (true, "Password reset successfully");

                    // The proof was used even if the replacement did not meet password policy.
                    // A database exception rolls back the proof, password and revocations together.
                    return (false, "Password reset failed. Request a new code and use a password that meets the password requirements.");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return (false, "An error occurred while resetting password");
            }
        }

        public async Task LogoutAsync(string userId, Guid sessionId, CancellationToken ct)
        {
            await _sessions.RevokeAsync(userId, sessionId, ct);
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
                    PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                    WhatsAppNotificationsEnabled = user.WhatsAppNotificationsEnabled,
                    WhatsAppConsentPhone = user.WhatsAppConsentPhone,
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

                var normalized = WhatsAppPhone.Normalize(newPhoneNumber, _whatsAppOptions);
                if (normalized == null)
                    return (false, "A valid phone number is required");
                var now = DateTime.UtcNow;
                var revokedPreviousConsent = user.WhatsAppNotificationsEnabled && user.WhatsAppConsentPhone is not null;
                if (revokedPreviousConsent && user.WhatsAppConsentPhone is { } previous)
                {
                    _db.WhatsAppConsentAudits.Add(new WhatsAppConsentAudit
                    {
                        AppUserId = user.Id,
                        Phone = previous,
                        Action = WhatsAppConsentAction.Revoked,
                        Source = "PhoneChanged",
                        OccurredAtUtc = now,
                        ProviderSyncStatus = "NotRequired",
                        CreatedBy = user.Id
                    });
                    await _db.WhatsAppInvoices
                        .Where(i => (i.Status == WhatsAppInvoiceStatus.Pending || i.Status == WhatsAppInvoiceStatus.Preparing) &&
                            i.PaymentAttempt.Order.WhatsAppConsentPhone == previous)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(i => i.Status, WhatsAppInvoiceStatus.Skipped)
                            .SetProperty(i => i.LastErrorCode, "CUSTOMER_CONSENT_REVOKED")
                            .SetProperty(i => i.LeaseOwner, (string?)null)
                            .SetProperty(i => i.LeaseUntilUtc, (DateTime?)null));
                }
                user.PhoneNumber = normalized;
                user.PhoneNumberConfirmed = false;
                user.WhatsAppNotificationsEnabled = false;
                user.WhatsAppConsentPhone = null;
                user.WhatsAppConsentAtUtc = null;
                if (revokedPreviousConsent) user.WhatsAppConsentRevokedAtUtc = now;
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
        public async Task<bool> SendEmailConfirmationAsync(SendEmailConfirmationDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.UserId);
            return user != null && await IssueChallengeAsync(user, AccountChallengePurpose.EmailVerification);
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
                return await AccountTransactionAsync(async () =>
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    return user != null && (await ResetAndRevokeAsync(user, newPassword)).Succeeded;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user: {UserId}", userId);
                return false;
            }
        }

        private async Task<IdentityResult> ResetAndRevokeAsync(AppUsers user, string newPassword)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (result.Succeeded)
                await _sessions.RevokeAsync(user.Id);
            return result;
        }

        private Task<T> AccountTransactionAsync<T>(Func<Task<T>> operation) =>
            _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                // A retry must reload the Identity row and its concurrency stamp.
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync();
                    return result;
                }
                finally
                {
                    _db.ChangeTracker.Clear();
                }
            });

        private async Task<(bool Success, string Message)> IssuePublicChallengeAsync(
            string email, AccountChallengePurpose purpose, string message)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user != null)
                    await IssueChallengeAsync(user, purpose);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error issuing account challenge");
            }
            return (true, message);
        }

        private async Task<bool> IssueChallengeAsync(AppUsers user, AccountChallengePurpose purpose)
        {
            if (!user.IsActive || user.LockoutEnd > DateTimeOffset.UtcNow ||
                user.EmailConfirmed != (purpose == AccountChallengePurpose.PasswordRecovery))
                return false;

            var code = await _otpService.IssueAsync(user, purpose);
            if (code == null)
                return false;

            return purpose == AccountChallengePurpose.PasswordRecovery
                ? await _emailService.SendPasswordResetEmailAsync(user, code)
                : await _emailService.SendOtpEmailAsync(user, code);
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


    }
}
