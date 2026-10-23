using System.Text.Json;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Validation;

namespace Ecom.AIShopping.Infrastructure.OpenAI;

public sealed class ShoppingToolExecutor(IProductRetrievalService retrieval, IProductCatalogReader catalog)
{
    private sealed record SearchArguments(string Query, string? Material);
    private sealed record DetailsArguments(int ProductId);

    public async Task<string> ExecuteAsync(string name, string arguments, CancellationToken ct)
    {
        if (arguments.Length > 1000) throw new ShoppingException("INVALID_TOOL_ARGUMENTS", 502);
        try
        {
            IReadOnlyList<CatalogProduct> products;
            switch (name)
            {
                case "search_products":
                    var search = JsonSerializer.Deserialize<SearchArguments>(arguments, ShoppingJson.Options)
                        ?? throw new ShoppingException("INVALID_TOOL_ARGUMENTS", 502);
                    if (!ShoppingValidation.Text(search.Query, 160) || search.Material?.Length > 80)
                        throw new ShoppingException("INVALID_TOOL_ARGUMENTS", 502);
                    products = await retrieval.SearchAsync(search.Query, search.Material, 6, ct);
                    break;
                case "get_product_details":
                    var details = JsonSerializer.Deserialize<DetailsArguments>(arguments, ShoppingJson.Options);
                    if (details is null || details.ProductId <= 0) throw new ShoppingException("INVALID_TOOL_ARGUMENTS", 502);
                    products = await catalog.GetAsync([details.ProductId], ct);
                    break;
                default: throw new ShoppingException("UNAUTHORIZED_TOOL", 502);
            }
            // Compact public catalog facts only; no profiles, carts, orders or authentication context.
            return JsonSerializer.Serialize(new { untrustedCatalogData = products.Select(p => new
            { p.ProductId, Name = Compact(p.Name, 120), NameEn = Compact(p.NameEn, 120), Description = Compact(p.Description, 160),
                Category = Compact(p.Category, 100), p.UnitPrice, p.AvailablePackages,
                p.UnitsPerPackage, p.Material, p.RequiresVariant }) }, ShoppingJson.Options);
        }
        catch (JsonException) { throw new ShoppingException("INVALID_TOOL_ARGUMENTS", 502); }
    }
    private static string Compact(string value, int length) => value[..Math.Min(value.Length, length)];
}
