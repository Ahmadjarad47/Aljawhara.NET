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
        Task<SubCategoryDto?> GetSubCategoryByIdAsync(int id);
        Task<IEnumerable<SubCategoryDto>> GetSubCategoriesByCategoryAsync(int categoryId);
        Task<IEnumerable<SubCategoryDto>> GetSubCategoriesByCategoryWithIncludesAsync(int categoryId);
        Task<SubCategoryDto> CreateSubCategoryAsync(SubCategoryCreateDto subCategoryDto);
        Task<SubCategoryDto> UpdateSubCategoryAsync(SubCategoryUpdateDto subCategoryDto);
        Task<bool> DeleteSubCategoryAsync(int id);
    }
}





