using AutoMapper;
using Ecom.Application.DTOs.Category;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.UnitOfWork;

namespace Ecom.Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CategoryService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
        {
            var category = await _unitOfWork.Categories.GetCategoryWithSubCategoriesAsync(id);
            return category != null ? _mapper.Map<CategoryDto>(category) : null;
        }

        public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync()
        {
            var categories = await _unitOfWork.Categories.GetAllAsync();
            return _mapper.Map<IEnumerable<CategoryDto>>(categories);
        }

        public async Task<IEnumerable<CategoryDto>> GetCategoriesWithSubCategoriesAsync()
        {
            var categories = await _unitOfWork.Categories.GetCategoriesWithSubCategoriesAsync();
            return _mapper.Map<IEnumerable<CategoryDto>>(categories);
        }

        public async Task<CategoryDto> CreateCategoryAsync(CategoryCreateDto categoryDto)
        {
            var category = _mapper.Map<Category>(categoryDto);
            
            await _unitOfWork.Categories.AddAsync(category);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CategoryDto>(category);
        }

        public async Task<CategoryDto> UpdateCategoryAsync(CategoryUpdateDto categoryDto)
        {
            var existingCategory = await _unitOfWork.Categories.GetByIdAsync(categoryDto.Id);
            if (existingCategory == null)
                throw new ArgumentException($"Category with ID {categoryDto.Id} not found.");

            _mapper.Map(categoryDto, existingCategory);
            _unitOfWork.Categories.Update(existingCategory);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CategoryDto>(existingCategory);
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return false;

            // Check if category has subcategories
            var hasSubCategories = await _unitOfWork.SubCategories.ExistsAsync(sc => sc.CategoryId == id);
            if (hasSubCategories)
                throw new InvalidOperationException("Cannot delete category that has subcategories.");

            _unitOfWork.Categories.SoftDelete(category);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<SubCategoryDto?> GetSubCategoryByIdAsync(int id)
        {
            var subCategory = await _unitOfWork.SubCategories.GetByIdAsync(id);
            return subCategory != null ? _mapper.Map<SubCategoryDto>(subCategory) : null;
        }

        public async Task<IEnumerable<SubCategoryDto>> GetSubCategoriesByCategoryAsync(int categoryId)
        {
            var subCategories = await _unitOfWork.SubCategories.FindAsync(sc => sc.CategoryId == categoryId);
            return _mapper.Map<IEnumerable<SubCategoryDto>>(subCategories);
        }

        public async Task<IEnumerable<SubCategoryDto>> GetSubCategoriesByCategoryWithIncludesAsync(int categoryId)
        {
            var subCategories = await _unitOfWork.SubCategories.GetSubCategoriesByCategoryWithProductsAsync(categoryId);
            return _mapper.Map<IEnumerable<SubCategoryDto>>(subCategories);
        }

        public async Task<SubCategoryDto> CreateSubCategoryAsync(SubCategoryCreateDto subCategoryDto)
        {
            var subCategory = _mapper.Map<SubCategory>(subCategoryDto);
            
            await _unitOfWork.SubCategories.AddAsync(subCategory);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<SubCategoryDto>(subCategory);
        }

        public async Task<SubCategoryDto> UpdateSubCategoryAsync(SubCategoryUpdateDto subCategoryDto)
        {
            var existingSubCategory = await _unitOfWork.SubCategories.GetByIdAsync(subCategoryDto.Id);
            if (existingSubCategory == null)
                throw new ArgumentException($"SubCategory with ID {subCategoryDto.Id} not found.");

            _mapper.Map(subCategoryDto, existingSubCategory);
            _unitOfWork.SubCategories.Update(existingSubCategory);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<SubCategoryDto>(existingSubCategory);
        }

        public async Task<bool> DeleteSubCategoryAsync(int id)
        {
            var subCategory = await _unitOfWork.SubCategories.GetByIdAsync(id);
            if (subCategory == null)
                return false;

            // Check if subcategory has products
            var hasProducts = await _unitOfWork.Products.ExistsAsync(p => p.SubCategoryId == id);
            if (hasProducts)
                throw new InvalidOperationException("Cannot delete subcategory that has products.");

            _unitOfWork.SubCategories.SoftDelete(subCategory);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<SubCategoryDto>> GetAllSubCategory()
        {
            var result = await _unitOfWork.SubCategories.GetAllAsync();
            return _mapper.Map<IEnumerable<SubCategoryDto>>(result);
        }

        public async Task<IEnumerable<SubCategoryDto>> GetAllSubCategoryWithIncludes()
        {
            var result = await _unitOfWork.SubCategories.GetSubCategoriesWithCategoryAndProductsAsync();
            return _mapper.Map<IEnumerable<SubCategoryDto>>(result);
        }
    }
}





