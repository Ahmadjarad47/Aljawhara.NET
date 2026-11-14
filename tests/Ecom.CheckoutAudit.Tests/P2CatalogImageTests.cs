using System.Text;
using Ecom.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Ecom.CheckoutAudit.Tests;

public sealed class P2CatalogImageTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "image/png", ".png")]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg", ".jpg")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp", ".webp")]
    public async Task Storage_receives_normalized_raster_for_every_allowed_format(SKEncodedImageFormat format, string type, string extension)
    {
        var fake = new FakeCatalogStorage();
        var service = new CatalogImageService(fake, Options.Create(new CatalogImageOptions()));
        var owner = Guid.NewGuid();
        var bytes = HttpSecurityTests.RasterFixture(format: format).Concat("inert trailer"u8.ToArray()).ToArray();
        using var content = new MemoryStream(bytes);
        var url = Assert.Single(await service.UploadAsync([File(content)], CatalogImageArea.Products, owner));
        var write = Assert.Single(fake.Writes);
        Assert.Equal(type, write.Type);
        Assert.EndsWith(extension, write.Key);
        Assert.Equal(fake.PublicUrl(write.Key), url);
        Assert.DoesNotContain("inert trailer", Encoding.UTF8.GetString(write.Bytes));
        Assert.Equal(write.Key, service.OwnedProductKey(owner, url));
        Assert.Null(service.OwnedProductKey(Guid.NewGuid(), url));
        Assert.Null(service.OwnedProductKey(owner, url + "?alias=1"));
        Assert.Null(service.OwnedProductKey(owner, url.Replace("https://", "http://")));
        Assert.Null(service.OwnedProductKey(owner, url.Replace("test-bucket", "other-bucket")));
        Assert.Null(service.OwnedProductKey(owner, url.Replace("/products/", "/categories/")));

        using var decoded = SKBitmap.Decode(write.Bytes);
        Assert.Equal(2, decoded.Width);
        Assert.Equal(2, decoded.Height);
    }

    [Fact]
    public async Task Empty_nonraster_truncated_and_oversized_inputs_are_rejected_before_storage()
    {
        var fake = new FakeCatalogStorage();
        var service = new CatalogImageService(fake, Options.Create(new CatalogImageOptions
            { MaxFileBytes = 2048, MaxWidth = 3, MaxHeight = 4, MaxPixels = 8, MaxFiles = 2 }));
        var owner = Guid.NewGuid();
        var valid = HttpSecurityTests.RasterFixture();
        foreach (var bytes in new[]
        {
            [], "<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray(), valid[..24], new byte[2049],
            HttpSecurityTests.RasterFixture(4, 2), HttpSecurityTests.RasterFixture(2, 5),
            HttpSecurityTests.RasterFixture(3, 3)
        })
        {
            using var content = new MemoryStream(bytes);
            await Assert.ThrowsAsync<CatalogImageException>(() =>
                service.UploadAsync([File(content)], CatalogImageArea.Products, owner));
        }
        using var source = new MemoryStream(valid);
        var file = File(source);
        await Assert.ThrowsAsync<CatalogImageException>(() =>
            service.UploadAsync([file, file, file], CatalogImageArea.Products, owner));
        Assert.Empty(fake.Writes);
    }

    private static FormFile File(MemoryStream stream) =>
        new(stream, 0, stream.Length, "images", "../untrusted.html")
        {
            Headers = new HeaderDictionary(), ContentType = "text/html"
        };
}
