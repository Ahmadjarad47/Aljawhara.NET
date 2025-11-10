using Ecom.Application.DTOs.Category;

namespace Ecom.Application.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryDto?> GetCategoryByIdAsync(int id);
        Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync();
        Task<IEnumerable<CategoryDto>> GetCategoriesWithSubCategoriesAsync();
        
        Task<CategoryDto> CreateCategoryAsync(CategoryCreateDto categoryDto);
        Task<CategoryDto> UpdateCategoryAsync(CategoryUpdateDto categoryDto);
        Task<bool> DeleteCategoryAsync(int id);

        Task<IEnumerable<SubCategoryDto>> GetAllSubCategory();
        Task<IEnumerable<SubCategoryDto>> GetAllSubCategoryWithIncludes();
        Task<IEnumerable<SubCategoryDto>> GetAllSubCategoriesAsync();
        Task<SubCategoryDto?> GetSubCategoryByIdAsync(int id);
        Task<IEnumerable<SubCategoryDto>> GetSubCategoriesByCategoryAsync(int categoryId);
        Task<IEnumerable<SubCategoryDto>> GetSubCategoriesByCategoryWithIncludesAsync(int categoryId);
        Task<SubCategoryDto> CreateSubCategoryAsync(SubCategoryCreateDto subCategoryDto);
        Task<SubCategoryDto> UpdateSubCategoryAsync(SubCategoryUpdateDto subCategoryDto);
        Task<bool> DeleteSubCategoryAsync(int id);

        // IsActive management methods
        Task<bool> ActivateCategoryAsync(int categoryId);
        Task<bool> DeactivateCategoryAsync(int categoryId);
        Task<bool> ActivateSubCategoryAsync(int subCategoryId);
        Task<bool> DeactivateSubCategoryAsync(int subCategoryId);
        Task<IEnumerable<CategoryDto>> GetAllCategoriesIncludingInactiveAsync();

        // Filtering methods
        Task<(IEnumerable<CategoryDto> Categories, int TotalCount)> GetCategoriesWithFiltersAsync(
            bool? isActive = null,
            string? searchTerm = null,
            int pageNumber = 1,
            int pageSize = 20);
        Task<(IEnumerable<SubCategoryDto> SubCategories, int TotalCount)> GetSubCategoriesWithFiltersAsync(
            bool? isActive = null,
            string? searchTerm = null,
            int? categoryId = null,
            int pageNumber = 1,
            int pageSize = 20);
    }
}





