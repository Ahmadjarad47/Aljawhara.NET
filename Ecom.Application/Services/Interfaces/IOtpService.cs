namespace Ecom.Application.Services.Interfaces
{
    public interface IOtpService
    {
        Task<string> GenerateOtpAsync(string email);
        Task<bool> VerifyOtpAsync(string email, string otp);
        Task<bool> IsOtpValidAsync(string email);
        Task<bool> InvalidateOtpAsync(string email);
    }
}
