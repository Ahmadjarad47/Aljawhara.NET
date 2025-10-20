using Ecom.Application.DTOs.Product;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<ActionResult<List<ProductSummaryDto>>> GetProducts(
            [FromQuery] int? categoryId = null,
            [FromQuery] int? subCategoryId = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchResults = await _productService.SearchProductsAsync(searchTerm);
                return Ok(searchResults);
            }

            if (categoryId.HasValue || subCategoryId.HasValue || minPrice.HasValue || maxPrice.HasValue)
            {
                var (products, totalCount) = await _productService.GetProductsWithFiltersAsync(
                    categoryId, subCategoryId, minPrice, maxPrice, searchTerm, pageNumber, pageSize);

                return Ok(new { Products = products, TotalCount = totalCount, PageNumber = pageNumber, PageSize = pageSize });
            }

            var allProducts = await _productService.GetAllProductsAsync();
            return Ok(allProducts.ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDto>> GetProduct(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound($"Product with ID {id} not found.");
            }
            return Ok(product);
        }

        [HttpGet("featured")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetFeaturedProducts([FromQuery] int count = 10)
        {
            var products = await _productService.GetFeaturedProductsAsync(count);
            return Ok(products);
        }

        [HttpGet("category/{categoryId}")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetProductsByCategory(int categoryId)
        {
            var products = await _productService.GetProductsByCategoryAsync(categoryId);
            return Ok(products);
        }

        [HttpGet("subcategory/{subCategoryId}")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetProductsBySubCategory(int subCategoryId)
        {
            var products = await _productService.GetProductsBySubCategoryAsync(subCategoryId);
            return Ok(products);
        }

        [HttpGet("{id}/related")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetRelatedProducts(int id, [FromQuery] int count = 5)
        {
            var products = await _productService.GetRelatedProductsAsync(id, count);
            return Ok(products);
        }


        [HttpGet("{id}/ratings")]
        public async Task<ActionResult<IEnumerable<RatingDto>>> GetProductRatings(int id)
        {
            var ratings = await _productService.GetProductRatingsAsync(id);
            return Ok(ratings);
        }

        [HttpPost("{id}/ratings")]
        public async Task<ActionResult<RatingDto>> AddProductRating(int id, [FromBody] RatingCreateDto ratingDto)
        {
            if (id != ratingDto.ProductId)
            {
                return BadRequest("Product ID mismatch.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var rating = await _productService.AddProductRatingAsync(ratingDto);
                return CreatedAtAction(nameof(GetProductRatings), new { id = id }, rating);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Stock management endpoints
        [HttpGet("in-stock")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetInStockProducts()
        {
            var products = await _productService.GetInStockProductsAsync();
            return Ok(products);
        }

        [HttpGet("out-of-stock")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetOutOfStockProducts()
        {
            var products = await _productService.GetOutOfStockProductsAsync();
            return Ok(products);
        }

        [HttpGet("low-stock")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetLowStockProducts([FromQuery] int threshold = 10)
        {
            var products = await _productService.GetLowStockProductsAsync(threshold);
            return Ok(products);
        }

        [HttpGet("{id}/stock")]
        public async Task<ActionResult<object>> GetProductStock(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound($"Product with ID {id} not found.");
            }

            return Ok(new
            {
                ProductId = id,
                IsInStock = product.IsInStock,
                TotalInStock = product.TotalInStock
            });
        }
    }
}

