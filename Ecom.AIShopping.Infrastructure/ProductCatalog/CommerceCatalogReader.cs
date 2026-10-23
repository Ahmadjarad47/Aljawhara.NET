using System.Globalization;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Ecom.AIShopping.Validation;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecom.AIShopping.Infrastructure.ProductCatalog;

public sealed class CommerceCatalogReader(EcomDbContext db, IOptions<AIShoppingOptions> options) : IProductCatalogReader
{
    public async Task<IReadOnlyList<CatalogProduct>> GetAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        if (ids.Count > 100 || ids.Any(i => i <= 0)) throw new ShoppingException("INVALID_PRODUCT_IDS");
        var rows = await EligibleQuery().Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        return rows.Select(ToProduct).ToArray();
    }

    public IQueryable<Product> EligibleQuery() => db.Products.AsNoTracking().AsSplitQuery()
        .Where(p => p.IsActive && !p.IsDeleted && p.IsInStock && p.TotalInStock > 0 && p.newPrice > 0 &&
            p.subCategory.IsActive && !p.subCategory.IsDeleted && p.subCategory.Category.IsActive && !p.subCategory.Category.IsDeleted)
        .Include(p => p.subCategory).ThenInclude(s => s.Category)
        .Include(p => p.productDetails.Where(d => d.IsActive && !d.IsDeleted))
        .Include(p => p.Variants.Where(v => v.IsActive && !v.IsDeleted));

    private CatalogProduct ToProduct(Product p) => new(p.Id, string.IsNullOrWhiteSpace(p.TitleAr) ? p.Title : p.TitleAr,
        p.Title, Limit(p.DescriptionAr + " " + p.Description, 1000),
        p.subCategory.Category.NameAr + " " + p.subCategory.NameAr, SafeImage(p.Images.FirstOrDefault()),
        ShoppingValidation.Money(p.newPrice), p.TotalInStock, PackageSize(p.productDetails),
        Detail(p.productDetails, "material", "المادة", "الخامة"), p.Variants.Count != 0);

    public static int? PackageSize(IEnumerable<ProductDetails> details)
    {
        var text = Detail(details, "package quantity", "units per package", "pack quantity", "عدد القطع", "عدد القطع في العبوة", "كمية العبوة");
        if (text is null) return null;
        var ascii = string.Concat(text.Trim().Select(c => c is >= '٠' and <= '٩' ? (char)('0' + c - '٠') :
            c is >= '۰' and <= '۹' ? (char)('0' + c - '۰') : c));
        return int.TryParse(ascii, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size is >= 1 and <= 10000 ? size : null;
    }

    private static string? Detail(IEnumerable<ProductDetails> details, params string[] names)
    {
        var values = details.Where(d => d.IsActive && !d.IsDeleted && names.Any(n =>
                string.Equals(d.Label.Trim(), n, StringComparison.OrdinalIgnoreCase) || d.LabelAr.Trim() == n))
            .Select(d => string.IsNullOrWhiteSpace(d.ValueAr) ? d.Value.Trim() : d.ValueAr.Trim()).Distinct().ToArray();
        return values.Length == 1 ? values[0] : null;
    }

    private string? SafeImage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\') && !value.Any(char.IsControl)) return value;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
            uri.UserInfo.Length == 0 && options.Value.ImageHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) ? uri.AbsoluteUri : null;
    }
    internal static string Limit(string text, int length) => text[..Math.Min(text.Length, length)];
}
