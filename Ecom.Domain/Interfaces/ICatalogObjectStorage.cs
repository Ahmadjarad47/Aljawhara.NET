namespace Ecom.Domain.Interfaces;

public interface ICatalogObjectStorage
{
    // Stable namespace for the configured account and bucket.
    string BucketScope { get; }
    string PublicUrl(string key);
    Task PutAsync(string key, Stream image, string contentType);
    Task DeleteAsync(string key);
}
