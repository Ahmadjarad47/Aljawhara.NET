namespace Ecom.Application.Services
{
    public record class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiredAfterSeconds { get; set; }
    }
}