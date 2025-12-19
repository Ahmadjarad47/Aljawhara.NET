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
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
                .Where(p => p.subCategory.CategoryId == categoryId && p.IsActive == true)
                .ToListAsync();

            return products;
        }

        public async Task<IEnumerable<Product>> GetProductsBySubCategoryAsync(int subCategoryId)
        {
            var products = await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.subCategory)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
                .Where(p => p.SubCategoryId == subCategoryId && p.IsActive == true)
                .ToListAsync();

            return products;
        }

        public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 10)
        {
            var products = await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
                .Where(p => p.IsActive == true)
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
            IQueryable<Product> query = _dbSet
                .Where(p => p.IsActive == true)
                .AsNoTracking();

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
                .AsSplitQuery()
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
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
            // Base query - IsDeleted is already filtered by query filter, only check IsActive
            IQueryable<Product> query = _dbSet
                .AsNoTracking()
                .Where(p => p.IsActive == true);

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

            // Optimized sorting - use subquery for rating averages to avoid complex CASE statements
            query = sortBy?.ToLower() switch
            {
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                "oldest" => query.OrderBy(p => p.CreatedAt),
                "highrating" => query.OrderByDescending(p =>
                    _context.Ratings
                        .Where(r => r.ProductId == p.Id && !r.IsDeleted)
                        .Select(r => (double?)r.RatingNumber)
                        .Average() ?? 0),
                "lowrating" => query.OrderBy(p =>
                    _context.Ratings
                        .Where(r => r.ProductId == p.Id && !r.IsDeleted)
                        .Select(r => (double?)r.RatingNumber)
                        .Average() ?? 0),
                "bestdiscount" => query.OrderByDescending(p =>
                    (p.oldPrice > 0) ? ((p.oldPrice - p.newPrice) / p.oldPrice) : 0),
                _ => query.OrderByDescending(p => p.Id)
            };

            // Get total count before pagination
            int totalCount = await query.CountAsync();

            // Apply pagination
            query = query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            // Load includes BEFORE executing query - use split query for better performance
            var result = await query
                .AsSplitQuery()
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
                .ToListAsync();

            var output = ((IReadOnlyList<Product>)result, totalCount);
            return output;
        }

        public async Task<Product?> GetProductWithDetailsAsync(int productId)
        {
            var product = await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive == true);

            return product;
        }

        public async Task<Product?> GetProductWithDetailsForUpdateAsync(int productId)
        {
            var product = await _dbSet
                .AsSplitQuery()
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive == true);

            return product;
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count = 5)
        {
            var product = await _dbSet
                .AsNoTracking()
                .Select(p => new { p.Id, p.SubCategoryId })
                .FirstOrDefaultAsync(p => p.Id == productId);
            
            if (product == null)
                return new List<Product>();

            var products = await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
                .Where(p => p.SubCategoryId == product.SubCategoryId && p.Id != productId && p.IsActive == true)
                .Take(count)
                .ToListAsync();

            return products;
        }

        public async Task<IEnumerable<Product>> GetAllProductsWithDetailsAsync()
        {
            var products = await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails.Where(pd => !pd.IsDeleted))
                .Include(p => p.Ratings.Where(r => !r.IsDeleted))
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Values.Where(vv => !vv.IsDeleted))
                .Where(p => p.IsActive == true)
                .ToListAsync();

            return products;
        }
    }
}

