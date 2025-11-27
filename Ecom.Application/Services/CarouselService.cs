using AutoMapper;
using Ecom.Application.DTOs.Carousel;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Application.Services
{
    public class CarouselService : ICarouselService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IFileService _fileService;

        public CarouselService(IUnitOfWork unitOfWork, IMapper mapper, IFileService fileService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _fileService = fileService;
        }

        public async Task<CarouselDto?> GetCarouselByIdAsync(int id)
        {
            var carousel = await _unitOfWork.Carousels.GetActiveByIdAsync(id);
            return carousel != null ? _mapper.Map<CarouselDto>(carousel) : null;
        }

        public async Task<IEnumerable<CarouselDto>> GetAllCarouselsAsync()
        {
            var carousels = await _unitOfWork.Carousels.GetAllAsync();
            return _mapper.Map<IEnumerable<CarouselDto>>(carousels);
        }

        public async Task<IEnumerable<CarouselDto>> GetActiveCarouselsAsync()
        {
            var carousels = await _unitOfWork.Carousels.GetAllActiveAsync();
            return _mapper.Map<IEnumerable<CarouselDto>>(carousels);
        }

        public async Task<CarouselDto> CreateCarouselAsync(CarouselCreateDto carouselDto)
        {
            var carousel = _mapper.Map<Carousel>(carouselDto);
            
            await _unitOfWork.Carousels.AddAsync(carousel);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CarouselDto>(carousel);
        }

        public async Task<CarouselDto> CreateCarouselWithFileAsync(CarouselCreateWithFileDto carouselDto)
        {
            var carousel = _mapper.Map<Carousel>(carouselDto);

            // Handle file upload if image is provided
            if (carouselDto.Image != null)
            {
                try
                {
                    var directory = $"carousels/{DateTime.UtcNow:yyyy/MM/dd}";
                    var imageUrl = await _fileService.SaveFileAsync(carouselDto.Image, directory);
                    carousel.Image = imageUrl;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error uploading image: {ex.Message}", ex);
                }
            }

            await _unitOfWork.Carousels.AddAsync(carousel);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CarouselDto>(carousel);
        }

        public async Task<CarouselDto> UpdateCarouselAsync(CarouselUpdateDto carouselDto)
        {
            var existingCarousel = await _unitOfWork.Carousels.GetByIdAsync(carouselDto.Id);
            if (existingCarousel == null)
                throw new ArgumentException($"Carousel with ID {carouselDto.Id} not found.");

            _mapper.Map(carouselDto, existingCarousel);
            _unitOfWork.Carousels.Update(existingCarousel);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CarouselDto>(existingCarousel);
        }

        public async Task<CarouselDto> UpdateCarouselWithFileAsync(CarouselUpdateWithFileDto carouselDto)
        {
            var existingCarousel = await _unitOfWork.Carousels.GetByIdAsync(carouselDto.Id);
            if (existingCarousel == null)
                throw new ArgumentException($"Carousel with ID {carouselDto.Id} not found.");

            // Store old image URL for deletion
            var oldImageUrl = existingCarousel.Image;

            // Map basic properties
            existingCarousel.Title = carouselDto.Title;
            existingCarousel.TitleAr = carouselDto.TitleAr;
            existingCarousel.Description = carouselDto.Description;
            existingCarousel.DescriptionAr = carouselDto.DescriptionAr;
            existingCarousel.Price = carouselDto.Price;
            existingCarousel.ProductUrl = carouselDto.ProductUrl;

            // Handle image deletion if specified
            if (!string.IsNullOrEmpty(carouselDto.ImageToDelete) && carouselDto.ImageToDelete == oldImageUrl)
            {
                try
                {
                    await _fileService.DeleteFileAsync(carouselDto.ImageToDelete);
                    existingCarousel.Image = string.Empty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting image {carouselDto.ImageToDelete}: {ex.Message}");
                }
            }

            // Handle new file upload if image is provided
            if (carouselDto.Image != null)
            {
                try
                {
                    // Delete old image if it exists
                    if (!string.IsNullOrEmpty(oldImageUrl) && oldImageUrl != carouselDto.ImageToDelete)
                    {
                        try
                        {
                            await _fileService.DeleteFileAsync(oldImageUrl);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting old image {oldImageUrl}: {ex.Message}");
                        }
                    }

                    var directory = $"carousels/{DateTime.UtcNow:yyyy/MM/dd}";
                    var imageUrl = await _fileService.SaveFileAsync(carouselDto.Image, directory);
                    existingCarousel.Image = imageUrl;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error uploading image: {ex.Message}", ex);
                }
            }

            _unitOfWork.Carousels.Update(existingCarousel);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CarouselDto>(existingCarousel);
        }

        public async Task<bool> DeleteCarouselAsync(int id)
        {
            var carousel = await _unitOfWork.Carousels.GetByIdAsync(id);
            if (carousel == null)
                return false;

            // Delete the image file if it exists
            if (!string.IsNullOrEmpty(carousel.Image))
            {
                try
                {
                    await _fileService.DeleteFileAsync(carousel.Image);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting image {carousel.Image}: {ex.Message}");
                }
            }

            _unitOfWork.Carousels.SoftDelete(carousel);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateCarouselAsync(int carouselId)
        {
            var carousel = await _unitOfWork.Carousels.GetByIdAsync(carouselId);
            if (carousel == null)
                return false;

            _unitOfWork.Carousels.Activate(carousel);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateCarouselAsync(int carouselId)
        {
            var carousel = await _unitOfWork.Carousels.GetByIdAsync(carouselId);
            if (carousel == null)
                return false;

            _unitOfWork.Carousels.Deactivate(carousel);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// نسبة رضا الزبائن = (متوسط تقييم المنتجات / 5) * 100
        /// إذا لا يوجد أي تقييمات ترجع 0.
        /// كما تعيد عدد المنتجات المتوفرة (Active + InStock).
        /// </summary>
        public async Task<CustomerSatisfactionDto> GetCustomerSatisfactionPercentageAsync()
        {
            // نستخدم DbContext مباشرة عبر UnitOfWork وأي Repo مناسب، هنا الأسهل عبر Products ورابطها مع Ratings
            var productsWithRatings = await _unitOfWork.Products.GetAllAsync();

            // نجمع كل التقييمات لجميع المنتجات
            var allRatings = productsWithRatings
                .SelectMany(p => p.Ratings)
                .ToList();

            double percentage = 0;

            if (allRatings.Count > 0)
            {
                var average = allRatings.Average(r => r.RatingNumber);

                // نفترض أن أعلى تقييم = 5
                const double maxRating = 5.0;
                percentage = (average / maxRating) * 100.0;
            }

            // عدد المنتجات المتوفرة: مفعلة وغير محذوفة ومتوفر منها في المخزون
            var availableProductsCount = productsWithRatings
                .Count(p => p.IsActive && p.IsInStock);

            return new CustomerSatisfactionDto
            {
                Percentage = Math.Round(percentage, 2),
                AvailableProductsCount = availableProductsCount
            };
        }

        /// <summary>
        /// إرجاع آخر ثلث التقييمات (بناءً على CreatedAt) مع معلومات المنتج.
        /// </summary>
        public async Task<IEnumerable<ProductRatingSummaryDto>> GetLatestThirdReviewsAsync()
        {
            // نستخدم DbContext من خلال UnitOfWork (عبر Ratings من Orders أو Products)
            // أبسط طريقة: الوصول إلى DbContext من ريبو المنتجات عن طريق Include لرابط Ratings
            var allProducts = await _unitOfWork.Products.GetAllAsync();

            var allRatings = allProducts
                .SelectMany(p => p.Ratings.Select(r => new { Product = p, Rating = r }))
                .OrderByDescending(x => x.Rating.CreatedAt)
                .ToList();

            if (allRatings.Count == 0)
                return Enumerable.Empty<ProductRatingSummaryDto>();

            var takeCount = Math.Max(1, allRatings.Count / 3); // "آخر ثلث" التقييمات

            var latestThird = allRatings
                .Take(takeCount)
                .Select(x => new ProductRatingSummaryDto
                {
                    RatingId = x.Rating.Id,
                    ProductId = x.Product.Id,
                    ProductTitle = x.Product.Title,
                    Content = x.Rating.Content,
                    RatingNumber = x.Rating.RatingNumber,
                    CreatedAt = x.Rating.CreatedAt
                })
                .ToList();

            return latestThird;
        }
    }
}

