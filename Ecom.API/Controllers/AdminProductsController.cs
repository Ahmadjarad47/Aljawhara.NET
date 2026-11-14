using Ecom.Application.DTOs.Common;
using Ecom.Application.DTOs.Product;
using Ecom.Application.Services;
using System.Security.Claims;
using Ecom.Application.Services.Interfaces;
using Ecom.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/products")]
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
            [FromQuery] bool? isActive = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var (products, totalCount) = await _productService.GetProductsWithFiltersAndDetailsAsync(
                categoryId: categoryId,
                subCategoryId: subCategoryId,
                minPrice: minPrice,
                maxPrice: maxPrice,
                searchTerm: searchTerm,
                isActive: isActive,
                sortBy: null,
                inStock: null,
                onSale: null,
                newArrival: null,
                bestDiscount: null,
                pageNumber: pageNumber,
                pageSize: pageSize);

            var result = new PagedResult<ProductDto>(products.ToList(), totalCount, pageNumber, pageSize);
            return Ok(result);
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
        [RequestSizeLimit(CatalogImageOptions.RequestBytes)]
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
            catch (CatalogImageException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        [RequestSizeLimit(CatalogImageOptions.RequestBytes)]
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
            catch (CatalogImageException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("The product changed while it was being edited. Reload it and try again.");
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

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            try
            {
                var rating = await _productService.AddProductRatingAsync(userId, ratingDto);
                return CreatedAtAction(nameof(GetProductRatings), new { id = id }, rating);
            }
            catch (DuplicateRatingException ex)
            {
                return Conflict(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
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

        [HttpPut("{id}/toggle-active")]
        public async Task<ActionResult<object>> ToggleProductActive(int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found.");
                }

                bool newStatus;
                if (product.IsActive)
                {
                    var result = await _productService.DeactivateProductAsync(id);
                    newStatus = false;
                }
                else
                {
                    var result = await _productService.ActivateProductAsync(id);
                    newStatus = true;
                }

                return Ok(new
                {
                    ProductId = id,
                    IsActive = newStatus,
                    Message = $"Product {(newStatus ? "activated" : "deactivated")} successfully"
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
