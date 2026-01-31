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
            var category = await _unitOfWork.Categories.GetActiveByIdAsync(id);
            if (category == null) return null;
            
            var categoryWithSubCategories = await _unitOfWork.Categories.GetCategoryWithSubCategoriesAsync(id);
            return categoryWithSubCategories != null ? _mapper.Map<CategoryDto>(categoryWithSubCategories) : null;
        }

        public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync()
        {
            var categories = await _unitOfWork.Categories.GetAllActiveAsync();
            return _mapper.Map<IEnumerable<CategoryDto>>(categories);
        }

        public async Task<IEnumerable<CategoryDto>> GetCategoriesWithSubCategoriesAsync()
        {
            var categories = await _unitOfWork.Categories.GetCategoriesWithProductCountAsync();
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

        public async Task<IEnumerable<SubCategoryDto>> GetAllSubCategoriesAsync()
        {
            var result = await _unitOfWork.SubCategories.GetAllAsync();
            return _mapper.Map<IEnumerable<SubCategoryDto>>(result);
        }

        public async Task<bool> ActivateCategoryAsync(int categoryId)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (category == null)
                return false;

            _unitOfWork.Categories.Activate(category);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateCategoryAsync(int categoryId)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (category == null)
                return false;

            _unitOfWork.Categories.Deactivate(category);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateSubCategoryAsync(int subCategoryId)
        {
            var subCategory = await _unitOfWork.SubCategories.GetByIdAsync(subCategoryId);
            if (subCategory == null)
                return false;

            _unitOfWork.SubCategories.Activate(subCategory);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateSubCategoryAsync(int subCategoryId)
        {
            var subCategory = await _unitOfWork.SubCategories.GetByIdAsync(subCategoryId);
            if (subCategory == null)
                return false;

            _unitOfWork.SubCategories.Deactivate(subCategory);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<CategoryDto>> GetAllCategoriesIncludingInactiveAsync()
        {
            var categories = await _unitOfWork.Categories.GetAllAsync();
            return _mapper.Map<IEnumerable<CategoryDto>>(categories);
        }

        public async Task<(IEnumerable<CategoryDto> Categories, int TotalCount)> GetCategoriesWithFiltersAsync(
            bool? isActive = null,
            string? searchTerm = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var predicate = BuildCategoryFilterPredicate(isActive, searchTerm);
            var orderBy = (IQueryable<Category> query) => query.OrderBy(c => c.Name);
            
            var (categories, totalCount) = await _unitOfWork.Categories.GetPagedCategoriesWithProductCountAsync(
                pageNumber, pageSize, predicate, orderBy);
            
            return (_mapper.Map<IEnumerable<CategoryDto>>(categories), totalCount);
        }

        public async Task<(IEnumerable<SubCategoryDto> SubCategories, int TotalCount)> GetSubCategoriesWithFiltersAsync(
            bool? isActive = null,
            string? searchTerm = null,
            int? categoryId = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var predicate = BuildSubCategoryFilterPredicate(isActive, searchTerm, categoryId);
            var orderBy = (IQueryable<SubCategory> query) => query.OrderBy(sc => sc.Name);
            
            var (subCategories, totalCount) = await _unitOfWork.SubCategories.GetPagedWithIncludesAsync(
                pageNumber, pageSize, predicate, orderBy);
            
            return (_mapper.Map<IEnumerable<SubCategoryDto>>(subCategories), totalCount);
        }

        private System.Linq.Expressions.Expression<Func<Category, bool>>? BuildCategoryFilterPredicate(bool? isActive, string? searchTerm)
        {
            System.Linq.Expressions.Expression<Func<Category, bool>>? predicate = null;

            if (isActive.HasValue)
            {
                predicate = c => c.IsActive == isActive.Value && !c.IsDeleted;
            }
            else
            {
                predicate = c => !c.IsDeleted;
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.ToLower();
                System.Linq.Expressions.Expression<Func<Category, bool>> searchPredicate = c => c.Name.ToLower().Contains(searchLower) || 
                                         c.NameAr.ToLower().Contains(searchLower) ||
                                         c.Description.ToLower().Contains(searchLower) ||
                                         c.DescriptionAr.ToLower().Contains(searchLower);
                
                predicate = CombinePredicates(predicate, searchPredicate);
            }

            return predicate;
        }

        private System.Linq.Expressions.Expression<Func<SubCategory, bool>>? BuildSubCategoryFilterPredicate(bool? isActive, string? searchTerm, int? categoryId)
        {
            System.Linq.Expressions.Expression<Func<SubCategory, bool>>? predicate = null;

            if (isActive.HasValue)
            {
                predicate = sc => sc.IsActive == isActive.Value && !sc.IsDeleted;
            }
            else
            {
                predicate = sc => !sc.IsDeleted;
            }

            if (categoryId.HasValue)
            {
                var categoryPredicate = (System.Linq.Expressions.Expression<Func<SubCategory, bool>>)(sc => sc.CategoryId == categoryId.Value);
                predicate = CombinePredicates(predicate, categoryPredicate);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.ToLower();
                var searchPredicate = (System.Linq.Expressions.Expression<Func<SubCategory, bool>>)(sc => 
                    sc.Name.ToLower().Contains(searchLower) || 
                    sc.NameAr.ToLower().Contains(searchLower) ||
                    sc.Description.ToLower().Contains(searchLower) ||
                    sc.DescriptionAr.ToLower().Contains(searchLower));
                
                predicate = CombinePredicates(predicate, searchPredicate);
            }

            return predicate;
        }

        private System.Linq.Expressions.Expression<Func<T, bool>> CombinePredicates<T>(
            System.Linq.Expressions.Expression<Func<T, bool>> predicate1,
            System.Linq.Expressions.Expression<Func<T, bool>> predicate2)
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
            var body = System.Linq.Expressions.Expression.AndAlso(
                System.Linq.Expressions.Expression.Invoke(predicate1, parameter),
                System.Linq.Expressions.Expression.Invoke(predicate2, parameter));
            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
        }
    }
}





