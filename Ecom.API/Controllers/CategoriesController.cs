using Ecom.Application.DTOs.Category;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories([FromQuery] bool includeSubCategories = false)
        {
            var categories = includeSubCategories
                ? await _categoryService.GetCategoriesWithSubCategoriesAsync()
                : await _categoryService.GetAllCategoriesAsync();

            return Ok(categories);
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


        [HttpGet("subcategories")]
        public async Task<ActionResult<IEnumerable<SubCategoryDto>>> GetSubCategories()
        {
            var subCategories = await _categoryService.GetAllSubCategory();
            return Ok(subCategories.ToList());
        }


        [HttpGet("{id}/subcategories")]
        public async Task<ActionResult<IEnumerable<SubCategoryDto>>> GetSubCategories(int id)
        {
            var subCategories = await _categoryService.GetSubCategoriesByCategoryAsync(id);
            return Ok(subCategories);
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

    }
}


