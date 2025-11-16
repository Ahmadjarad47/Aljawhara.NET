using Ecom.Domain.Entity;

namespace Ecom.Infrastructure.Repositories.Interfaces
{
    public interface IProductRepository : IBaseRepository<Product>
    {

        Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId);
        Task<IEnumerable<Product>> GetProductsBySubCategoryAsync(int subCategoryId);
        Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 10);
        Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm);
        Task<(IReadOnlyList<Product> Products, int TotalCount)> GetProductsWithFiltersAsync(
            int? categoryId = null,
            int? subCategoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? searchTerm = null,
            bool? isActive = null,
            string? sortBy = null,
            bool? inStock = null,
            bool? onSale = null,
            bool? newArrival = null,
            bool? bestDiscount = null,
            int pageNumber = 1,
            int pageSize = 20);
        Task<Product?> GetProductWithDetailsAsync(int productId);
        Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count = 5);
        Task<IEnumerable<Product>> GetAllProductsWithDetailsAsync();
    }
}

