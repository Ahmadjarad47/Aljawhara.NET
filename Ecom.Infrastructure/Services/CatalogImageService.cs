using System.Text.RegularExpressions;
using Ecom.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Ecom.Infrastructure.Services;

public sealed class CatalogImageOptions
{
    public const int RequestBytes = 32 * 1024 * 1024;
    public int MaxFileBytes { get; set; } = 8 * 1024 * 1024;
    public int MaxWidth { get; set; } = 4096;
    public int MaxHeight { get; set; } = 4096;
    public int MaxPixels { get; set; } = 12_000_000;
    public int MaxFiles { get; set; } = 8;

    public bool IsValid() => MaxFileBytes is > 0 and <= RequestBytes &&
        MaxWidth is > 0 and <= 8192 && MaxHeight is > 0 and <= 8192 &&
        MaxPixels is > 0 and <= 20_000_000 && MaxFiles is > 0 and <= 12;
}

public sealed class CatalogImageException(string message) : Exception(message);
public enum CatalogImageArea { Products, Categories, Carousels }

public sealed partial class CatalogImageService(ICatalogObjectStorage storage, IOptions<CatalogImageOptions> options)
{
    private readonly CatalogImageOptions _limits = options.Value;

    [GeneratedRegex(@"\A[0-9a-f]{32}\.(?:jpg|png|webp)\z", RegexOptions.CultureInvariant)]
    private static partial Regex StoredFileName();

    private string Prefix(CatalogImageArea area, Guid owner) =>
        $"catalog/{storage.BucketScope}/{area.ToString().ToLowerInvariant()}/{owner:N}/";

    internal static bool IsKeyInScope(string key, string bucketScope)
    {
        var parts = key.Split('/');
        return parts is ["catalog", var scope, "products", var owner, var file] &&
            scope == bucketScope && Guid.TryParseExact(owner, "N", out _) && StoredFileName().IsMatch(file);
    }

    public string? OwnedProductKey(Guid owner, string savedUrl)
    {
        var prefix = Prefix(CatalogImageArea.Products, owner);
        var urlPrefix = storage.PublicUrl(prefix);
        if (!savedUrl.StartsWith(urlPrefix, StringComparison.Ordinal))
            return null;
        var file = savedUrl[urlPrefix.Length..];
        return StoredFileName().IsMatch(file) ? prefix + file : null;
    }

    public Task DeleteProductObjectAsync(string key) => storage.DeleteAsync(key);

    public async Task<string[]> UploadAsync(IReadOnlyList<IFormFile> files, CatalogImageArea area, Guid owner)
    {
        if (files.Count > _limits.MaxFiles || files.Any(f => f.Length <= 0 || f.Length > _limits.MaxFileBytes) ||
            files.Sum(f => f.Length) > CatalogImageOptions.RequestBytes)
            throw new CatalogImageException("The number or size of images exceeds the upload limits, or an image is empty.");

        // Validate and normalize the entire batch before any write.
        var images = new List<EncodedImage>(files.Count);
        long bytes = 0;
        foreach (var file in files)
        {
            var image = Encode(file);
            bytes += image.Bytes.Length;
            if (bytes > CatalogImageOptions.RequestBytes)
                throw new CatalogImageException("The total encoded image size exceeds the upload limit.");
            images.Add(image);
        }

        var urls = new List<string>(images.Count);
        foreach (var image in images)
        {
            var key = $"{Prefix(area, owner)}{Guid.NewGuid():N}.{image.Extension}";
            using var content = new MemoryStream(image.Bytes, writable: false);
            await storage.PutAsync(key, content, image.ContentType);
            urls.Add(storage.PublicUrl(key));
        }
        return urls.ToArray();
    }

    private EncodedImage Encode(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        // Limit format dispatch before the native decoder sees the input.
        Span<byte> signature = stackalloc byte[12];
        var read = stream.ReadAtLeast(signature, signature.Length, false);
        var format = signature[..read] switch
        {
            [0xFF, 0xD8, 0xFF, ..] => SKEncodedImageFormat.Jpeg,
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => SKEncodedImageFormat.Png,
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P'] => SKEncodedImageFormat.Webp,
            _ => throw new CatalogImageException("Only JPEG, PNG, and WebP images are allowed.")
        };
        stream.Position = 0;
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.EncodedFormat != format)
            throw new CatalogImageException("The image could not be decoded.");
        var width = codec.Info.Width;
        var height = codec.Info.Height;
        var swap = (int)codec.EncodedOrigin >= (int)SKEncodedOrigin.LeftTop;
        if (width <= 0 || height <= 0 || (swap ? height : width) > _limits.MaxWidth ||
            (swap ? width : height) > _limits.MaxHeight || (long)width * height > _limits.MaxPixels)
            throw new CatalogImageException("The image dimensions exceed the upload limits.");
        if (codec.FrameCount > 1)
            throw new CatalogImageException("Animated images are not allowed.");

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888,
            format == SKEncodedImageFormat.Jpeg ? SKAlphaType.Opaque : SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new CatalogImageException("The image could not be decoded.");

        // Apply EXIF orientation to the pixels so the re-encoded image needs no input metadata.
        using var oriented = codec.EncodedOrigin == SKEncodedOrigin.TopLeft ? null : Orient(bitmap, codec.EncodedOrigin, swap);
        using var raster = SKImage.FromBitmap(oriented ?? bitmap);
        using var encoded = raster.Encode(format, 90);
        if (encoded is null || encoded.Size == 0 || encoded.Size > _limits.MaxFileBytes)
            throw new CatalogImageException("The encoded image exceeds the upload limit or could not be saved.");
        var (extension, contentType) = format switch
        {
            SKEncodedImageFormat.Jpeg => ("jpg", "image/jpeg"),
            SKEncodedImageFormat.Png => ("png", "image/png"),
            _ => ("webp", "image/webp")
        };
        return new EncodedImage(encoded.ToArray(), extension, contentType);
    }

    private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin, bool swap)
    {
        var w = bitmap.Width;
        var h = bitmap.Height;
        var result = new SKBitmap(new SKImageInfo(swap ? h : w, swap ? w : h, bitmap.ColorType, bitmap.AlphaType));
        using var canvas = new SKCanvas(result);
        var transform = origin switch
        {
            SKEncodedOrigin.TopRight => Matrix(-1, 0, w, 0, 1, 0),
            SKEncodedOrigin.BottomRight => Matrix(-1, 0, w, 0, -1, h),
            SKEncodedOrigin.BottomLeft => Matrix(1, 0, 0, 0, -1, h),
            SKEncodedOrigin.LeftTop => Matrix(0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => Matrix(0, -1, h, 1, 0, 0),
            SKEncodedOrigin.RightBottom => Matrix(0, -1, h, -1, 0, w),
            SKEncodedOrigin.LeftBottom => Matrix(0, 1, 0, -1, 0, w),
            _ => SKMatrix.Identity
        };
        canvas.SetMatrix(transform);
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;

        static SKMatrix Matrix(float a, float b, float x, float c, float d, float y) =>
            new(a, b, x, c, d, y, 0, 0, 1);
    }

    private sealed record EncodedImage(byte[] Bytes, string Extension, string ContentType);
}
