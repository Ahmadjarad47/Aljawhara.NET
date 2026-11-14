using AutoMapper;
using Ecom.Application.DTOs.Product;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecom.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly CatalogImageService _images;
        private readonly ILogger<ProductService>? _logger;
        private readonly UserManager<AppUsers> _userManager;

        public ProductService(IUnitOfWork unitOfWork, IMapper mapper, CatalogImageService images, UserManager<AppUsers> userManager,
            ILogger<ProductService>? logger = null)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _images = images;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<ProductDto?> GetProductByIdAsync(int id)
        {
            var product = await _unitOfWork.Products.GetActiveByIdAsync(id);
            if (product == null) return null;
            
            var productWithDetails = await _unitOfWork.Products.GetProductWithDetailsAsync(id);
            return productWithDetails != null ? _mapper.Map<ProductDto>(productWithDetails) : null;
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetAllProductsAsync()
        {
            var products = await _unitOfWork.Products.GetAllActiveAsync();
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetProductsByCategoryAsync(int categoryId)
        {
            var products = await _unitOfWork.Products.GetProductsByCategoryAsync(categoryId);
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetProductsBySubCategoryAsync(int subCategoryId)
        {
            var products = await _unitOfWork.Products.GetProductsBySubCategoryAsync(subCategoryId);
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetFeaturedProductsAsync(int count = 10)
        {
            var products = await _unitOfWork.Products.GetFeaturedProductsAsync(count);
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<IEnumerable<ProductSummaryDto>> SearchProductsAsync(string searchTerm)
        {
            var products = await _unitOfWork.Products.SearchProductsAsync(searchTerm, includeDetails: false);
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<(IEnumerable<ProductSummaryDto> Products, int TotalCount)> GetProductsWithFiltersAsync(
            int? categoryId = null,
            int? subCategoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? searchTerm = null,
            bool? isActive = null,
            string? sortBy = null,
            bool? inStock = null,
            bool? onSale = null,
            bool? newArrival = null,
            bool? bestDiscount = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var (products, totalCount) = await _unitOfWork.Products.GetProductsWithFiltersAsync(
                categoryId: categoryId,
                subCategoryId: subCategoryId,
                minPrice: minPrice,
                maxPrice: maxPrice,
                searchTerm: searchTerm,
                isActive: isActive,
                sortBy: sortBy,
                inStock: inStock,
                onSale: onSale,
                newArrival: newArrival,
                bestDiscount: bestDiscount,
                pageNumber: pageNumber,
                pageSize: pageSize,
                includeDetails: false);

            var productDtos = _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
            return (productDtos, totalCount);
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetRelatedProductsAsync(int productId, int count = 5)
        {
            var products = await _unitOfWork.Products.GetRelatedProductsAsync(productId, count);
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<ProductDto> CreateProductAsync(ProductCreateDto productDto)
        {
            var product = _mapper.Map<Product>(productDto);

            // Handle Variants
            if (productDto.Variants != null && productDto.Variants.Count > 0)
            {
                foreach (var variantDto in productDto.Variants)
                {
                    var variant = _mapper.Map<ProductVariant>(variantDto);
                    // Reset values to avoid double-mapping from AutoMapper
                    variant.Values = new List<ProductVariantValue>();
                    variant.ProductId = product.Id;
                    
                    if (variantDto.Values != null && variantDto.Values.Count > 0)
                    {
                        foreach (var valueDto in variantDto.Values)
                        {
                            var value = _mapper.Map<ProductVariantValue>(valueDto);
                            value.ProductVariantId = variant.Id;
                            variant.Values.Add(value);
                        }
                    }
                    
                    product.Variants.Add(variant);
                }
            }

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            var createdProduct = await _unitOfWork.Products.GetProductWithDetailsAsync(product.Id);
            return _mapper.Map<ProductDto>(createdProduct);
        }

        public async Task<ProductDto> CreateProductWithFilesAsync(ProductCreateWithFilesDto productDto)
        {
            var product = _mapper.Map<Product>(productDto);

            if (productDto.Images is { Count: > 0 })
            {
                product.ImageOwnerId = Guid.NewGuid();
                product.Images = await _images.UploadAsync(productDto.Images.ToArray(), CatalogImageArea.Products, product.ImageOwnerId.Value);
            }

            // Handle Variants
            if (productDto.Variants != null && productDto.Variants.Count > 0)
            {
                foreach (var variantDto in productDto.Variants)
                {
                    var variant = _mapper.Map<ProductVariant>(variantDto);
                    // Reset values to avoid double-mapping from AutoMapper
                    variant.Values = new List<ProductVariantValue>();
                    variant.ProductId = product.Id;
                    
                    if (variantDto.Values != null && variantDto.Values.Count > 0)
                    {
                        foreach (var valueDto in variantDto.Values)
                        {
                            var value = _mapper.Map<ProductVariantValue>(valueDto);
                            value.ProductVariantId = variant.Id;
                            variant.Values.Add(value);
                        }
                    }
                    
                    product.Variants.Add(variant);
                }
            }

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            var createdProduct = await _unitOfWork.Products.GetProductWithDetailsAsync(product.Id);
            return _mapper.Map<ProductDto>(createdProduct);
        }

        public async Task<ProductDto> UpdateProductAsync(ProductUpdateDto productDto)
        {
            var existingProduct = await _unitOfWork.Products.GetProductWithDetailsForUpdateAsync(productDto.Id);
            if (existingProduct == null)
                throw new ArgumentException($"Product with ID {productDto.Id} not found.");

            // Map basic properties
            existingProduct.Title = productDto.Title;
            existingProduct.TitleAr = productDto.TitleAr;
            existingProduct.Description = productDto.Description;
            existingProduct.DescriptionAr = productDto.DescriptionAr;
            existingProduct.oldPrice = productDto.OldPrice;
            existingProduct.newPrice = productDto.NewPrice;
            existingProduct.IsInStock = productDto.IsInStock;
            existingProduct.TotalInStock = productDto.TotalInStock;
            existingProduct.SubCategoryId = productDto.SubCategoryId;

            // Handle ProductDetails update - clear existing and add new ones
            existingProduct.productDetails.Clear();
            if (productDto.ProductDetails != null && productDto.ProductDetails.Count > 0)
            {
                var newProductDetails = _mapper.Map<List<ProductDetails>>(productDto.ProductDetails);
                foreach (var detail in newProductDetails)
                {
                    detail.ProductId = existingProduct.Id;
                    existingProduct.productDetails.Add(detail);
                }
            }

            // Handle Variants update
            existingProduct.Variants.Clear();
            if (productDto.Variants != null && productDto.Variants.Count > 0)
            {
                foreach (var variantDto in productDto.Variants)
                {
                    var variant = _mapper.Map<ProductVariant>(variantDto);
                    // Reset values to avoid double-mapping from AutoMapper
                    variant.Values = new List<ProductVariantValue>();
                    variant.ProductId = existingProduct.Id;
                    
                    if (variantDto.Values != null && variantDto.Values.Count > 0)
                    {
                        foreach (var valueDto in variantDto.Values)
                        {
                            var value = _mapper.Map<ProductVariantValue>(valueDto);
                            value.ProductVariantId = variant.Id;
                            variant.Values.Add(value);
                        }
                    }
                    
                    existingProduct.Variants.Add(variant);
                }
            }

            _unitOfWork.Products.Update(existingProduct);
            await _unitOfWork.SaveChangesAsync();

            var updatedProduct = await _unitOfWork.Products.GetProductWithDetailsAsync(productDto.Id);
            return _mapper.Map<ProductDto>(updatedProduct);
        }

        public async Task<ProductDto> UpdateProductWithFilesAsync(ProductUpdateWithFilesDto productDto)
        {
            var existingProduct = await _unitOfWork.Products.GetProductWithDetailsForUpdateAsync(productDto.Id);
            if (existingProduct == null)
                throw new ArgumentException($"Product with ID {productDto.Id} not found.");

            var oldImageUrls = existingProduct.Images;
            var removals = (productDto.ImagesToDelete ?? []).ToHashSet(StringComparer.Ordinal);
            if (removals.Any(url => !oldImageUrls.Contains(url, StringComparer.Ordinal)))
                throw new CatalogImageException("An image selected for removal does not belong to this product.");

            var newUrls = Array.Empty<string>();
            if (productDto.Images is { Count: > 0 })
            {
                existingProduct.ImageOwnerId ??= Guid.NewGuid();
                newUrls = await _images.UploadAsync(productDto.Images.ToArray(), CatalogImageArea.Products,
                    existingProduct.ImageOwnerId.Value);
            }
            existingProduct.Images = oldImageUrls.Where(url => !removals.Contains(url)).Concat(newUrls).ToArray();

            // Map basic properties, including localized fields
            existingProduct.Title = productDto.Title;
            existingProduct.TitleAr = productDto.TitleAr;
            existingProduct.Description = productDto.Description;
            existingProduct.DescriptionAr = productDto.DescriptionAr;
            existingProduct.oldPrice = productDto.OldPrice;
            existingProduct.newPrice = productDto.NewPrice;
            existingProduct.SubCategoryId = productDto.SubCategoryId;
            existingProduct.TotalInStock = productDto.TotalInStock;
            existingProduct.IsInStock = productDto.IsInStock;
            
            // Handle ProductDetails update - clear existing and add new ones
            existingProduct.productDetails.Clear();
            if (productDto.ProductDetails != null && productDto.ProductDetails.Count > 0)
            {
                var newProductDetails = _mapper.Map<List<ProductDetails>>(productDto.ProductDetails);
                foreach (var detail in newProductDetails)
                {
                    detail.ProductId = existingProduct.Id;
                    existingProduct.productDetails.Add(detail);
                }
            }

            // Handle Variants update
            existingProduct.Variants.Clear();
            if (productDto.Variants != null && productDto.Variants.Count > 0)
            {
                foreach (var variantDto in productDto.Variants)
                {
                    var variant = _mapper.Map<ProductVariant>(variantDto);
                    // Reset values to avoid double-mapping from AutoMapper
                    variant.Values = new List<ProductVariantValue>();
                    variant.ProductId = existingProduct.Id;
                    
                    if (variantDto.Values != null && variantDto.Values.Count > 0)
                    {
                        foreach (var valueDto in variantDto.Values)
                        {
                            var value = _mapper.Map<ProductVariantValue>(valueDto);
                            value.ProductVariantId = variant.Id;
                            variant.Values.Add(value);
                        }
                    }
                    
                    existingProduct.Variants.Add(variant);
                }
            }

            _unitOfWork.Products.Update(existingProduct);
            await _unitOfWork.SaveChangesAsync();

            if (existingProduct.ImageOwnerId is Guid owner)
                await DeleteUnreferencedProductImagesAsync(owner, oldImageUrls.Where(removals.Contains).Distinct());

            var updatedProduct = await _unitOfWork.Products.GetProductWithDetailsAsync(productDto.Id);
            return _mapper.Map<ProductDto>(updatedProduct);
        }

        private async Task DeleteUnreferencedProductImagesAsync(Guid owner, IEnumerable<string> removedUrls)
        {
            var db = (EcomDbContext)_unitOfWork.Context;
            foreach (var url in removedUrls)
            {
                var key = _images.OwnedProductKey(owner, url);
                if (key is null)
                    continue; // Preserve objects with unknown provenance, including all legacy URLs.

                try
                {
                    if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.Images.Contains(url)) ||
                        await db.Categories.IgnoreQueryFilters().AnyAsync(c => c.Image == url) ||
                        await db.Carousels.IgnoreQueryFilters().AnyAsync(c => c.Image == url) ||
                        await db.OrderItems.IgnoreQueryFilters().AnyAsync(i => i.Image == url))
                        continue;

                    await _images.DeleteProductObjectAsync(key);
                }
                catch (Exception ex)
                {
                    // The association was already removed; retain the object on any uncertainty.
                    _logger?.LogWarning("Catalog object cleanup deferred. FailureType={FailureType}", ex.GetType().Name);
                }
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(id);
            if (product == null)
                return false;

            _unitOfWork.Products.SoftDelete(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<RatingDto?> AddCustomerProductRatingAsync(string userId, int orderId, RatingCreateDto ratingDto)
        {
            // Storefront rule: one review per account and product, after delivery of
            // an order owned by that account with that product as a live line item.
            var eligible = await _unitOfWork.Orders.ExistsAsync(order =>
                order.Id == orderId && order.AppUserId == userId && order.Status == OrderStatus.Delivered &&
                order.Items.Any(item => item.ProductId == ratingDto.ProductId && !item.IsDeleted && item.Product.IsActive && !item.Product.IsDeleted));

            return eligible ? await AddProductRatingAsync(userId, ratingDto) : null;
        }

        public async Task<RatingDto> AddProductRatingAsync(string userId, RatingCreateDto ratingDto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || !user.IsActive || user.LockoutEnd > DateTimeOffset.UtcNow)
                throw new InvalidOperationException("An active reviewer account is required.");

            if (!await _unitOfWork.Products.ExistsAsync(p => p.Id == ratingDto.ProductId && p.IsActive))
                throw new ArgumentException("Product not found.");

            var db = (EcomDbContext)_unitOfWork.Context;
            // Preserve legacy reviews and count their existing author attribution for eligibility.
            if (await db.Ratings.IgnoreQueryFilters().AnyAsync(r => r.ProductId == ratingDto.ProductId &&
                (r.ReviewerUserId == userId || (r.ReviewerUserId == null && r.CreatedBy == userId))))
                throw new DuplicateRatingException();

            var rating = new Rating
            {
                ProductId = ratingDto.ProductId,
                Content = ratingDto.Content,
                RatingNumber = ratingDto.RatingNumber,
                ReviewerUserId = user.Id
            };
            await _unitOfWork.Ratings.AddAsync(rating);
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_Ratings_Product_Reviewer" })
            {
                db.Entry(rating).State = EntityState.Detached;
                throw new DuplicateRatingException();
            }

            var result = _mapper.Map<RatingDto>(rating);
            result.RatingName = user.UserName ?? string.Empty;
            return result;
        }

        public async Task<RatingDto?> GetOwnProductRatingAsync(int productId, string userId)
        {
            var db = (EcomDbContext)_unitOfWork.Context;
            var rating = await db.Ratings.AsNoTracking().Where(r => r.ProductId == productId &&
                    (r.ReviewerUserId == userId || (r.ReviewerUserId == null && r.CreatedBy == userId)))
                .OrderByDescending(r => r.ReviewerUserId != null).ThenByDescending(r => r.Id).FirstOrDefaultAsync();
            if (rating is null)
                return null;

            var result = _mapper.Map<RatingDto>(rating);
            result.RatingName = (await _userManager.FindByIdAsync(userId))?.UserName ?? string.Empty;
            return result;
        }

        public async Task<IEnumerable<RatingDto>> GetProductRatingsAsync(int productId)
        {
            var ratings = await _unitOfWork.Ratings.FindAsync(r => r.ProductId == productId);
            var ratingsList = ratings.ToList();
            
            // Manually map to ensure Product is loaded for ProductTitle
            var ratingDtos = new List<RatingDto>();
            foreach (var rating in ratingsList)
            {
                var ratingDto = _mapper.Map<RatingDto>(rating);
                ratingDtos.Add(ratingDto);
            }
            
            // Get unique user IDs from ratings
            var userIds = ratingsList
                .Select(r => r.ReviewerUserId ?? r.CreatedBy)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
            
            // Fetch all users in a single query to avoid DbContext concurrency issues
            var usersDict = new Dictionary<string, string>();
            if (userIds.Any())
            {
                var userList = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.UserName })
                    .ToListAsync();
                
                foreach (var user in userList)
                {
                    usersDict[user.Id] = user.UserName ?? string.Empty;
                }
            }
            
            // Populate RatingName for each rating
            for (var i = 0; i < ratingDtos.Count; i++)
            {
                var author = ratingsList[i].ReviewerUserId ?? ratingsList[i].CreatedBy;
                if (!string.IsNullOrEmpty(author) && usersDict.TryGetValue(author, out var userName))
                {
                    ratingDtos[i].RatingName = userName;
                }
            }
            
            return ratingDtos;
        }

        // New methods that return ProductDto with full details
        public async Task<IEnumerable<ProductDto>> GetAllProductsWithDetailsAsync()
        {
            var products = await _unitOfWork.Products.GetAllProductsWithDetailsAsync();
            return _mapper.Map<IEnumerable<ProductDto>>(products);
        }

        public async Task<IEnumerable<ProductDto>> GetProductsByCategoryWithDetailsAsync(int categoryId)
        {
            var products = await _unitOfWork.Products.GetProductsByCategoryAsync(categoryId);
            return _mapper.Map<IEnumerable<ProductDto>>(products);
        }

        public async Task<IEnumerable<ProductDto>> GetProductsBySubCategoryWithDetailsAsync(int subCategoryId)
        {
            var products = await _unitOfWork.Products.GetProductsBySubCategoryAsync(subCategoryId);
            return _mapper.Map<IEnumerable<ProductDto>>(products);
        }

        public async Task<IEnumerable<ProductDto>> GetFeaturedProductsWithDetailsAsync(int count = 10)
        {
            var products = await _unitOfWork.Products.GetFeaturedProductsAsync(count);
            return _mapper.Map<IEnumerable<ProductDto>>(products);
        }

        public async Task<IEnumerable<ProductDto>> SearchProductsWithDetailsAsync(string searchTerm)
        {
            var products = await _unitOfWork.Products.SearchProductsAsync(searchTerm);
            return _mapper.Map<IEnumerable<ProductDto>>(products);
        }

        public async Task<(IEnumerable<ProductDto> Products, int TotalCount)> GetProductsWithFiltersAndDetailsAsync(
            int? categoryId = null,
            int? subCategoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? searchTerm = null,
            bool? isActive = null,
            string? sortBy = null,
            bool? inStock = null,
            bool? onSale = null,
            bool? newArrival = null,
            bool? bestDiscount = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var (products, totalCount) = await _unitOfWork.Products.GetProductsWithFiltersAsync(
                categoryId: categoryId,
                subCategoryId: subCategoryId,
                minPrice: minPrice,
                maxPrice: maxPrice,
                searchTerm: searchTerm,
                isActive: isActive,
                sortBy: sortBy,
                inStock: inStock,
                onSale: onSale,
                newArrival: newArrival,
                bestDiscount: bestDiscount,
                pageNumber: pageNumber,
                pageSize: pageSize);

            var productDtos = _mapper.Map<IEnumerable<ProductDto>>(products);
            return (productDtos, totalCount);
        }

        public async Task<IEnumerable<ProductDto>> GetRelatedProductsWithDetailsAsync(int productId, int count = 5)
        {
            var products = await _unitOfWork.Products.GetRelatedProductsAsync(productId, count);
            return _mapper.Map<IEnumerable<ProductDto>>(products);
        }

        // Stock management methods
        public async Task<bool> UpdateProductStockAsync(int productId, int newStockQuantity)
        {
            if (newStockQuantity < 0)
                return false;

            // Use FirstOrDefaultAsync to get a tracked entity for updates
            var product = await _unitOfWork.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
                return false;

            product.TotalInStock = newStockQuantity;
            product.IsInStock = newStockQuantity > 0;

            // Entity is already tracked, just mark as modified
            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReduceProductStockAsync(int productId, int quantity)
        {
            if (quantity <= 0)
                return false;

            return await _unitOfWork.Products.TryReduceStockAsync(productId, quantity);
        }

        public async Task<bool> IncreaseProductStockAsync(int productId, int quantity)
        {
            if (quantity <= 0)
                return false;

            return await _unitOfWork.Products.TryIncreaseStockAsync(productId, quantity);
        }

        public async Task<bool> SetProductInStockStatusAsync(int productId, bool isInStock)
        {
            // Use FirstOrDefaultAsync to get a tracked entity for updates
            var product = await _unitOfWork.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
                return false;

            product.IsInStock = isInStock;

            // Entity is already tracked, just mark as modified
            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetInStockProductsAsync()
        {
            var products = await _unitOfWork.Products.FindAsync(p => p.IsInStock && p.TotalInStock > 0);
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetOutOfStockProductsAsync()
        {
            var products = await _unitOfWork.Products.FindAsync(p => !p.IsInStock || p.TotalInStock <= 0);
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetLowStockProductsAsync(int threshold = 10)
        {
            var products = await _unitOfWork.Products.FindAsync(p => p.IsInStock && p.TotalInStock > 0 && p.TotalInStock <= threshold);
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }

        public async Task<bool> ActivateProductAsync(int productId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return false;

            _unitOfWork.Products.Activate(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateProductAsync(int productId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return false;

            _unitOfWork.Products.Deactivate(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetAllProductsIncludingInactiveAsync()
        {
            var products = await _unitOfWork.Products.GetAllAsync();
            return _mapper.Map<IEnumerable<ProductSummaryDto>>(products);
        }
    }
}





