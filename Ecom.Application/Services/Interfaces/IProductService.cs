using Ecom.Application.DTOs.Product;

namespace Ecom.Application.Services.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto?> GetProductByIdAsync(int id);
        Task<IEnumerable<ProductSummaryDto>> GetAllProductsAsync();
        Task<IEnumerable<ProductSummaryDto>> GetProductsByCategoryAsync(int categoryId);
        Task<IEnumerable<ProductSummaryDto>> GetProductsBySubCategoryAsync(int subCategoryId);
        Task<IEnumerable<ProductSummaryDto>> GetFeaturedProductsAsync(int count = 10);
        Task<IEnumerable<ProductSummaryDto>> SearchProductsAsync(string searchTerm);
        Task<(IEnumerable<ProductSummaryDto> Products, int TotalCount)> GetProductsWithFiltersAsync(
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
        Task<IEnumerable<ProductSummaryDto>> GetRelatedProductsAsync(int productId, int count = 5);

        // New methods that return ProductDto with full details
        Task<IEnumerable<ProductDto>> GetAllProductsWithDetailsAsync();
        Task<IEnumerable<ProductDto>> GetProductsByCategoryWithDetailsAsync(int categoryId);
        Task<IEnumerable<ProductDto>> GetProductsBySubCategoryWithDetailsAsync(int subCategoryId);
        Task<IEnumerable<ProductDto>> GetFeaturedProductsWithDetailsAsync(int count = 10);
        Task<IEnumerable<ProductDto>> SearchProductsWithDetailsAsync(string searchTerm);
        Task<(IEnumerable<ProductDto> Products, int TotalCount)> GetProductsWithFiltersAndDetailsAsync(
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
        Task<IEnumerable<ProductDto>> GetRelatedProductsWithDetailsAsync(int productId, int count = 5);

        Task<ProductDto> CreateProductAsync(ProductCreateDto productDto);
        Task<ProductDto> CreateProductWithFilesAsync(ProductCreateWithFilesDto productDto);
        Task<ProductDto> UpdateProductAsync(ProductUpdateDto productDto);
        Task<ProductDto> UpdateProductWithFilesAsync(ProductUpdateWithFilesDto productDto);
        Task<bool> DeleteProductAsync(int id);

        Task<RatingDto> AddProductRatingAsync(RatingCreateDto ratingDto);
        Task<IEnumerable<RatingDto>> GetProductRatingsAsync(int productId);

        // Stock management methods
        Task<bool> UpdateProductStockAsync(int productId, int newStockQuantity);
        Task<bool> ReduceProductStockAsync(int productId, int quantity);
        Task<bool> IncreaseProductStockAsync(int productId, int quantity);
        Task<bool> SetProductInStockStatusAsync(int productId, bool isInStock);
        Task<IEnumerable<ProductSummaryDto>> GetInStockProductsAsync();
        Task<IEnumerable<ProductSummaryDto>> GetOutOfStockProductsAsync();
        Task<IEnumerable<ProductSummaryDto>> GetLowStockProductsAsync(int threshold = 10);

        // IsActive management methods
        Task<bool> ActivateProductAsync(int productId);
        Task<bool> DeactivateProductAsync(int productId);
        Task<IEnumerable<ProductSummaryDto>> GetAllProductsIncludingInactiveAsync();
    }
}





