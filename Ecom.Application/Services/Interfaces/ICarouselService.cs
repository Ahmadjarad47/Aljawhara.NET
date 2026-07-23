using Ecom.Application.DTOs.Carousel;

namespace Ecom.Application.Services.Interfaces
{
    public interface ICarouselService
    {
        Task<CarouselDto?> GetCarouselByIdAsync(int id);
        Task<IEnumerable<CarouselDto>> GetAllCarouselsAsync();
        Task<IEnumerable<CarouselDto>> GetActiveCarouselsAsync();

        /// <summary>
        /// نسبة رضا الزبائن (0-100) مبنية على متوسط تقييمات المنتجات
        /// مع عدد المنتجات المتوفرة.
        /// </summary>
        Task<CustomerSatisfactionDto> GetCustomerSatisfactionPercentageAsync();

        /// <summary>
        /// إرجاع آخر ثلث التقييمات (Reviews) بترتيب تنازلي حسب التاريخ.
        /// </summary>
        Task<IEnumerable<ProductRatingSummaryDto>> GetLatestThirdReviewsAsync();
        
        Task<CarouselDto> CreateCarouselAsync(CarouselCreateDto carouselDto);
        Task<CarouselDto> CreateCarouselWithFileAsync(CarouselCreateWithFileDto carouselDto);
        Task<CarouselDto> UpdateCarouselAsync(CarouselUpdateDto carouselDto);
        Task<CarouselDto> UpdateCarouselWithFileAsync(CarouselUpdateWithFileDto carouselDto);
        Task<bool> DeleteCarouselAsync(int id);
        
        // IsActive management methods
        Task<bool> ActivateCarouselAsync(int carouselId);
        Task<bool> DeactivateCarouselAsync(int carouselId);
    }
}

