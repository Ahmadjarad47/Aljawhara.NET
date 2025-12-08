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
    }
}

