using Ecom.Application.DTOs.Carousel;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarouselsController : ControllerBase
    {
        private readonly ICarouselService _carouselService;

        public CarouselsController(ICarouselService carouselService)
        {
            _carouselService = carouselService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CarouselDto>>> GetCarousels()
        {
            IEnumerable<CarouselDto>? carousels = await _carouselService.GetActiveCarouselsAsync();
            return Ok(carousels.ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CarouselDto>> GetCarousel(int id)
        {
            var carousel = await _carouselService.GetCarouselByIdAsync(id);
            if (carousel == null)
            {
                return NotFound($"Carousel with ID {id} not found.");
            }
            return Ok(carousel);
        }

        /// <summary>
        /// نسبة رضا الزبائن بالمئة مبنية على متوسط تقييمات المنتجات (Ratings).
        /// </summary>
        [HttpGet("customer-satisfaction")]
        public async Task<ActionResult<CustomerSatisfactionDto>> GetCustomerSatisfactionPercentage()
        {
            var result = await _carouselService.GetCustomerSatisfactionPercentageAsync();
            return Ok(result);
        }

        /// <summary>
        /// إرجاع آخر ثلث التقييمات (Reviews) بترتيب تنازلي حسب التاريخ.
        /// </summary>
        [HttpGet("latest-third-reviews")]
        public async Task<ActionResult<IEnumerable<ProductRatingSummaryDto>>> GetLatestThirdReviews()
        {
            var reviews = await _carouselService.GetLatestThirdReviewsAsync();
            return Ok(reviews.ToList());
        }
    }
}

