using System.Linq.Expressions;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ecom.Infrastructure.Repositories
{
    public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
    {
        public CategoryRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }

        public async Task<IEnumerable<Category>> GetCategoriesWithSubCategoriesAsync()
        {
            return await _dbSet
                .AsNoTracking()
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetCategoryWithSubCategoriesAsync(int categoryId)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                    .ThenInclude(sc => sc.Products.Where(p => !p.IsDeleted && p.IsActive))
                .FirstOrDefaultAsync(c => c.Id == categoryId);
        }

        public async Task<IEnumerable<Category>> GetCategoriesWithProductCountAsync()
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                    .ThenInclude(sc => sc.Products.Where(p => !p.IsDeleted && p.IsActive))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<(IEnumerable<Category> Items, int TotalCount)> GetPagedCategoriesWithProductCountAsync(
            int pageNumber, int pageSize, Expression<Func<Category, bool>>? predicate, Func<IQueryable<Category>, IOrderedQueryable<Category>>? orderBy)
        {
            var query = _dbSet.AsNoTracking().AsQueryable();

            if (predicate != null)
                query = query.Where(predicate);

            var totalCount = await query.CountAsync();

            query = query
                .AsSplitQuery()
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                    .ThenInclude(sc => sc.Products.Where(p => !p.IsDeleted && p.IsActive));

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

