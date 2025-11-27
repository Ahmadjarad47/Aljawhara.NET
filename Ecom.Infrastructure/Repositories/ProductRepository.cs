using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;

namespace Ecom.Infrastructure.Repositories
{
    public class ProductRepository : BaseRepository<Product>, IProductRepository
    {
        public ProductRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId)
        {
            var products = await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.subCategory.CategoryId == categoryId)
                .ToListAsync();

            return products;
        }

        public async Task<IEnumerable<Product>> GetProductsBySubCategoryAsync(int subCategoryId)
        {
            var products = await _dbSet
                .Include(p => p.subCategory)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.SubCategoryId == subCategoryId)
                .ToListAsync();

            return products;
        }

        public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 10)
        {
            var products = await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();

            return products;
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<Product>();

            var tokens = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            IQueryable<Product> query = _dbSet.Where(m=>m.IsActive==true && m.IsDeleted==false).AsNoTracking();

            foreach (var token in tokens)
            {
                var t = token; // local copy for EF
                query = query.Where(p =>
                    EF.Functions.Like(p.Title, $"{t}%") ||
                    EF.Functions.Like(p.TitleAr, $"{t}%") ||
                    EF.Functions.Like(p.Description, $"%{t}%") ||
                    EF.Functions.Like(p.DescriptionAr, $"%{t}%") ||
                    EF.Functions.Like(p.subCategory.Name, $"{t}%") ||
                    EF.Functions.Like(p.subCategory.NameAr, $"{t}%") ||
                    EF.Functions.Like(p.subCategory.Category.Name, $"{t}%") ||
                    EF.Functions.Like(p.subCategory.Category.NameAr, $"{t}%")
                );
            }
            var products = await query
                .Include(p => p.subCategory).ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants).ThenInclude(v => v.Values)
                .ToListAsync();

            return products;
        }

        // Helper: Normalize text
        public async Task<(IReadOnlyList<Product> Products, int TotalCount)>
      GetProductsWithFiltersAsync(
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
            IQueryable<Product> query = _dbSet
                .AsNoTracking()                    // ��� ���� ����
                .Where(p => p.IsDeleted == false && p.IsActive == true);

            if (categoryId.HasValue)
                query = query.Where(p => p.subCategory.CategoryId == categoryId);

            if (subCategoryId.HasValue)
                query = query.Where(p => p.SubCategoryId == subCategoryId);

            if (minPrice.HasValue)
                query = query.Where(p => p.newPrice >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.newPrice <= maxPrice.Value);

            if (isActive.HasValue)
                query = query.Where(p => p.IsActive == isActive.Value);

            if (inStock.HasValue && inStock.Value)
                query = query.Where(p => p.IsInStock == true);

            if (onSale.HasValue && onSale.Value)
                query = query.Where(p => p.oldPrice > p.newPrice);

            if (newArrival.HasValue && newArrival.Value)
            {
                var since = DateTime.UtcNow.AddDays(-30);
                query = query.Where(p => p.CreatedAt >= since);
            }

            if (bestDiscount.HasValue && bestDiscount.Value)
                query = query.Where(p => p.oldPrice > p.newPrice);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string t = searchTerm.Trim();
                query = query.Where(p =>
                    EF.Functions.Like(p.Title, $"%{t}%") ||
                    EF.Functions.Like(p.TitleAr, $"%{t}%"));
            }
            query = sortBy?.ToLower() switch
            {
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                "oldest" => query.OrderBy(p => p.CreatedAt),
                "highrating" => query.OrderByDescending(p =>
                    p.Ratings.Any() ? p.Ratings.Average(r => r.RatingNumber) : 0),
                "lowrating" => query.OrderBy(p =>
                    p.Ratings.Any() ? p.Ratings.Average(r => r.RatingNumber) : 0),
                "bestdiscount" => query.OrderByDescending(p =>
                    (p.oldPrice > 0) ? ((p.oldPrice - p.newPrice) / p.oldPrice) : 0),
                _ => query.OrderByDescending(p => p.Id)
            };
            int totalCount = await query.CountAsync();
            query = query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);
            var result = await query
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Values)
                .ToListAsync();
            var output = ((IReadOnlyList<Product>)result, totalCount);

            return output;
        }

        public async Task<Product?> GetProductWithDetailsAsync(int productId)
        {
            var product = await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .FirstOrDefaultAsync(p => p.Id == productId);

            return product;
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count = 5)
        {
            var product = await _dbSet.FindAsync(productId);
            if (product == null)
                return new List<Product>();

            var products = await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.SubCategoryId == product.SubCategoryId && p.Id != productId)
                .Take(count)
                .ToListAsync();

            return products;
        }

        public async Task<IEnumerable<Product>> GetAllProductsWithDetailsAsync()
        {
            var products = await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .ToListAsync();

            return products;
        }
    }
}

