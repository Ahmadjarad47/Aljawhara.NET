using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
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
                var confirmationLink = $"{_configuration["AppSettings:BaseUrl"]}/auth/verify?userId={user.Id}&token={confirmationToken}";
                
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
                var resetLink = $"{_configuration["AppSettings:BaseUrl"]}/auth/reset-password?userId={user.Id}&token={resetToken}";
                
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

        public async Task<bool> SendOrderNotificationToAdminAsync(OrderDto order, string eventType)
        {
            try
            {
                var subject = eventType == "Created" 
                    ? $"New Order Created - {order.OrderNumber}" 
                    : $"Order Status Updated - {order.OrderNumber}";

                var itemsHtml = string.Join("", order.Items.Select(item => $@"
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Name}</td>
                        <td style='padding: 10px; border: 1px solid #ddd; text-align: center;'>{item.Quantity}</td>
                        <td style='padding: 10px; border: 1px solid #ddd; text-align: right;'>{item.Price:C}</td>
                        <td style='padding: 10px; border: 1px solid #ddd; text-align: right;'>{item.Total:C}</td>
                    </tr>"));

                var statusColor = order.Status switch
                {
                    OrderStatus.Pending => "#ffc107",
                    OrderStatus.Processing => "#17a2b8",
                    OrderStatus.Shipped => "#007bff",
                    OrderStatus.Delivered => "#28a745",
                    OrderStatus.Cancelled => "#dc3545",
                    OrderStatus.Refunded => "#6c757d",
                    _ => "#6c757d"
                };

                var eventDescription = eventType == "Created" 
                    ? $"A new order has been created by {order.CustomerName}." 
                    : $"The order status has been updated to <strong style='color: {statusColor};'>{order.Status}</strong>.";

                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 800px; margin: 0 auto; padding: 20px;'>
                            <h2 style='color: #007bff; border-bottom: 2px solid #007bff; padding-bottom: 10px;'>
                                Order Notification - {order.OrderNumber}
                            </h2>
                            
                            <p style='font-size: 16px;'>{eventDescription}</p>
                            
                            <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <h3 style='margin-top: 0; color: #495057;'>Order Details</h3>
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold; width: 200px;'>Order Number:</td>
                                        <td style='padding: 8px;'>{order.OrderNumber}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold;'>Customer Name:</td>
                                        <td style='padding: 8px;'>{order.CustomerName}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold;'>Status:</td>
                                        <td style='padding: 8px;'>
                                            <span style='background-color: {statusColor}; color: white; padding: 5px 10px; border-radius: 3px; font-weight: bold;'>
                                                {order.Status}
                                            </span>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold;'>Order Date:</td>
                                        <td style='padding: 8px;'>{order.CreatedAt:yyyy-MM-dd HH:mm:ss}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold;'>Subtotal:</td>
                                        <td style='padding: 8px;'>{order.Subtotal:C}</td>
                                    </tr>
                                    {(order.CouponDiscountAmount.HasValue && order.CouponDiscountAmount > 0 ? $@"
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold;'>Coupon ({order.CouponCode}):</td>
                                        <td style='padding: 8px; color: #28a745;'>-{order.CouponDiscountAmount:C}</td>
                                    </tr>" : "")}
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold;'>Shipping:</td>
                                        <td style='padding: 8px;'>{order.Shipping:C}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold;'>Tax:</td>
                                        <td style='padding: 8px;'>{order.Tax:C}</td>
                                    </tr>
                                    <tr style='background-color: #e9ecef;'>
                                        <td style='padding: 8px; font-weight: bold; font-size: 16px;'>Total:</td>
                                        <td style='padding: 8px; font-weight: bold; font-size: 16px; color: #007bff;'>{order.Total:C}</td>
                                    </tr>
                                </table>
                            </div>

                            <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <h3 style='margin-top: 0; color: #495057;'>Order Items</h3>
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <thead>
                                        <tr style='background-color: #007bff; color: white;'>
                                            <th style='padding: 10px; border: 1px solid #ddd; text-align: left;'>Product</th>
                                            <th style='padding: 10px; border: 1px solid #ddd; text-align: center;'>Quantity</th>
                                            <th style='padding: 10px; border: 1px solid #ddd; text-align: right;'>Price</th>
                                            <th style='padding: 10px; border: 1px solid #ddd; text-align: right;'>Total</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {itemsHtml}
                                    </tbody>
                                </table>
                            </div>

                            <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <h3 style='margin-top: 0; color: #495057;'>Shipping Address</h3>
                                <p style='margin: 5px 0;'><strong>{order.ShippingAddress.FullName}</strong></p>
                                <p style='margin: 5px 0;'>{order.ShippingAddress.Street}</p>
                                <p style='margin: 5px 0;'>
                                    {order.ShippingAddress.City}
                                    {(!string.IsNullOrEmpty(order.ShippingAddress.State) ? $", {order.ShippingAddress.State}" : "")}
                                    {order.ShippingAddress.PostalCode}
                                </p>
                                <p style='margin: 5px 0;'>{order.ShippingAddress.Country}</p>
                                <p style='margin: 5px 0;'><strong>Phone:</strong> {order.ShippingAddress.Phone}</p>
                            </div>

                            <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>
                            <p style='color: #6c757d; font-size: 12px;'>
                                This is an automated notification from the Aljawhara E-commerce system.
                            </p>
                        </div>
                    </body>
                    </html>";

                return await SendEmailAsync("info@aljawhara.com", subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending order notification email for order {OrderNumber}", order.OrderNumber);
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
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.SslOnConnect);
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
