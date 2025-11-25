using Ecom.Domain.Entity;

namespace Ecom.Application.Services.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailConfirmationAsync(AppUsers user, string confirmationToken);
        Task<bool> SendPasswordResetEmailAsync(AppUsers user, string resetToken);
        Task<bool> SendEmailChangeConfirmationAsync(AppUsers user, string newEmail, string confirmationToken);
        Task<bool> SendUserBlockedNotificationAsync(AppUsers user, string reason, DateTime? blockUntil);
        Task<bool> SendUserUnblockedNotificationAsync(AppUsers user);
        Task<bool> SendOtpEmailAsync(AppUsers user, string otp);
        Task<bool> SendPasswordChangeConfirmationAsync(AppUsers user);
        Task<bool> SendContactEmailAsync(string name, string email, string? phoneNumber, string subject, string message);
    }
}
