using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Infrastructure.Repositories
{
    public class SubCategoryRepository : BaseRepository<SubCategory>, ISubCategoryRepository
    {
        public SubCategoryRepository(EcomDbContext context) : base(context)
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
    }
}
