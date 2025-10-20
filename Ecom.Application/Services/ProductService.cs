using AutoMapper;
using Ecom.Application.DTOs.Product;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.UnitOfWork;

namespace Ecom.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IFileService _fileService;

        public ProductService(IUnitOfWork unitOfWork, IMapper mapper, IFileService fileService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _fileService = fileService;
        }

        public async Task<ProductDto?> GetProductByIdAsync(int id)
        {
            var product = await _unitOfWork.Products.GetProductWithDetailsAsync(id);
            return product != null ? _mapper.Map<ProductDto>(product) : null;
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetAllProductsAsync()
        {
            var products = await _unitOfWork.Products.GetAllAsync();
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
            int pageNumber = 1,
            int pageSize = 20)
        {
            var (products, totalCount) = await _unitOfWork.Products.GetProductsWithFiltersAsync(
                categoryId, subCategoryId, minPrice, maxPrice, searchTerm, pageNumber, pageSize);

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

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            var createdProduct = await _unitOfWork.Products.GetProductWithDetailsAsync(product.Id);
            return _mapper.Map<ProductDto>(createdProduct);
        }

        public async Task<ProductDto> UpdateProductAsync(ProductUpdateDto productDto)
        {
            var existingProduct = await _unitOfWork.Products.GetByIdAsync(productDto.Id);
            if (existingProduct == null)
                throw new ArgumentException($"Product with ID {productDto.Id} not found.");

            _mapper.Map(productDto, existingProduct);
            _unitOfWork.Products.Update(existingProduct);
            await _unitOfWork.SaveChangesAsync();

            var updatedProduct = await _unitOfWork.Products.GetProductWithDetailsAsync(productDto.Id);
            return _mapper.Map<ProductDto>(updatedProduct);
        }

        public async Task<ProductDto> UpdateProductWithFilesAsync(ProductUpdateWithFilesDto productDto)
        {
            var existingProduct = await _unitOfWork.Products.GetProductWithDetailsAsync(productDto.Id);
            if (existingProduct == null)
                throw new ArgumentException($"Product with ID {productDto.Id} not found.");

            // Store old image URLs for deletion
            var oldImageUrls = existingProduct.Images?.ToList() ?? new List<string>();

            // Map basic properties
            existingProduct.Title = productDto.Title;
            existingProduct.Description = productDto.Description;
            existingProduct.oldPrice = productDto.OldPrice;
            existingProduct.newPrice = productDto.NewPrice;
            existingProduct.SubCategoryId = productDto.SubCategoryId;
            existingProduct.TotalInStock = productDto.TotalInStock;
            existingProduct.IsInStock = productDto.IsInStock;
            existingProduct.Title = productDto.Title;
            
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

            return _mapper.Map<RatingDto>(rating);
        }

        public async Task<IEnumerable<RatingDto>> GetProductRatingsAsync(int productId)
        {
            var ratings = await _unitOfWork.Ratings.FindAsync(r => r.ProductId == productId);
            return _mapper.Map<IEnumerable<RatingDto>>(ratings);
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
            int pageNumber = 1,
            int pageSize = 20)
        {
            var (products, totalCount) = await _unitOfWork.Products.GetProductsWithFiltersAsync(
                categoryId, subCategoryId, minPrice, maxPrice, searchTerm, pageNumber, pageSize);

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
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return false;

            product.TotalInStock = newStockQuantity;
            product.IsInStock = newStockQuantity > 0;

            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReduceProductStockAsync(int productId, int quantity)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null || product.TotalInStock < quantity)
                return false;

            product.TotalInStock -= quantity;
            product.IsInStock = product.TotalInStock > 0;

            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IncreaseProductStockAsync(int productId, int quantity)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return false;

            product.TotalInStock += quantity;
            product.IsInStock = true; // If we're adding stock, it's definitely in stock

            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetProductInStockStatusAsync(int productId, bool isInStock)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return false;

            product.IsInStock = isInStock;

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
    }
}





