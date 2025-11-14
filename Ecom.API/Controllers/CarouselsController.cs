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
            var carousels = await _carouselService.GetActiveCarouselsAsync();
            return Ok(carousels);
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
    }
}

