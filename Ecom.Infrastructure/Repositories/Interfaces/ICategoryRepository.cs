using Ecom.Domain.Entity;

namespace Ecom.Infrastructure.Repositories.Interfaces
{
    public interface ICategoryRepository : IBaseRepository<Category>
    {
        Task<IEnumerable<Category>> GetCategoriesWithSubCategoriesAsync();
        Task<Category?> GetCategoryWithSubCategoriesAsync(int categoryId);
        Task<IEnumerable<Category>> GetCategoriesWithProductCountAsync();
    }
}

