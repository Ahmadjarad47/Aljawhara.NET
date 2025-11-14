using Ecom.Application.DTOs.Carousel;
using Ecom.Application.DTOs.Common;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/carousels")]
    [Authorize(Roles = "Admin")]
    public class AdminCarouselController : ControllerBase
    {
        private readonly ICarouselService _carouselService;

        public AdminCarouselController(ICarouselService carouselService)
        {
            _carouselService = carouselService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<CarouselDto>>> GetCarousels(
            [FromQuery] bool? isActive = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allCarousels = await _carouselService.GetAllCarouselsAsync();
            var carouselsList = allCarousels.ToList();

            // Apply active filter if specified
            if (isActive.HasValue)
            {
                carouselsList = carouselsList.Where(c => c.IsActive == isActive.Value).ToList();
            }

            var totalCount = carouselsList.Count;
            var pagedCarousels = carouselsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<CarouselDto>(pagedCarousels, totalCount, pageNumber, pageSize);
            return Ok(result);
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

        [HttpPost]
        public async Task<ActionResult<CarouselDto>> CreateCarousel(
            [FromForm] CarouselCreateWithFileDto carouselDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var carousel = await _carouselService.CreateCarouselWithFileAsync(carouselDto);
                return CreatedAtAction(nameof(GetCarousel), new { id = carousel.Id }, carousel);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CarouselDto>> UpdateCarousel(
            int id,
            [FromForm] CarouselUpdateWithFileDto carouselDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                carouselDto.Id = id;
                var carousel = await _carouselService.UpdateCarouselWithFileAsync(carouselDto);
                return Ok(carousel);
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
        public async Task<ActionResult> DeleteCarousel(int id)
        {
            try
            {
                var result = await _carouselService.DeleteCarouselAsync(id);
                if (!result)
                {
                    return NotFound($"Carousel with ID {id} not found.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}/toggle-active")]
        public async Task<ActionResult<object>> ToggleCarouselActive(int id)
        {
            try
            {
                var carousel = await _carouselService.GetCarouselByIdAsync(id);
                if (carousel == null)
                {
                    return NotFound($"Carousel with ID {id} not found.");
                }

                bool newStatus;
                if (carousel.IsActive)
                {
                    var result = await _carouselService.DeactivateCarouselAsync(id);
                    newStatus = false;
                }
                else
                {
                    var result = await _carouselService.ActivateCarouselAsync(id);
                    newStatus = true;
                }

                return Ok(new
                {
                    CarouselId = id,
                    IsActive = newStatus,
                    Message = $"Carousel {(newStatus ? "activated" : "deactivated")} successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

