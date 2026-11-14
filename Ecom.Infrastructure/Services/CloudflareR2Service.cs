using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ecom.Domain.Interfaces;

namespace Ecom.Infrastructure.Services;

public sealed class CloudflareR2Service : ICatalogObjectStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly string _origin;

    public CloudflareR2Service()
    {
        var account = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? "your_account_id";
        _bucket = Environment.GetEnvironmentVariable("R2_BUCKET_NAME") ?? "images";
        var publicDomain = Environment.GetEnvironmentVariable("R2_PUBLIC_DOMAIN") ??
            $"https://{_bucket}.{account}.r2.cloudflarestorage.com";
        if (!Uri.TryCreate(publicDomain, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps ||
            origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new InvalidOperationException("R2_PUBLIC_DOMAIN must be a dedicated HTTPS asset origin.");
        _origin = origin.GetLeftPart(UriPartial.Authority);
        BucketScope = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{account}\n{_bucket}")));
        _client = new AmazonS3Client(new BasicAWSCredentials(
            Environment.GetEnvironmentVariable("R2_KEY") ?? "your_key",
            Environment.GetEnvironmentVariable("R2_SECRET") ?? "your_secret"),
            new AmazonS3Config
            {
                ServiceURL = $"https://{account}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                UseHttp = false
            });
    }

    public string BucketScope { get; }
    public string PublicUrl(string key) => $"{_origin}/{key}";

    public async Task PutAsync(string key, Stream image, string contentType)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = image,
            ContentType = contentType,
            UseChunkEncoding = false
        });
    }

    public async Task DeleteAsync(string key)
    {
        if (!CatalogImageService.IsKeyInScope(key, BucketScope))
            throw new ArgumentException("Invalid catalog object key.", nameof(key));
        await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key });
    }

    public void Dispose() => _client.Dispose();
}
