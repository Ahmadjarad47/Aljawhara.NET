using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Infrastructure.Repositories
{
    public class ProductRepository : BaseRepository<Product>, IProductRepository
    {
        public ProductRepository(EcomDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId)
        {
            return await _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .Where(p => p.subCategory.CategoryId == categoryId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsBySubCategoryAsync(int subCategoryId)
        {
            return await _dbSet
                .Include(p => p.subCategory)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
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
                .Where(p => p.Title.Contains(searchTerm) || 
                           p.Description.Contains(searchTerm) ||
                           p.subCategory.Name.Contains(searchTerm) ||
                           p.subCategory.Category.Name.Contains(searchTerm))
                .ToListAsync();
        }

        public async Task<(IEnumerable<Product> Products, int TotalCount)> GetProductsWithFiltersAsync(
            int? categoryId = null,
            int? subCategoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? searchTerm = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var query = _dbSet
                .Include(p => p.subCategory)
                .ThenInclude(sc => sc.Category)
                .Include(p => p.productDetails)
                .Include(p => p.Ratings)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.subCategory.CategoryId == categoryId.Value);

            if (subCategoryId.HasValue)
                query = query.Where(p => p.SubCategoryId == subCategoryId.Value);

            if (minPrice.HasValue)
                query = query.Where(p => p.newPrice >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.newPrice <= maxPrice.Value);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Title.Contains(searchTerm) || 
                                        p.Description.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderBy(p => p.Title)
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
                .ToListAsync();
        }
    }
}

