using Ecom.Domain.Entity;

namespace Ecom.Infrastructure.Repositories.Interfaces
{
    public interface ISubCategoryRepository : IBaseRepository<SubCategory>
    {
        Task<IEnumerable<SubCategory>> GetSubCategoriesWithCategoryAsync();
        Task<IEnumerable<SubCategory>> GetSubCategoriesWithCategoryAndProductsAsync();
        Task<SubCategory?> GetSubCategoryWithCategoryAsync(int id);
        Task<SubCategory?> GetSubCategoryWithCategoryAndProductsAsync(int id);
        Task<IEnumerable<SubCategory>> GetSubCategoriesByCategoryWithProductsAsync(int categoryId);
    }
}
