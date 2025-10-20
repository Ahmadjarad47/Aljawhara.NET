using Ecom.Application.DTOs.Common;
using Ecom.Application.DTOs.Product;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public AdminProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts(
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
                var allSearchResults = await _productService.SearchProductsWithDetailsAsync(searchTerm);
                List<ProductDto>? searchResultsList = allSearchResults.ToList();
                var searchTotalCount = searchResultsList.Count;

                var pagedSearchResults = searchResultsList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                PagedResult<ProductDto>? result = new PagedResult<ProductDto>(pagedSearchResults, searchTotalCount, pageNumber, pageSize);
                return Ok(result);
            }

            if (categoryId.HasValue || subCategoryId.HasValue || minPrice.HasValue || maxPrice.HasValue)
            {
                var (products, totalCount) = await _productService.GetProductsWithFiltersAndDetailsAsync(
                    categoryId, subCategoryId, minPrice, maxPrice, searchTerm, pageNumber, pageSize);

                var result = new PagedResult<ProductDto>(products.ToList(), totalCount, pageNumber, pageSize);
                return Ok(result);
            }

            var allProducts = await _productService.GetAllProductsWithDetailsAsync();
            var allProductsList = allProducts.ToList();
            var allTotalCount = allProductsList.Count;

            var pagedAllProducts = allProductsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var allResult = new PagedResult<ProductDto>(pagedAllProducts, allTotalCount, pageNumber, pageSize);
            return Ok(allResult);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDto>> GetProduct(int id)
        {
            ProductDto? product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound($"Product with ID {id} not found.");
            }
            return Ok(product);
        }

        [HttpGet("featured")]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetFeaturedProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var allProducts = await _productService.GetFeaturedProductsWithDetailsAsync(1000); // Get more for pagination
            var productsList = allProducts.ToList();
            var totalCount = productsList.Count;

            var pagedProducts = productsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<ProductDto>(pagedProducts, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("category/{categoryId}")]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetProductsByCategory(
            int categoryId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allProducts = await _productService.GetProductsByCategoryWithDetailsAsync(categoryId);
            var productsList = allProducts.ToList();
            var totalCount = productsList.Count;

            var pagedProducts = productsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<ProductDto>(pagedProducts, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("subcategory/{subCategoryId}")]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetProductsBySubCategory(
            int subCategoryId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allProducts = await _productService.GetProductsBySubCategoryWithDetailsAsync(subCategoryId);
            var productsList = allProducts.ToList();
            var totalCount = productsList.Count;

            var pagedProducts = productsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<ProductDto>(pagedProducts, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}/related")]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetRelatedProducts(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 5)
        {
            var allProducts = await _productService.GetRelatedProductsWithDetailsAsync(id, 1000); // Get more for pagination
            var productsList = allProducts.ToList();
            var totalCount = productsList.Count;

            var pagedProducts = productsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<ProductDto>(pagedProducts, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ProductDto>> CreateProduct(
            [FromForm] ProductCreateWithFilesDto productDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var product = await _productService.CreateProductWithFilesAsync(productDto);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ProductDto>> UpdateProduct(
            int id,
            [FromForm] ProductUpdateWithFilesDto productDto)
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                productDto.Id = id;
                var product = await _productService.UpdateProductWithFilesAsync(productDto);
                return Ok(product);
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
        public async Task<ActionResult> DeleteProduct(int id)
        {
            try
            {
                var result = await _productService.DeleteProductAsync(id);
                if (!result)
                {
                    return NotFound($"Product with ID {id} not found.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}/ratings")]
        public async Task<ActionResult<PagedResult<RatingDto>>> GetProductRatings(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allRatings = await _productService.GetProductRatingsAsync(id);
            var ratingsList = allRatings.ToList();
            var totalCount = ratingsList.Count;

            var pagedRatings = ratingsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<RatingDto>(pagedRatings, totalCount, pageNumber, pageSize);
            return Ok(result);
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

        // Stock management endpoints for admin
        [HttpGet("in-stock")]
        public async Task<ActionResult<PagedResult<ProductSummaryDto>>> GetInStockProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allProducts = await _productService.GetInStockProductsAsync();
            var productsList = allProducts.ToList();
            var totalCount = productsList.Count;

            var pagedProducts = productsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<ProductSummaryDto>(pagedProducts, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("out-of-stock")]
        public async Task<ActionResult<PagedResult<ProductSummaryDto>>> GetOutOfStockProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allProducts = await _productService.GetOutOfStockProductsAsync();
            var productsList = allProducts.ToList();
            var totalCount = productsList.Count;

            var pagedProducts = productsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<ProductSummaryDto>(pagedProducts, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("low-stock")]
        public async Task<ActionResult<PagedResult<ProductSummaryDto>>> GetLowStockProducts(
            [FromQuery] int threshold = 10,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allProducts = await _productService.GetLowStockProductsAsync(threshold);
            var productsList = allProducts.ToList();
            var totalCount = productsList.Count;

            var pagedProducts = productsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<ProductSummaryDto>(pagedProducts, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpPut("{id}/stock")]
        public async Task<ActionResult<object>> UpdateProductStock(int id, [FromBody] UpdateStockRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _productService.UpdateProductStockAsync(id, request.NewStockQuantity);
                if (!success)
                {
                    return NotFound($"Product with ID {id} not found.");
                }

                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found.");
                }

                return Ok(new
                {
                    ProductId = id,
                    IsInStock = product.IsInStock,
                    TotalInStock = product.TotalInStock,
                    Message = "Stock updated successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}/stock/increase")]
        public async Task<ActionResult<object>> IncreaseProductStock(int id, [FromBody] StockQuantityRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _productService.IncreaseProductStockAsync(id, request.Quantity);
                if (!success)
                {
                    return NotFound($"Product with ID {id} not found.");
                }

                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found.");
                }

                return Ok(new
                {
                    ProductId = id,
                    IsInStock = product.IsInStock,
                    TotalInStock = product.TotalInStock,
                    Message = $"Stock increased by {request.Quantity}"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}/stock/reduce")]
        public async Task<ActionResult<object>> ReduceProductStock(int id, [FromBody] StockQuantityRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _productService.ReduceProductStockAsync(id, request.Quantity);
                if (!success)
                {
                    return BadRequest($"Product with ID {id} not found or insufficient stock.");
                }

                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found.");
                }

                return Ok(new
                {
                    ProductId = id,
                    IsInStock = product.IsInStock,
                    TotalInStock = product.TotalInStock,
                    Message = $"Stock reduced by {request.Quantity}"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}/stock/status")]
        public async Task<ActionResult<object>> SetProductStockStatus(int id, [FromBody] StockStatusRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _productService.SetProductInStockStatusAsync(id, request.IsInStock);
                if (!success)
                {
                    return NotFound($"Product with ID {id} not found.");
                }

                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found.");
                }

                return Ok(new
                {
                    ProductId = id,
                    IsInStock = product.IsInStock,
                    TotalInStock = product.TotalInStock,
                    Message = $"Stock status updated to {(request.IsInStock ? "In Stock" : "Out of Stock")}"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    // Request DTOs for stock management
    public class UpdateStockRequest
    {
        public int NewStockQuantity { get; set; }
    }

    public class StockQuantityRequest
    {
        public int Quantity { get; set; }
    }

    public class StockStatusRequest
    {
        public bool IsInStock { get; set; }
    }
}
