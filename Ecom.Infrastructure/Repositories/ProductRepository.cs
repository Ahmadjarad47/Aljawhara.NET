using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ecom.Infrastructure.Repositories
{
    public class ProductRepository : BaseRepository<Product>, IProductRepository
    {
        public ProductRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId)
        {
            return await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.subCategory.CategoryId == categoryId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsBySubCategoryAsync(int subCategoryId)
        {
            return await _dbSet
                .Include(p => p.subCategory)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.SubCategoryId == subCategoryId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 10)
        {
            return await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm)
        {
            return await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.Title.Contains(searchTerm) || 
                           p.TitleAr.Contains(searchTerm) ||
                           p.Description.Contains(searchTerm) ||
                           p.DescriptionAr.Contains(searchTerm) ||
                           p.subCategory.Name.Contains(searchTerm) ||
                           p.subCategory.NameAr.Contains(searchTerm) ||
                           p.subCategory.Category.Name.Contains(searchTerm) ||
                           p.subCategory.Category.NameAr.Contains(searchTerm))
                .ToListAsync();
        }

        public async Task<(IEnumerable<Product> Products, int TotalCount)> GetProductsWithFiltersAsync(
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
            int pageSize = 20)
        {
            IQueryable<Product>? query = _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.subCategory.CategoryId == categoryId.Value);

            if (subCategoryId.HasValue)
                query = query.Where(p => p.SubCategoryId == subCategoryId.Value);

            if (minPrice.HasValue)
                query = query.Where(p => p.newPrice >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.newPrice <= maxPrice.Value);

            if (isActive.HasValue)
                query = query.Where(p => p.IsActive == isActive.Value);

            // New filters
            if (inStock.HasValue)
                query = query.Where(p => p.IsInStock == inStock.Value);

            if (onSale.HasValue && onSale.Value)
                query = query.Where(p => p.oldPrice > p.newPrice);

            if (newArrival.HasValue && newArrival.Value)
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                query = query.Where(p => p.CreatedAt >= thirtyDaysAgo);
            }

            // Best Discount filter - products with highest discount percentage
            if (bestDiscount.HasValue && bestDiscount.Value)
            {
                query = query.Where(p => p.oldPrice > 0 && p.oldPrice > p.newPrice);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Title.Contains(searchTerm) || 
                                        p.TitleAr.Contains(searchTerm) ||
                                        p.Description.Contains(searchTerm) ||
                                        p.DescriptionAr.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();

            // Apply sorting
            switch (sortBy?.ToLower())
            {
                case "newest":
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;
                case "oldest":
                    query = query.OrderBy(p => p.CreatedAt);
                    break;
                case "highrating":
                    query = query.OrderByDescending(p => p.Ratings.Any() ? p.Ratings.Average(r => r.RatingNumber) : 0);
                    break;
                case "lowrating":
                    query = query.OrderBy(p => p.Ratings.Any() ? p.Ratings.Average(r => r.RatingNumber) : 0);
                    break;
                case "mostrating":
                    // Sort products by highest average RatingNumber (stars)
                    query = query.OrderByDescending(p => p.Ratings.Any()
                        ? p.Ratings.Average(r => r.RatingNumber)
                        : 0);
                    break;
                case "bestdiscount":
                    query = query.OrderByDescending(p => Math.Round(((p.oldPrice - p.newPrice) / p.oldPrice) * 100, 2));

                    break;
                default:
                    query = query.OrderBy(p => p.Title);
                    break;
            }

            var products = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (products, totalCount);
        }

        public async Task<Product?> GetProductWithDetailsAsync(int productId)
        {
            return await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .FirstOrDefaultAsync(p => p.Id == productId);
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count = 5)
        {
            var product = await _dbSet.FindAsync(productId);
            if (product == null)
                return new List<Product>();

            return await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.SubCategoryId == product.SubCategoryId && p.Id != productId)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetAllProductsWithDetailsAsync()
        {
            return await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .ToListAsync();
        }
    }
}

