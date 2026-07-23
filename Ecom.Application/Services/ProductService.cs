using AutoMapper;
using Ecom.Application.DTOs.Product;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IFileService _fileService;
        private readonly UserManager<AppUsers> _userManager;

        public ProductService(IUnitOfWork unitOfWork, IMapper mapper, IFileService fileService, UserManager<AppUsers> userManager)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _fileService = fileService;
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
            var products = await _unitOfWork.Products.SearchProductsAsync(searchTerm);
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
                pageSize: pageSize);

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

            // Handle file uploads if images are provided
            if (productDto.Images != null && productDto.Images.Count > 0)
            {
                var imageUrls = new List<string>();
                var directory = $"products/{DateTime.UtcNow:yyyy/MM/dd}";

                foreach (var image in productDto.Images)
                {
                    try
                    {
                        var imageUrl = await _fileService.SaveFileAsync(image, directory);
                        imageUrls.Add(imageUrl);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other images
                        Console.WriteLine($"Error uploading image {image.FileName}: {ex.Message}");
                    }
                }

                product.Images = imageUrls.ToArray();
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

            // Store old image URLs for deletion
            var oldImageUrls = existingProduct.Images?.ToList() ?? new List<string>();

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

            // Handle image deletions first
            if (productDto.ImagesToDelete != null && productDto.ImagesToDelete.Count > 0)
            {
                var remainingImages = oldImageUrls.Where(url => !productDto.ImagesToDelete.Contains(url)).ToList();
                existingProduct.Images = remainingImages.ToArray();

                // Delete specified images
                foreach (var imageUrlToDelete in productDto.ImagesToDelete)
                {
                    try
                    {
                        await _fileService.DeleteFileAsync(imageUrlToDelete);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other deletions
                        Console.WriteLine($"Error deleting image {imageUrlToDelete}: {ex.Message}");
                    }
                }
            }

            // Handle new file uploads if images are provided
            if (productDto.Images != null && productDto.Images.Count > 0)
            {
                var imageUrls = new List<string>();
                var directory = $"products/{DateTime.UtcNow:yyyy/MM/dd}";

                foreach (var image in productDto.Images)
                {
                    try
                    {
                        var imageUrl = await _fileService.SaveFileAsync(image, directory);
                        imageUrls.Add(imageUrl);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other images
                        Console.WriteLine($"Error uploading image {image.FileName}: {ex.Message}");
                    }
                }

                // Add new images to existing ones (if no deletions were specified)
                if (productDto.ImagesToDelete == null || productDto.ImagesToDelete.Count == 0)
                {
                    var allImages = oldImageUrls.Concat(imageUrls).ToArray();
                    existingProduct.Images = allImages;
                }
                else
                {
                    // If deletions were specified, add new images to remaining ones
                    var remainingImages = oldImageUrls.Where(url => !productDto.ImagesToDelete.Contains(url)).ToList();
                    remainingImages.AddRange(imageUrls);
                    existingProduct.Images = remainingImages.ToArray();
                }
            }

            _unitOfWork.Products.Update(existingProduct);
            await _unitOfWork.SaveChangesAsync();

            var updatedProduct = await _unitOfWork.Products.GetProductWithDetailsAsync(productDto.Id);
            return _mapper.Map<ProductDto>(updatedProduct);
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

        public async Task<RatingDto> AddProductRatingAsync(RatingCreateDto ratingDto)
        {
            var rating = _mapper.Map<Rating>(ratingDto);

            await _unitOfWork.Ratings.AddAsync(rating);
            await _unitOfWork.SaveChangesAsync();

            var result = _mapper.Map<RatingDto>(rating);
            
            // Populate RatingName from user
            if (!string.IsNullOrEmpty(rating.CreatedBy))
            {
                var user = await _userManager.FindByIdAsync(rating.CreatedBy);
                if (user != null)
                {
                    result.RatingName = user.UserName ?? string.Empty;
                }
            }
            
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
            var userIds = ratingDtos
                .Where(r => !string.IsNullOrEmpty(r.CreatedBy))
                .Select(r => r.CreatedBy)
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
            foreach (var ratingDto in ratingDtos)
            {
                if (!string.IsNullOrEmpty(ratingDto.CreatedBy) && usersDict.TryGetValue(ratingDto.CreatedBy, out var userName))
                {
                    ratingDto.RatingName = userName;
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
            // Use FirstOrDefaultAsync to get a tracked entity for updates
            var product = await _unitOfWork.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null || product.TotalInStock < quantity)
                return false;

            product.TotalInStock -= quantity;
            product.IsInStock = product.TotalInStock > 0;

            // Entity is already tracked, just mark as modified
            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IncreaseProductStockAsync(int productId, int quantity)
        {
            // Use FirstOrDefaultAsync to get a tracked entity for updates
            var product = await _unitOfWork.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
                return false;

            product.TotalInStock += quantity;
            product.IsInStock = true; // If we're adding stock, it's definitely in stock

            // Entity is already tracked, just mark as modified
            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
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





