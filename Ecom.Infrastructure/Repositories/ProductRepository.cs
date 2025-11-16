using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace Ecom.Infrastructure.Repositories
{
    public class ProductRepository : BaseRepository<Product>, IProductRepository
    {
        // Cache expiration policies
        private static readonly TimeSpan ProductDetailsCacheExpiration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan CategoryProductsCacheExpiration = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan FeaturedProductsCacheExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan SearchResultsCacheExpiration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FilteredProductsCacheExpiration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan RelatedProductsCacheExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan AllProductsCacheExpiration = TimeSpan.FromMinutes(30);

        // Cache key prefixes
        private const string ProductDetailsKeyPrefix = "Product_Details_";
        private const string CategoryProductsKeyPrefix = "Product_Category_";
        private const string SubCategoryProductsKeyPrefix = "Product_SubCategory_";
        private const string FeaturedProductsKeyPrefix = "Product_Featured_";
        private const string SearchProductsKeyPrefix = "Product_Search_";
        private const string FilteredProductsKeyPrefix = "Product_Filtered_";
        private const string RelatedProductsKeyPrefix = "Product_Related_";
        private const string AllProductsWithDetailsKey = "Product_AllWithDetails";
        private const string ProductCacheTag = "Product_Cache";

        public ProductRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }

        // Cache key generation helpers
        private string GetCategoryProductsCacheKey(int categoryId) => $"{CategoryProductsKeyPrefix}{categoryId}";
        private string GetSubCategoryProductsCacheKey(int subCategoryId) => $"{SubCategoryProductsKeyPrefix}{subCategoryId}";
        private string GetFeaturedProductsCacheKey(int count) => $"{FeaturedProductsKeyPrefix}{count}";
        private string GetProductDetailsCacheKey(int productId) => $"{ProductDetailsKeyPrefix}{productId}";
        private string GetRelatedProductsCacheKey(int productId, int count) => $"{RelatedProductsKeyPrefix}{productId}_{count}";
        private string GetAllProductsWithDetailsCacheKey() => AllProductsWithDetailsKey;

        // Generate hash-based cache key for search and filtered queries
        private string GenerateSearchCacheKey(string searchTerm)
        {
            var normalizedTerm = searchTerm?.Trim().ToLowerInvariant() ?? string.Empty;
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedTerm));
            var hashString = Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-").TrimEnd('=');
            return $"{SearchProductsKeyPrefix}{hashString}";
        }

        private string GenerateFilteredProductsCacheKey(
            int? categoryId, int? subCategoryId, decimal? minPrice, decimal? maxPrice,
            string? searchTerm, bool? isActive, string? sortBy, bool? inStock,
            bool? onSale, bool? newArrival, bool? bestDiscount, int pageNumber, int pageSize)
        {
            var keyBuilder = new StringBuilder(FilteredProductsKeyPrefix);
            keyBuilder.Append($"C{categoryId ?? 0}_");
            keyBuilder.Append($"SC{subCategoryId ?? 0}_");
            keyBuilder.Append($"Min{minPrice ?? 0}_");
            keyBuilder.Append($"Max{maxPrice ?? 0}_");
            keyBuilder.Append($"S{searchTerm?.Trim().ToLowerInvariant() ?? "null"}_");
            keyBuilder.Append($"A{isActive?.ToString() ?? "null"}_");
            keyBuilder.Append($"Sort{sortBy?.ToLowerInvariant() ?? "null"}_");
            keyBuilder.Append($"Stock{inStock?.ToString() ?? "null"}_");
            keyBuilder.Append($"Sale{onSale?.ToString() ?? "null"}_");
            keyBuilder.Append($"New{newArrival?.ToString() ?? "null"}_");
            keyBuilder.Append($"Disc{bestDiscount?.ToString() ?? "null"}_");
            keyBuilder.Append($"P{pageNumber}_");
            keyBuilder.Append($"S{pageSize}");

            var keyString = keyBuilder.ToString();
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
            var hashString = Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-").TrimEnd('=');
            return $"{FilteredProductsKeyPrefix}{hashString}";
        }

        // Cache invalidation methods
        private void InvalidateProductCaches(int? productId = null, int? categoryId = null, int? subCategoryId = null)
        {
            // Invalidate all product-related caches
            var keysToRemove = new List<string>
            {
                GetAllProductsWithDetailsCacheKey(),
                GetFeaturedProductsCacheKey(10),
                GetFeaturedProductsCacheKey(5),
                GetFeaturedProductsCacheKey(20)
            };

            if (productId.HasValue)
            {
                keysToRemove.Add(GetProductDetailsCacheKey(productId.Value));
                // Invalidate related products cache for this product
                for (int i = 1; i <= 10; i++)
                {
                    keysToRemove.Add(GetRelatedProductsCacheKey(productId.Value, i));
                }
            }

            if (categoryId.HasValue)
            {
                keysToRemove.Add(GetCategoryProductsCacheKey(categoryId.Value));
            }

            if (subCategoryId.HasValue)
            {
                keysToRemove.Add(GetSubCategoryProductsCacheKey(subCategoryId.Value));
            }

            // Remove all search and filtered caches (they're too numerous to track individually)
            // We'll use a cache tag pattern - store a version number that changes on invalidation
            var cacheVersionKey = $"{ProductCacheTag}_Version";
            if (_cache.TryGetValue(cacheVersionKey, out int currentVersion))
            {
                _cache.Set(cacheVersionKey, currentVersion + 1, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(365),
                    Size = 1
                });
            }
            else
            {
                _cache.Set(cacheVersionKey, 1, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(365),
                    Size = 1
                });
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }

        // Override base methods to invalidate product-specific caches
        public override async Task<Product> AddAsync(Product entity)
        {
            var result = await base.AddAsync(entity);
            // Invalidate caches after adding (category/subcategory will be loaded after save)
            InvalidateProductCaches(null, null, entity.SubCategoryId);
            return result;
        }

        public override void Update(Product entity)
        {
            base.Update(entity);
            InvalidateProductCaches(entity.Id, 
                entity.subCategory?.CategoryId, 
                entity.SubCategoryId);
        }

        public override void UpdateRange(IEnumerable<Product> entities)
        {
            base.UpdateRange(entities);
            var productList = entities.ToList();
            foreach (var entity in productList)
            {
                InvalidateProductCaches(entity.Id, 
                    entity.subCategory?.CategoryId, 
                    entity.SubCategoryId);
            }
        }

        public override void Remove(Product entity)
        {
            base.Remove(entity);
            InvalidateProductCaches(entity.Id, 
                entity.subCategory?.CategoryId, 
                entity.SubCategoryId);
        }

        public override void RemoveRange(IEnumerable<Product> entities)
        {
            base.RemoveRange(entities);
            var productList = entities.ToList();
            foreach (var entity in productList)
            {
                InvalidateProductCaches(entity.Id, 
                    entity.subCategory?.CategoryId, 
                    entity.SubCategoryId);
            }
        }

        public override void SoftDelete(Product entity)
        {
            base.SoftDelete(entity);
            InvalidateProductCaches(entity.Id, 
                entity.subCategory?.CategoryId, 
                entity.SubCategoryId);
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId)
        {
            var cacheKey = GetCategoryProductsCacheKey(categoryId);
            var cacheVersionKey = $"{ProductCacheTag}_Version";
            
            // Get cache version to invalidate search/filter caches
            var cacheVersion = _cache.Get<int?>(cacheVersionKey) ?? 0;
            var versionedCacheKey = $"{cacheKey}_v{cacheVersion}";

            if (_cache.TryGetValue(versionedCacheKey, out IEnumerable<Product>? cachedProducts))
            {
                return cachedProducts ?? Enumerable.Empty<Product>();
            }

            var products = await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.subCategory.CategoryId == categoryId)
                .ToListAsync();

            if (products.Any())
            {
                _cache.Set(versionedCacheKey, products, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CategoryProductsCacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(10),
                    Priority = CacheItemPriority.Normal,
                    Size = 2048
                });
            }

            return products;
        }

        public async Task<IEnumerable<Product>> GetProductsBySubCategoryAsync(int subCategoryId)
        {
            var cacheKey = GetSubCategoryProductsCacheKey(subCategoryId);
            var cacheVersionKey = $"{ProductCacheTag}_Version";
            var cacheVersion = _cache.Get<int?>(cacheVersionKey) ?? 0;
            var versionedCacheKey = $"{cacheKey}_v{cacheVersion}";

            if (_cache.TryGetValue(versionedCacheKey, out IEnumerable<Product>? cachedProducts))
            {
                return cachedProducts ?? Enumerable.Empty<Product>();
            }

            var products = await _dbSet
                .Include(p => p.subCategory)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .Where(p => p.SubCategoryId == subCategoryId)
                .ToListAsync();

            if (products.Any())
            {
                _cache.Set(versionedCacheKey, products, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CategoryProductsCacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(10),
                    Priority = CacheItemPriority.Normal,
                    Size = 2048
                });
            }

            return products;
        }

        public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 10)
        {
            var cacheKey = GetFeaturedProductsCacheKey(count);
            var cacheVersionKey = $"{ProductCacheTag}_Version";
            var cacheVersion = _cache.Get<int?>(cacheVersionKey) ?? 0;
            var versionedCacheKey = $"{cacheKey}_v{cacheVersion}";

            if (_cache.TryGetValue(versionedCacheKey, out IEnumerable<Product>? cachedProducts))
            {
                return cachedProducts ?? Enumerable.Empty<Product>();
            }

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

            if (products.Any())
            {
                _cache.Set(versionedCacheKey, products, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = FeaturedProductsCacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(5),
                    Priority = CacheItemPriority.High, // Featured products are important
                    Size = 2048
                });
            }

            return products;
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<Product>();

            // 1) Normalize Search Term
            var tokens = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // 2) Cache Handling
            var cacheKey = GenerateSearchCacheKey(searchTerm);
            var versionKey = $"{ProductCacheTag}_Version";
            var version = _cache.Get<int?>(versionKey) ?? 0;
            var finalKey = $"{cacheKey}_v{version}";

            if (_cache.TryGetValue(finalKey, out IEnumerable<Product>? cached))
                return cached ?? Enumerable.Empty<Product>();

            // 3) Query
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

          
            // 5) Fetch Includes AFTER filtering for better performance
            var products = await query
                .Include(p => p.subCategory).ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants).ThenInclude(v => v.Values)
                .ToListAsync();

            // 6) Cache Results
            _cache.Set(finalKey, products, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SearchResultsCacheExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(2),
                Priority = CacheItemPriority.Low,
                Size = 2048
            });

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
            // ============================
            //     1) BUILD CACHE KEY
            // ============================
            string cacheKey =
                $"products:{categoryId}:{subCategoryId}:{minPrice}:{maxPrice}:{searchTerm}:{isActive}:{sortBy}:{inStock}:{onSale}:{newArrival}:{bestDiscount}:{pageNumber}:{pageSize}";

            if (_cache.TryGetValue(cacheKey, out (IReadOnlyList<Product> Products, int TotalCount)? cached))
                return cached.Value;

            // ============================
            //     2) BASE QUERY
            // ============================
            IQueryable<Product> query = _dbSet
                .AsNoTracking()                    // ãåã ÌÏÇð ÌÏÇð
                .Where(p => p.IsDeleted == false && p.IsActive == true);

            // ============================
            //     3) FILTERS
            // ============================
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

            // Optimized Search (LIKE only on key columns)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string t = searchTerm.Trim();
                query = query.Where(p =>
                    EF.Functions.Like(p.Title, $"%{t}%") ||
                    EF.Functions.Like(p.TitleAr, $"%{t}%"));
            }

            // ============================
            //     4) SORTING
            // ============================
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

            // ============================
            //     5) COUNT BEFORE PAGING
            // ============================
            int totalCount = await query.CountAsync();

            // ============================
            //     6) PAGINATION (VERY IMPORTANT)
            // ============================
            query = query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            // ============================
            //     7) NOW APPLY INCLUDES
            // ============================
            var result = await query
                .Include(p => p.subCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Values)
                .ToListAsync();

            // ============================
            //     8) SAVE TO CACHE
            // ============================
            var output = ((IReadOnlyList<Product>)result, totalCount);

            _cache.Set(cacheKey, output, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                SlidingExpiration = TimeSpan.FromMinutes(2),
                Priority = CacheItemPriority.High
            });

            return output;
        }

        public async Task<Product?> GetProductWithDetailsAsync(int productId)
        {
            var cacheKey = GetProductDetailsCacheKey(productId);
            var cacheVersionKey = $"{ProductCacheTag}_Version";
            var cacheVersion = _cache.Get<int?>(cacheVersionKey) ?? 0;
            var versionedCacheKey = $"{cacheKey}_v{cacheVersion}";

            if (_cache.TryGetValue(versionedCacheKey, out Product? cachedProduct))
            {
                return cachedProduct;
            }

            var product = await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product != null)
            {
                _cache.Set(versionedCacheKey, product, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ProductDetailsCacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(15),
                    Priority = CacheItemPriority.High, // Product details are frequently accessed
                    Size = 512
                });
            }

            return product;
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count = 5)
        {
            var cacheKey = GetRelatedProductsCacheKey(productId, count);
            var cacheVersionKey = $"{ProductCacheTag}_Version";
            var cacheVersion = _cache.Get<int?>(cacheVersionKey) ?? 0;
            var versionedCacheKey = $"{cacheKey}_v{cacheVersion}";

            if (_cache.TryGetValue(versionedCacheKey, out IEnumerable<Product>? cachedProducts))
            {
                return cachedProducts ?? Enumerable.Empty<Product>();
            }

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

            if (products.Any())
            {
                _cache.Set(versionedCacheKey, products, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = RelatedProductsCacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(5),
                    Priority = CacheItemPriority.Normal,
                    Size = 2048
                });
            }

            return products;
        }

        public async Task<IEnumerable<Product>> GetAllProductsWithDetailsAsync()
        {
            var cacheKey = GetAllProductsWithDetailsCacheKey();
            var cacheVersionKey = $"{ProductCacheTag}_Version";
            var cacheVersion = _cache.Get<int?>(cacheVersionKey) ?? 0;
            var versionedCacheKey = $"{cacheKey}_v{cacheVersion}";

            if (_cache.TryGetValue(versionedCacheKey, out IEnumerable<Product>? cachedProducts))
            {
                return cachedProducts ?? Enumerable.Empty<Product>();
            }

            var products = await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Values)
                .ToListAsync();

            if (products.Any())
            {
                _cache.Set(versionedCacheKey, products, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = AllProductsCacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(15),
                    Priority = CacheItemPriority.Normal,
                    Size = 4096
                });
            }

            return products;
        }
    }
}

