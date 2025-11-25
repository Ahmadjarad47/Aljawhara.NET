using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ecom.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailConfirmationAsync(AppUsers user, string confirmationToken)
        {
            try
            {
                var confirmationLink = $"{_configuration["AppSettings:BaseUrl"]}/api/auth/confirm-email?userId={user.Id}&token={confirmationToken}";
                
                var subject = "Confirm Your Email - Ecom Taani";
                var body = $@"
                    <html>
                    <body>
                        <h2>Welcome to Ecom Taani!</h2>
                        <p>Dear {user.UserName},</p>
                        <p>Thank you for registering with Ecom Taani. Please confirm your email address by clicking the link below:</p>
                        <p><a href='{confirmationLink}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Confirm Email</a></p>
                        <p>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p>{confirmationLink}</p>
                        <p>This link will expire in 24 hours.</p>
                        <p>Best regards,<br>Ecom Taani Team</p>
                    </body>
                    </html>";

                return await SendEmailAsync(user.Email!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email confirmation to user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(AppUsers user, string resetToken)
        {
            try
            {
                var resetLink = $"{_configuration["AppSettings:BaseUrl"]}/api/auth/reset-password?userId={user.Id}&token={resetToken}";
                
                var subject = "Reset Your Password - Ecom Taani";
                var body = $@"
                    <html>
                    <body>
                        <h2>Password Reset Request</h2>
                        <p>Dear {user.UserName},</p>
                        <p>You have requested to reset your password. Click the link below to reset your password:</p>
                        <p><a href='{resetLink}' style='background-color: #dc3545; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Reset Password</a></p>
                        <p>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p>{resetLink}</p>
                        <p>This link will expire in 1 hour.</p>
                        <p>If you didn't request this password reset, please ignore this email.</p>
                        <p>Best regards,<br>Ecom Taani Team</p>
                    </body>
                    </html>";

                return await SendEmailAsync(user.Email!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendEmailChangeConfirmationAsync(AppUsers user, string newEmail, string confirmationToken)
        {
            try
            {
                var confirmationLink = $"{_configuration["AppSettings:BaseUrl"]}/api/auth/confirm-email-change?userId={user.Id}&newEmail={newEmail}&token={confirmationToken}";
                
                var subject = "Confirm Email Change - Ecom Taani";
                var body = $@"
                    <html>
                    <body>
                        <h2>Email Change Confirmation</h2>
                        <p>Dear {user.UserName},</p>
                        <p>You have requested to change your email address to {newEmail}. Please confirm this change by clicking the link below:</p>
                        <p><a href='{confirmationLink}' style='background-color: #28a745; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Confirm Email Change</a></p>
                        <p>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p>{confirmationLink}</p>
                        <p>This link will expire in 24 hours.</p>
                        <p>If you didn't request this email change, please contact support immediately.</p>
                        <p>Best regards,<br>Ecom Taani Team</p>
                    </body>
                    </html>";

                return await SendEmailAsync(newEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email change confirmation to user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendUserBlockedNotificationAsync(AppUsers user, string reason, DateTime? blockUntil)
        {
            try
            {
                var subject = "Account Blocked - Ecom Taani";
                var blockDuration = blockUntil.HasValue ? $" until {blockUntil.Value:yyyy-MM-dd HH:mm:ss}" : " permanently";
                var body = $@"
                    <html>
                    <body>
                        <h2>Account Blocked</h2>
                        <p>Dear {user.UserName},</p>
                        <p>Your account has been blocked{blockDuration}.</p>
                        <p><strong>Reason:</strong> {reason}</p>
                        <p>If you believe this is an error, please contact our support team.</p>
                        <p>Best regards,<br>Ecom Taani Team</p>
                    </body>
                    </html>";

                return await SendEmailAsync(user.Email!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user blocked notification to user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendUserUnblockedNotificationAsync(AppUsers user)
        {
            try
            {
                var subject = "Account Unblocked - Ecom Taani";
                var body = $@"
                    <html>
                    <body>
                        <h2>Account Unblocked</h2>
                        <p>Dear {user.UserName},</p>
                        <p>Your account has been unblocked and you can now access all features of Ecom Taani.</p>
                        <p>Welcome back!</p>
                        <p>Best regards,<br>Ecom Taani Team</p>
                    </body>
                    </html>";

                return await SendEmailAsync(user.Email!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user unblocked notification to user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendOtpEmailAsync(AppUsers user, string otp)
        {
            try
            {
                var subject = "رمز التحقق - المتجر الجوهرة";
                var body = $@"
                    <html dir='rtl'>
                    <body style='font-family: Arial, sans-serif; text-align: right;'>
                        <h2>رمز التحقق</h2>
                        <p>عزيزي/عزيزتي {user.UserName},</p>
                        <p>رمز التحقق الخاص بك هو:</p>
                        <div style='background-color: #f8f9fa; padding: 20px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                            <h1 style='color: #007bff; font-size: 32px; margin: 0; letter-spacing: 5px;'>{otp}</h1>
                        </div>
                        <p>هذا الرمز صالح لمدة ساعة واحدة فقط.</p>
                        <p>إذا لم تطلب هذا الرمز، يرجى تجاهل هذه الرسالة.</p>
                        <p>مع أطيب التحيات،<br>فريق المتجر الجوهرة</p>
                    </body>
                    </html>";

                return await SendEmailAsync(user.Email!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP email to user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendPasswordChangeConfirmationAsync(AppUsers user)
        {
            try
            {
                var subject = "تأكيد تغيير كلمة المرور - المتجر الجوهرة";
                var body = $@"
                    <html dir='rtl'>
                    <body style='font-family: Arial, sans-serif; text-align: right;'>
                        <h2>تم تغيير كلمة المرور بنجاح</h2>
                        <p>عزيزي/عزيزتي {user.UserName},</p>
                        <p>تم تغيير كلمة المرور الخاصة بحسابك بنجاح.</p>
                        <p>إذا لم تقم بتغيير كلمة المرور، يرجى الاتصال بنا فوراً.</p>
                        <p>مع أطيب التحيات،<br>فريق المتجر الجوهرة</p>
                    </body>
                    </html>";

                return await SendEmailAsync(user.Email!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password change confirmation to user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendContactEmailAsync(string name, string email, string? phoneNumber, string subject, string message)
        {
            try
            {
                var contactSubject = $"Contact Form Submission: {subject}";
                var phoneInfo = !string.IsNullOrWhiteSpace(phoneNumber) ? $"<p><strong>Phone Number:</strong> {phoneNumber}</p>" : "";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>New Contact Form Submission</h2>
                        <p><strong>Name:</strong> {name}</p>
                        <p><strong>Email:</strong> {email}</p>
                        {phoneInfo}
                        <p><strong>Subject:</strong> {subject}</p>
                        <hr>
                        <p><strong>Message:</strong></p>
                        <p style='white-space: pre-wrap;'>{message}</p>
                        <hr>
                        <p><em>This email was sent from the contact form on the website.</em></p>
                    </body>
                    </html>";

                return await SendEmailAsync("info@aljawhara.com", contactSubject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending contact email from {Email}", email);
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var smtpHost = smtpSettings["Host"];
                var smtpPort = int.Parse(smtpSettings["Port"] ?? "587");
                var smtpUsername = smtpSettings["Username"];
                var smtpPassword = smtpSettings["Password"];
                var fromEmail = smtpSettings["FromEmail"];
                var fromName = smtpSettings["FromName"] ?? "المتجر الجوهرة";

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(smtpUsername, smtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Email}", toEmail);
                return false;
            }
        }
    }
}
