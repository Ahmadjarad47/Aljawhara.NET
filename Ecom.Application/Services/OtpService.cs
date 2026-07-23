using Ecom.Application.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Ecom.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<OtpService> _logger;
        private const int OtpExpirationMinutes = 60; // 1 hour
        private const string OtpCacheKeyPrefix = "otp_";

        public OtpService(IMemoryCache cache, ILogger<OtpService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<string> GenerateOtpAsync(string email)
        {
            try
            {
                // Generate 6-digit OTP
                var otp = GenerateRandomOtp();
                
                // Store OTP in cache with expiration
                var cacheKey = $"{OtpCacheKeyPrefix}{email}";
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OtpExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(15) // Reset expiration if accessed within 15 minutes
                    ,Size=1
                };
                
                _cache.Set(cacheKey, otp, cacheEntryOptions);
                
                _logger.LogInformation("OTP generated for email: {Email}", email);
                return Task.FromResult(otp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OTP for email: {Email}", email);
                throw;
            }
        }

        public Task<bool> VerifyOtpAsync(string email, string otp)
        {
            try
            {
                var cacheKey = $"{OtpCacheKeyPrefix}{email}";
                
                if (!_cache.TryGetValue(cacheKey, out string? storedOtp))
                {
                    _logger.LogWarning("No OTP found for email: {Email}", email);
                    return Task.FromResult(false);
                }

                if (string.IsNullOrEmpty(storedOtp) || storedOtp != otp)
                {
                    _logger.LogWarning("Invalid OTP for email: {Email}", email);
                    return Task.FromResult(false);
                }

                // Remove OTP after successful verification
                _cache.Remove(cacheKey);
                _logger.LogInformation("OTP verified successfully for email: {Email}", email);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for email: {Email}", email);
                return Task.FromResult(false);
            }
        }

        public Task<bool> IsOtpValidAsync(string email)
        {
            try
            {
                var cacheKey = $"{OtpCacheKeyPrefix}{email}";
                return Task.FromResult(_cache.TryGetValue(cacheKey, out _));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OTP validity for email: {Email}", email);
                return Task.FromResult(false);
            }
        }

        public Task<bool> InvalidateOtpAsync(string email)
        {
            try
            {
                var cacheKey = $"{OtpCacheKeyPrefix}{email}";
                _cache.Remove(cacheKey);
                _logger.LogInformation("OTP invalidated for email: {Email}", email);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating OTP for email: {Email}", email);
                return Task.FromResult(false);
            }
        }

        private string GenerateRandomOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var randomNumber = Math.Abs(BitConverter.ToInt32(bytes, 0));
            return (randomNumber % 1000000).ToString("D6");
        }
    }
}
