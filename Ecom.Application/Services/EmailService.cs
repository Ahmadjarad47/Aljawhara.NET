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
                
                var subject = "تأكيد البريد الإلكتروني - المتجر الجوهرة";
                var body = $@"
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>مرحباً بك في المتجر الجوهرة!</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px;'>
                                                <p style='color: #333333; font-size: 16px; line-height: 1.8; margin: 0 0 20px 0;'>عزيزي/عزيزتي <strong>{user.UserName}</strong>,</p>
                                                <p style='color: #555555; font-size: 15px; line-height: 1.8; margin: 0 0 30px 0;'>شكراً لك على التسجيل في المتجر الجوهرة. يرجى تأكيد عنوان بريدك الإلكتروني بالنقر على الزر أدناه:</p>
                                                <div style='text-align: center; margin: 30px 0;'>
                                                    <a href='{confirmationLink}' style='display: inline-block; background-color: #054239d9; color: #ffffff; padding: 15px 40px; text-decoration: none; border-radius: 8px; font-size: 16px; font-weight: bold; box-shadow: 0 4px 6px rgba(5,66,57,0.3); transition: all 0.3s;'>تأكيد البريد الإلكتروني</a>
                                                </div>
                                                <p style='color: #777777; font-size: 14px; line-height: 1.6; margin: 30px 0 20px 0;'>إذا لم يعمل الزر، يرجى نسخ الرابط التالي ولصقه في المتصفح:</p>
                                                <p style='color: #054239d9; font-size: 13px; word-break: break-all; background-color: #f8f9fa; padding: 15px; border-radius: 5px; border-right: 4px solid #054239d9; margin: 0;'>{confirmationLink}</p>
                                                <p style='color: #dc3545; font-size: 14px; margin: 25px 0 0 0;'><strong>ملاحظة:</strong> هذا الرابط صالح لمدة 24 ساعة فقط.</p>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 14px; margin: 0;'>مع أطيب التحيات،<br><strong style='color: #054239d9;'>فريق المتجر الجوهرة</strong></p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
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
                
                var subject = "إعادة تعيين كلمة المرور - المتجر الجوهرة";
                var body = $@"
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>طلب إعادة تعيين كلمة المرور</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px;'>
                                                <p style='color: #333333; font-size: 16px; line-height: 1.8; margin: 0 0 20px 0;'>عزيزي/عزيزتي <strong>{user.UserName}</strong>,</p>
                                                <p style='color: #555555; font-size: 15px; line-height: 1.8; margin: 0 0 30px 0;'>لقد طلبت إعادة تعيين كلمة المرور الخاصة بحسابك. يرجى النقر على الزر أدناه لإعادة تعيين كلمة المرور:</p>
                                                <div style='text-align: center; margin: 30px 0;'>
                                                    <a href='{resetLink}' style='display: inline-block; background-color: #054239d9; color: #ffffff; padding: 15px 40px; text-decoration: none; border-radius: 8px; font-size: 16px; font-weight: bold; box-shadow: 0 4px 6px rgba(5,66,57,0.3); transition: all 0.3s;'>إعادة تعيين كلمة المرور</a>
                                                </div>
                                                <p style='color: #777777; font-size: 14px; line-height: 1.6; margin: 30px 0 20px 0;'>إذا لم يعمل الزر، يرجى نسخ الرابط التالي ولصقه في المتصفح:</p>
                                                <p style='color: #054239d9; font-size: 13px; word-break: break-all; background-color: #f8f9fa; padding: 15px; border-radius: 5px; border-right: 4px solid #054239d9; margin: 0;'>{resetLink}</p>
                                                <p style='color: #dc3545; font-size: 14px; margin: 25px 0 0 0;'><strong>ملاحظة:</strong> هذا الرابط صالح لمدة ساعة واحدة فقط.</p>
                                                <div style='background-color: #fff3cd; border-right: 4px solid #ffc107; padding: 15px; border-radius: 5px; margin-top: 25px;'>
                                                    <p style='color: #856404; font-size: 14px; margin: 0;'><strong>تنبيه:</strong> إذا لم تطلب إعادة تعيين كلمة المرور، يرجى تجاهل هذه الرسالة.</p>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 14px; margin: 0;'>مع أطيب التحيات،<br><strong style='color: #054239d9;'>فريق المتجر الجوهرة</strong></p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
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
                
                var subject = "تأكيد تغيير البريد الإلكتروني - المتجر الجوهرة";
                var body = $@"
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>تأكيد تغيير البريد الإلكتروني</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px;'>
                                                <p style='color: #333333; font-size: 16px; line-height: 1.8; margin: 0 0 20px 0;'>عزيزي/عزيزتي <strong>{user.UserName}</strong>,</p>
                                                <p style='color: #555555; font-size: 15px; line-height: 1.8; margin: 0 0 20px 0;'>لقد طلبت تغيير عنوان بريدك الإلكتروني إلى:</p>
                                                <div style='background-color: #e7f3ff; border-right: 4px solid #054239d9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                                    <p style='color: #054239d9; font-size: 16px; font-weight: bold; margin: 0;'>{newEmail}</p>
                                                </div>
                                                <p style='color: #555555; font-size: 15px; line-height: 1.8; margin: 0 0 30px 0;'>يرجى تأكيد هذا التغيير بالنقر على الزر أدناه:</p>
                                                <div style='text-align: center; margin: 30px 0;'>
                                                    <a href='{confirmationLink}' style='display: inline-block; background-color: #054239d9; color: #ffffff; padding: 15px 40px; text-decoration: none; border-radius: 8px; font-size: 16px; font-weight: bold; box-shadow: 0 4px 6px rgba(5,66,57,0.3); transition: all 0.3s;'>تأكيد تغيير البريد الإلكتروني</a>
                                                </div>
                                                <p style='color: #777777; font-size: 14px; line-height: 1.6; margin: 30px 0 20px 0;'>إذا لم يعمل الزر، يرجى نسخ الرابط التالي ولصقه في المتصفح:</p>
                                                <p style='color: #054239d9; font-size: 13px; word-break: break-all; background-color: #f8f9fa; padding: 15px; border-radius: 5px; border-right: 4px solid #054239d9; margin: 0;'>{confirmationLink}</p>
                                                <p style='color: #dc3545; font-size: 14px; margin: 25px 0 0 0;'><strong>ملاحظة:</strong> هذا الرابط صالح لمدة 24 ساعة فقط.</p>
                                                <div style='background-color: #f8d7da; border-right: 4px solid #dc3545; padding: 15px; border-radius: 5px; margin-top: 25px;'>
                                                    <p style='color: #721c24; font-size: 14px; margin: 0;'><strong>تحذير:</strong> إذا لم تطلب تغيير البريد الإلكتروني، يرجى الاتصال بالدعم فوراً.</p>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 14px; margin: 0;'>مع أطيب التحيات،<br><strong style='color: #054239d9;'>فريق المتجر الجوهرة</strong></p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
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
                var subject = "حظر الحساب - المتجر الجوهرة";
                var blockDuration = blockUntil.HasValue ? $" حتى {blockUntil.Value:yyyy-MM-dd HH:mm:ss}" : " بشكل دائم";
                var body = $@"
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #dc3545 0%, #c82333 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>تم حظر حسابك</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px;'>
                                                <p style='color: #333333; font-size: 16px; line-height: 1.8; margin: 0 0 20px 0;'>عزيزي/عزيزتي <strong>{user.UserName}</strong>,</p>
                                                <p style='color: #555555; font-size: 15px; line-height: 1.8; margin: 0 0 30px 0;'>تم حظر حسابك{blockDuration}.</p>
                                                <div style='background-color: #fff3cd; border-right: 4px solid #ffc107; padding: 20px; border-radius: 5px; margin: 20px 0;'>
                                                    <p style='color: #856404; font-size: 15px; margin: 0 0 10px 0;'><strong>السبب:</strong></p>
                                                    <p style='color: #856404; font-size: 15px; margin: 0; line-height: 1.6;'>{reason}</p>
                                                </div>
                                                <div style='background-color: #d1ecf1; border-right: 4px solid #054239d9; padding: 15px; border-radius: 5px; margin-top: 25px;'>
                                                    <p style='color: #0c5460; font-size: 14px; margin: 0;'>إذا كنت تعتقد أن هذا خطأ، يرجى الاتصال بفريق الدعم لدينا.</p>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 14px; margin: 0;'>مع أطيب التحيات،<br><strong style='color: #054239d9;'>فريق المتجر الجوهرة</strong></p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
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
                var subject = "إلغاء حظر الحساب - المتجر الجوهرة";
                var body = $@"
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #28a745 0%, #218838 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>تم إلغاء حظر حسابك</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px; text-align: center;'>
                                                <div style='margin: 30px 0;'>
                                                    <div style='width: 80px; height: 80px; background-color: #d4edda; border-radius: 50%; margin: 0 auto; display: flex; align-items: center; justify-content: center;'>
                                                        <span style='font-size: 40px; color: #28a745;'>✓</span>
                                                    </div>
                                                </div>
                                                <p style='color: #333333; font-size: 16px; line-height: 1.8; margin: 0 0 20px 0;'>عزيزي/عزيزتي <strong>{user.UserName}</strong>,</p>
                                                <p style='color: #555555; font-size: 15px; line-height: 1.8; margin: 0 0 30px 0;'>تم إلغاء حظر حسابك ويمكنك الآن الوصول إلى جميع ميزات المتجر الجوهرة.</p>
                                                <div style='background-color: #d4edda; border-right: 4px solid #28a745; padding: 20px; border-radius: 5px; margin: 20px 0;'>
                                                    <p style='color: #155724; font-size: 18px; font-weight: bold; margin: 0;'>مرحباً بعودتك!</p>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 14px; margin: 0;'>مع أطيب التحيات،<br><strong style='color: #054239d9;'>فريق المتجر الجوهرة</strong></p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
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
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>رمز التحقق</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px;'>
                                                <p style='color: #333333; font-size: 16px; line-height: 1.8; margin: 0 0 20px 0;'>عزيزي/عزيزتي <strong>{user.UserName}</strong>,</p>
                                                <p style='color: #555555; font-size: 15px; line-height: 1.8; margin: 0 0 30px 0; text-align: center;'>رمز التحقق الخاص بك هو:</p>
                                                <div style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%); padding: 30px; text-align: center; border-radius: 10px; margin: 30px 0; box-shadow: 0 4px 6px rgba(5,66,57,0.3);'>
                                                    <h1 style='color: #ffffff; font-size: 42px; margin: 0; letter-spacing: 8px; font-weight: bold; font-family: 'Courier New', monospace;'>{otp}</h1>
                                                </div>
                                                <p style='color: #dc3545; font-size: 14px; margin: 25px 0 0 0; text-align: center;'><strong>ملاحظة:</strong> هذا الرمز صالح لمدة ساعة واحدة فقط.</p>
                                                <div style='background-color: #fff3cd; border-right: 4px solid #ffc107; padding: 15px; border-radius: 5px; margin-top: 25px;'>
                                                    <p style='color: #856404; font-size: 14px; margin: 0; text-align: center;'>إذا لم تطلب هذا الرمز، يرجى تجاهل هذه الرسالة.</p>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 14px; margin: 0;'>مع أطيب التحيات،<br><strong style='color: #054239d9;'>فريق المتجر الجوهرة</strong></p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
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
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #28a745 0%, #218838 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>تم تغيير كلمة المرور بنجاح</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px; text-align: center;'>
                                                <div style='margin: 30px 0;'>
                                                    <div style='width: 80px; height: 80px; background-color: #d4edda; border-radius: 50%; margin: 0 auto; display: flex; align-items: center; justify-content: center;'>
                                                        <span style='font-size: 40px; color: #28a745;'>✓</span>
                                                    </div>
                                                </div>
                                                <p style='color: #333333; font-size: 16px; line-height: 1.8; margin: 0 0 20px 0;'>عزيزي/عزيزتي <strong>{user.UserName}</strong>,</p>
                                                <p style='color: #555555; font-size: 15px; line-height: 1.8; margin: 0 0 30px 0;'>تم تغيير كلمة المرور الخاصة بحسابك بنجاح.</p>
                                                <div style='background-color: #f8d7da; border-right: 4px solid #dc3545; padding: 15px; border-radius: 5px; margin-top: 25px;'>
                                                    <p style='color: #721c24; font-size: 14px; margin: 0;'><strong>تحذير:</strong> إذا لم تقم بتغيير كلمة المرور، يرجى الاتصال بنا فوراً.</p>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 14px; margin: 0;'>مع أطيب التحيات،<br><strong style='color: #054239d9;'>فريق المتجر الجوهرة</strong></p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
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
                var contactSubject = $"إرسال نموذج الاتصال: {subject}";
                var phoneInfo = !string.IsNullOrWhiteSpace(phoneNumber) ? $@"
                                    <tr>
                                        <td style='padding: 12px; font-weight: bold; width: 180px; background-color: #f8f9fa; border: 1px solid #e9ecef;'>رقم الهاتف:</td>
                                        <td style='padding: 12px; border: 1px solid #e9ecef;'>{phoneNumber}</td>
                                    </tr>" : "";
                var body = $@"
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='700' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>إرسال جديد من نموذج الاتصال</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px;'>
                                                <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 25px; border-right: 4px solid #054239d9;'>
                                                    <h3 style='color: #054239d9; margin: 0 0 20px 0; font-size: 20px;'>معلومات المرسل</h3>
                                                    <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse: collapse;'>
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; width: 180px; background-color: #ffffff; border: 1px solid #e9ecef;'>الاسم:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef; background-color: #ffffff;'>{name}</td>
                                                        </tr>
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; width: 180px; background-color: #f8f9fa; border: 1px solid #e9ecef;'>البريد الإلكتروني:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef;'>{email}</td>
                                                        </tr>
                                                        {phoneInfo}
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; width: 180px; background-color: {(string.IsNullOrWhiteSpace(phoneNumber) ? "#ffffff" : "#f8f9fa")}; border: 1px solid #e9ecef;'>الموضوع:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef; background-color: {(string.IsNullOrWhiteSpace(phoneNumber) ? "#ffffff" : "#f8f9fa")};'>{subject}</td>
                                                        </tr>
                                                    </table>
                                                </div>
                                                <div style='background-color: #e7f3ff; padding: 20px; border-radius: 8px; border-right: 4px solid #054239d9;'>
                                                    <h3 style='color: #054239d9; margin: 0 0 15px 0; font-size: 20px;'>الرسالة</h3>
                                                    <div style='background-color: #ffffff; padding: 20px; border-radius: 5px; border: 1px solid #d1ecf1;'>
                                                        <p style='color: #333333; font-size: 15px; line-height: 1.8; margin: 0; white-space: pre-wrap;'>{message}</p>
                                                    </div>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 13px; margin: 0; font-style: italic;'>تم إرسال هذه الرسالة من نموذج الاتصال على الموقع.</p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
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

        public async Task<bool> SendOrderNotificationToAdminAsync(OrderDto order,string email, string eventType)
        {
            try
            {
                var subject = eventType == "Created" 
                    ? $"طلب جديد - {order.OrderNumber}" 
                    : $"تحديث حالة الطلب - {order.OrderNumber}";

                var itemsHtml = string.Join("", order.Items.Select(item => $@"
                    <tr>
                        <td style='padding: 12px; border: 1px solid #e9ecef; background-color: #ffffff;'>{item.Name}</td>
                        <td style='padding: 12px; border: 1px solid #e9ecef; text-align: center; background-color: #ffffff;'>{item.Quantity}</td>
                        <td style='padding: 12px; border: 1px solid #e9ecef; text-align: right; background-color: #ffffff;'>{item.Price:C}</td>
                        <td style='padding: 12px; border: 1px solid #e9ecef; text-align: right; background-color: #ffffff; font-weight: bold;'>{item.Total:C}</td>
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

                var statusText = order.Status switch
                {
                    OrderStatus.Pending => "قيد الانتظار",
                    OrderStatus.Processing => "قيد المعالجة",
                    OrderStatus.Shipped => "تم الشحن",
                    OrderStatus.Delivered => "تم التسليم",
                    OrderStatus.Cancelled => "ملغي",
                    OrderStatus.Refunded => "مسترد",
                    _ => order.Status.ToString()
                };

                var eventDescription = eventType == "Created" 
                    ? $"تم إنشاء طلب جديد من قبل <strong>{order.CustomerName}</strong>." 
                    : $"تم تحديث حالة الطلب إلى <strong style='color: {statusColor};'>{statusText}</strong>.";

                var body = $@"
                    <!DOCTYPE html>
                    <html dir='rtl' lang='ar'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f5f5f5;'>
                        <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
                            <tr>
                                <td align='center'>
                                    <table width='800' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                                        <tr>
                                            <td style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%); padding: 30px; text-align: center;'>
                                                <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>
                                                    إشعار الطلب - {order.OrderNumber}
                                                </h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 40px 30px;'>
                                                <p style='font-size: 16px; color: #333333; line-height: 1.8; margin: 0 0 30px 0;'>{eventDescription}</p>
                                                
                                                <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin: 25px 0; border-right: 4px solid #054239d9;'>
                                                    <h3 style='margin: 0 0 20px 0; color: #054239d9; font-size: 20px;'>تفاصيل الطلب</h3>
                                                    <table style='width: 100%; border-collapse: collapse;'>
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; width: 200px; background-color: #ffffff; border: 1px solid #e9ecef;'>رقم الطلب:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef; background-color: #ffffff;'>{order.OrderNumber}</td>
                                                        </tr>
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; background-color: #f8f9fa; border: 1px solid #e9ecef;'>اسم العميل:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef;'>{order.CustomerName}</td>
                                                        </tr>
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; background-color: #ffffff; border: 1px solid #e9ecef;'>الحالة:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef; background-color: #ffffff;'>
                                                                <span style='background-color: {statusColor}; color: white; padding: 6px 15px; border-radius: 5px; font-weight: bold; font-size: 14px;'>
                                                                    {statusText}
                                                                </span>
                                                            </td>
                                                        </tr>
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; background-color: #f8f9fa; border: 1px solid #e9ecef;'>تاريخ الطلب:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef;'>{order.CreatedAt:yyyy-MM-dd HH:mm:ss}</td>
                                                        </tr>
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; background-color: #ffffff; border: 1px solid #e9ecef;'>المجموع الفرعي:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef; background-color: #ffffff;'>{order.Subtotal:C}</td>
                                                        </tr>
                                                        {(order.CouponDiscountAmount.HasValue && order.CouponDiscountAmount > 0 ? $@"
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; background-color: #f8f9fa; border: 1px solid #e9ecef;'>كوبون ({order.CouponCode}):</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef; color: #28a745; font-weight: bold;'>-{order.CouponDiscountAmount:C}</td>
                                                        </tr>" : "")}
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; background-color: {(order.CouponDiscountAmount.HasValue && order.CouponDiscountAmount > 0 ? "#ffffff" : "#f8f9fa")}; border: 1px solid #e9ecef;'>الشحن:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef; background-color: {(order.CouponDiscountAmount.HasValue && order.CouponDiscountAmount > 0 ? "#ffffff" : "#f8f9fa")};'>{order.Shipping:C}</td>
                                                        </tr>
                                                        <tr>
                                                            <td style='padding: 12px; font-weight: bold; background-color: #f8f9fa; border: 1px solid #e9ecef;'>الضريبة:</td>
                                                            <td style='padding: 12px; border: 1px solid #e9ecef;'>{order.Tax:C}</td>
                                                        </tr>
                                                        <tr style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%);'>
                                                            <td style='padding: 15px; font-weight: bold; font-size: 18px; color: #ffffff; border: 1px solid #054239;'>الإجمالي:</td>
                                                            <td style='padding: 15px; font-weight: bold; font-size: 18px; color: #ffffff; border: 1px solid #054239;'>{order.Total:C}</td>
                                                        </tr>
                                                    </table>
                                                </div>

                                                <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin: 25px 0; border-right: 4px solid #054239d9;'>
                                                    <h3 style='margin: 0 0 20px 0; color: #054239d9; font-size: 20px;'>عناصر الطلب</h3>
                                                    <table style='width: 100%; border-collapse: collapse;'>
                                                        <thead>
                                                            <tr style='background: linear-gradient(135deg, #054239d9 0%, #054239 100%); color: white;'>
                                                                <th style='padding: 12px; border: 1px solid #054239; text-align: right;'>المنتج</th>
                                                                <th style='padding: 12px; border: 1px solid #054239; text-align: center;'>الكمية</th>
                                                                <th style='padding: 12px; border: 1px solid #054239; text-align: right;'>السعر</th>
                                                                <th style='padding: 12px; border: 1px solid #054239; text-align: right;'>الإجمالي</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody>
                                                            {itemsHtml}
                                                        </tbody>
                                                    </table>
                                                </div>

                                                <div style='background-color: #e7f3ff; padding: 20px; border-radius: 8px; margin: 25px 0; border-right: 4px solid #054239d9;'>
                                                    <h3 style='margin: 0 0 15px 0; color: #054239d9; font-size: 20px;'>عنوان الشحن</h3>
                                                    <div style='background-color: #ffffff; padding: 20px; border-radius: 5px; border: 1px solid #d1ecf1;'>
                                                        <p style='margin: 8px 0; color: #333333; font-size: 15px;'><strong>{order.ShippingAddress.FullName}</strong></p>
                                                        <p style='margin: 8px 0; color: #555555; font-size: 15px;'>{order.ShippingAddress.Street}</p>
                                                        <p style='margin: 8px 0; color: #555555; font-size: 15px;'>
                                                            {order.ShippingAddress.City}
                                                            {(!string.IsNullOrEmpty(order.ShippingAddress.State) ? $", {order.ShippingAddress.State}" : "")}
                                                            {order.ShippingAddress.PostalCode}
                                                        </p>
                                                        <p style='margin: 8px 0; color: #555555; font-size: 15px;'>{order.ShippingAddress.Country}</p>
                                                        <p style='margin: 8px 0; color: #333333; font-size: 15px;'><strong>الهاتف:</strong> {order.ShippingAddress.Phone}</p>
                                                    </div>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style='background-color: #f8f9fa; padding: 25px 30px; text-align: center; border-top: 1px solid #e9ecef;'>
                                                <p style='color: #666666; font-size: 12px; margin: 0;'>
                                                    هذه إشعار تلقائي من نظام المتجر الجوهرة للتجارة الإلكترونية.
                                                </p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
                    </body>
                    </html>";

                 await SendEmailAsync(email, subject, body);
                return await SendEmailAsync("Bader.kh94@gmail.com", subject, body);
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
