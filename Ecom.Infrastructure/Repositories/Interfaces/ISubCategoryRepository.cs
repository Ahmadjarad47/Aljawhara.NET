using Ecom.Domain.Entity;
using System.Linq.Expressions;

namespace Ecom.Infrastructure.Repositories.Interfaces
{
    public interface ISubCategoryRepository : IBaseRepository<SubCategory>
    {
        Task<IEnumerable<SubCategory>> GetSubCategoriesWithCategoryAsync();
        Task<IEnumerable<SubCategory>> GetSubCategoriesWithCategoryAndProductsAsync();
        Task<SubCategory?> GetSubCategoryWithCategoryAsync(int id);
        Task<SubCategory?> GetSubCategoryWithCategoryAndProductsAsync(int id);
        Task<IEnumerable<SubCategory>> GetSubCategoriesByCategoryWithProductsAsync(int categoryId);
        Task<(IEnumerable<SubCategory> Items, int TotalCount)> GetPagedWithIncludesAsync(
            int pageNumber, 
            int pageSize, 
            Expression<Func<SubCategory, bool>>? predicate = null,
            Func<IQueryable<SubCategory>, IOrderedQueryable<SubCategory>>? orderBy = null,
            params Expression<Func<SubCategory, object>>[] includes);
    }
}
