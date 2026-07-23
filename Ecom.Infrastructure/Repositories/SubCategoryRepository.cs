using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Linq.Expressions;

namespace Ecom.Infrastructure.Repositories
{
    public class SubCategoryRepository : BaseRepository<SubCategory>, ISubCategoryRepository
    {
        public SubCategoryRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }

        public async Task<IEnumerable<SubCategory>> GetSubCategoriesWithCategoryAsync()
        {
            return await _dbSet
                .Include(sc => sc.Category)
                .OrderBy(sc => sc.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<SubCategory>> GetSubCategoriesWithCategoryAndProductsAsync()
        {
            return await _dbSet
                .Include(sc => sc.Category)
                .ToListAsync();
        }

        public async Task<SubCategory?> GetSubCategoryWithCategoryAsync(int id)
        {
            return await _dbSet
                .Include(sc => sc.Category)
                .FirstOrDefaultAsync(sc => sc.Id == id);
        }

        public async Task<SubCategory?> GetSubCategoryWithCategoryAndProductsAsync(int id)
        {
            return await _dbSet
                .Include(sc => sc.Category)
                .Include(sc => sc.Products)
                .FirstOrDefaultAsync(sc => sc.Id == id);
        }

        public async Task<IEnumerable<SubCategory>> GetSubCategoriesByCategoryWithProductsAsync(int categoryId)
        {
            return await _dbSet
                .Include(sc => sc.Category)
                .Include(sc => sc.Products)
                .Where(sc => sc.CategoryId == categoryId)
                .OrderBy(sc => sc.Name)
                .ToListAsync();
        }

        public async Task<(IEnumerable<SubCategory> Items, int TotalCount)> GetPagedWithIncludesAsync(
            int pageNumber, 
            int pageSize, 
            Expression<Func<SubCategory, bool>>? predicate = null,
            Func<IQueryable<SubCategory>, IOrderedQueryable<SubCategory>>? orderBy = null,
            params Expression<Func<SubCategory, object>>[] includes)
        {
            var query = _dbSet.AsQueryable();

            // Apply includes first
          query=query.Include(sc => sc.Category);

            // Apply predicate
            if (predicate != null)
                query = query.Where(predicate);

            var totalCount = await query.CountAsync();

            // Apply ordering
            if (orderBy != null)
                query = orderBy(query);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
