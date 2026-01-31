using System.Linq.Expressions;
using Ecom.Domain.Entity;

namespace Ecom.Infrastructure.Repositories.Interfaces
{
    public interface ICategoryRepository : IBaseRepository<Category>
    {
        Task<IEnumerable<Category>> GetCategoriesWithSubCategoriesAsync();
        Task<Category?> GetCategoryWithSubCategoriesAsync(int categoryId);
        Task<IEnumerable<Category>> GetCategoriesWithProductCountAsync();
        Task<(IEnumerable<Category> Items, int TotalCount)> GetPagedCategoriesWithProductCountAsync(
            int pageNumber, int pageSize, Expression<Func<Category, bool>>? predicate, Func<IQueryable<Category>, IOrderedQueryable<Category>>? orderBy);
    }
}

