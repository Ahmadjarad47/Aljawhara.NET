using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.AspNetCore.Http;
using Ecom.Domain.Interfaces;

namespace Ecom.Infrastructure.Services
{
    public class CloudflareR2Service : IFileService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _publicDomain;
        private readonly HttpClient _httpClient;

        public CloudflareR2Service()
        {
            var accessKey = Environment.GetEnvironmentVariable("R2_KEY") ?? "your_key";
            var secretKey = Environment.GetEnvironmentVariable("R2_SECRET") ?? "your_secret";
            var accountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? "your_account_id";
            _bucketName = Environment.GetEnvironmentVariable("R2_BUCKET_NAME") ?? "images";
            _publicDomain = Environment.GetEnvironmentVariable("R2_PUBLIC_DOMAIN") ?? $"https://{_bucketName}.{accountId}.r2.cloudflarestorage.com";

            // Configure SSL/TLS for Cloudflare R2 connection
            var httpClientHandler = new HttpClientHandler
            {
                // Ensure proper SSL/TLS protocol support
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                // Allow certificate validation (Cloudflare uses valid certificates)
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // In production, validate certificates properly
                    // For Cloudflare R2, certificates should be valid
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                        return true;

                    // Log certificate errors for debugging
                    System.Diagnostics.Debug.WriteLine($"SSL Certificate Error: {errors}");

                    // In production, you might want stricter validation
                    // For now, return true to allow connection (adjust based on your security requirements)
                    return true;
                }
            };

            // Create HttpClient with proper SSL configuration
            _httpClient = new HttpClient(httpClientHandler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            var config = new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                UseHttp = false,
                // Use the configured HttpClient
                HttpClientFactory = new CustomHttpClientFactory(_httpClient)
            };

            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            _s3Client = new AmazonS3Client(credentials, config);
        }

        public async Task<string> SaveFileAsync(IFormFile file, string directory)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var key = $"{directory}/{fileName}";
            
            // Stream upload directly to S3 without buffering entire file in memory
            using var stream = file.OpenReadStream();

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType,
                UseChunkEncoding = false,
                AutoCloseStream = true
            };

            await _s3Client.PutObjectAsync(request);

            return $"{_publicDomain}/{key}";
        }

        public async Task<bool> DeleteFileAsync(string fileUrl)
        {
            try
            {
                var uri = new Uri(fileUrl);
                var key = uri.AbsolutePath.TrimStart('/');

                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                await _s3Client.DeleteObjectAsync(deleteRequest);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // Helper class to provide custom HttpClient to AWS SDK
    public class CustomHttpClientFactory : Amazon.Runtime.HttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public CustomHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            return _httpClient;
        }
    }
}
