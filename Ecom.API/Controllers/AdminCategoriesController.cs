using Ecom.Application.DTOs.Category;
using Ecom.Application.DTOs.Common;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
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
            [FromQuery] bool includeSubCategories = false,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allCategories = includeSubCategories
                ? await _categoryService.GetCategoriesWithSubCategoriesAsync()
                : await _categoryService.GetAllCategoriesAsync();

            var categoriesList = allCategories.ToList();
            var totalCount = categoriesList.Count;

            var pagedCategories = categoriesList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<CategoryDto>(pagedCategories, totalCount, pageNumber, pageSize);
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
                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
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
            [FromQuery] bool includeRelated = true,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allSubCategories = includeRelated
                ? await _categoryService.GetAllSubCategoryWithIncludes()
                : await _categoryService.GetAllSubCategory();
            var subCategoriesList = allSubCategories.ToList();
            var totalCount = subCategoriesList.Count;

            var pagedSubCategories = subCategoriesList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<SubCategoryDto>(pagedSubCategories, totalCount, pageNumber, pageSize);
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
    }
}
