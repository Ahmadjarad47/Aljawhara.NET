using Ecom.Application.DTOs.Category;
using Ecom.Application.DTOs.Common;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/categories")]
    [Authorize(Roles = "Admin")]
    public class AdminCategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public AdminCategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetCategories(
            [FromQuery] bool? isActive = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var (categories, totalCount) = await _categoryService.GetCategoriesWithFiltersAsync(
                isActive, searchTerm, pageNumber, pageSize);

            PagedResult<CategoryDto>? result = new PagedResult<CategoryDto>(categories.ToList(), totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CategoryDto>> GetCategory(int id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound($"Category with ID {id} not found.");
            }
            return Ok(category);
        }

        [HttpPost]
        public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CategoryCreateDto categoryDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var category = await _categoryService.CreateCategoryAsync(categoryDto);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpDelete("bulk")]
        public async Task<ActionResult<CategoryDto>>bulk([FromBody] List<int> ids)
        {
            try
            {
                foreach (var id in ids)
                {
                    var result = await _categoryService.DeleteCategoryAsync(id);
                    if (!result)
                    {
                        return NotFound($"Category with ID {id} not found.");
                    }
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPut("{id}")]
        public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, [FromBody] CategoryUpdateDto categoryDto)
        {
            if (id != categoryDto.Id)
            {
                return BadRequest("ID mismatch.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var category = await _categoryService.UpdateCategoryAsync(categoryDto);
                return Ok(category);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteCategory(int id)
        {
            try
            {
                var result = await _categoryService.DeleteCategoryAsync(id);
                if (!result)
                {
                    return NotFound($"Category with ID {id} not found.");
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("subcategories")]
        public async Task<ActionResult<PagedResult<SubCategoryDto>>> GetSubCategories(
            [FromQuery] bool? isActive = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var (subCategories, totalCount) = await _categoryService.GetSubCategoriesWithFiltersAsync(
                isActive, searchTerm, categoryId, pageNumber, pageSize);

            PagedResult<SubCategoryDto>? result = new PagedResult<SubCategoryDto>(subCategories.ToList(), totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}/subcategories")]
        public async Task<ActionResult<PagedResult<SubCategoryDto>>> GetSubCategories(
            int id,
            [FromQuery] bool includeRelated = true,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allSubCategories = includeRelated
                ? await _categoryService.GetSubCategoriesByCategoryWithIncludesAsync(id)
                : await _categoryService.GetSubCategoriesByCategoryAsync(id);
            var subCategoriesList = allSubCategories.ToList();
            var totalCount = subCategoriesList.Count;

            var pagedSubCategories = subCategoriesList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<SubCategoryDto>(pagedSubCategories, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpPost("subcategories")]
        public async Task<ActionResult<SubCategoryDto>> CreateSubCategory([FromBody] SubCategoryCreateDto subCategoryDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var subCategory = await _categoryService.CreateSubCategoryAsync(subCategoryDto);
                return CreatedAtAction(nameof(GetSubCategory), new { id = subCategory.Id }, subCategory);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("subcategories/{id}")]
        public async Task<ActionResult<SubCategoryDto>> GetSubCategory(int id)
        {
            var subCategory = await _categoryService.GetSubCategoryByIdAsync(id);
            if (subCategory == null)
            {
                return NotFound($"SubCategory with ID {id} not found.");
            }
            return Ok(subCategory);
        }

        [HttpPut("subcategories/{id}")]
        public async Task<ActionResult<SubCategoryDto>> UpdateSubCategory(int id, [FromBody] SubCategoryUpdateDto subCategoryDto)
        {
            if (id != subCategoryDto.Id)
            {
                return BadRequest("ID mismatch.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var subCategory = await _categoryService.UpdateSubCategoryAsync(subCategoryDto);
                return Ok(subCategory);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("subcategories/{id}")]
        public async Task<ActionResult> DeleteSubCategory(int id)
        {
            try
            {
                var result = await _categoryService.DeleteSubCategoryAsync(id);
                if (!result)
                {
                    return NotFound($"SubCategory with ID {id} not found.");
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}/toggle-active")]
        public async Task<ActionResult<object>> ToggleCategoryActive(int id)
        {
            try
            {
                var category = await _categoryService.GetCategoryByIdAsync(id);
                if (category == null)
                {
                    return NotFound($"Category with ID {id} not found.");
                }

                bool newStatus;
                if (category.IsActive)
                {
                    var result = await _categoryService.DeactivateCategoryAsync(id);
                    newStatus = false;
                }
                else
                {
                    var result = await _categoryService.ActivateCategoryAsync(id);
                    newStatus = true;
                }

                return Ok(new
                {
                    CategoryId = id,
                    IsActive = newStatus,
                    Message = $"Category {(newStatus ? "activated" : "deactivated")} successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("subcategories/{id}/toggle-active")]
        public async Task<ActionResult<object>> ToggleSubCategoryActive(int id)
        {
            try
            {
                var subCategory = await _categoryService.GetSubCategoryByIdAsync(id);
                if (subCategory == null)
                {
                    return NotFound($"SubCategory with ID {id} not found.");
                }

                bool newStatus;
                if (subCategory.IsActive)
                {
                    var result = await _categoryService.DeactivateSubCategoryAsync(id);
                    newStatus = false;
                }
                else
                {
                    var result = await _categoryService.ActivateSubCategoryAsync(id);
                    newStatus = true;
                }

                return Ok(new
                {
                    SubCategoryId = id,
                    IsActive = newStatus,
                    Message = $"SubCategory {(newStatus ? "activated" : "deactivated")} successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("all")]
        public async Task<ActionResult<Dictionary<int, string>>> GetAllCategories()
        {
            try
            {
                var categories = await _categoryService.GetAllCategoriesAsync();
                Dictionary<int, string>? result = categories.ToDictionary(c => c.Id, c => c.Name);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("subcategories/all")]
        public async Task<ActionResult<Dictionary<int, string>>> GetAllSubCategories()
        {
            try
            {
                var subCategories = await _categoryService.GetAllSubCategoriesAsync();
                var result = subCategories.ToDictionary(sc => sc.Id, sc => sc.Name);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
