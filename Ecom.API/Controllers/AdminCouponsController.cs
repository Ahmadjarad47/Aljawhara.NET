using Ecom.Application.DTOs.Common;
using Ecom.Application.DTOs.Coupon;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/coupons")]
    [Authorize(Roles ="Admin")]
    public class AdminCouponsController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public AdminCouponsController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<CouponDto>>> GetCoupons(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allCoupons = await _couponService.GetAllCouponsAsync();
            var couponsList = allCoupons.ToList();
            var totalCount = couponsList.Count;

            var pagedCoupons = couponsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<CouponDto>(pagedCoupons, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("active")]
        public async Task<ActionResult<PagedResult<CouponDto>>> GetActiveCoupons(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allCoupons = await _couponService.GetActiveCouponsAsync();
            var couponsList = allCoupons.ToList();
            var totalCount = couponsList.Count;

            var pagedCoupons = couponsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<CouponDto>(pagedCoupons, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("expired")]
        public async Task<ActionResult<PagedResult<CouponDto>>> GetExpiredCoupons(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allCoupons = await _couponService.GetExpiredCouponsAsync();
            var couponsList = allCoupons.ToList();
            var totalCount = couponsList.Count;

            var pagedCoupons = couponsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<CouponDto>(pagedCoupons, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CouponDto>> GetCoupon(int id)
        {
            var coupon = await _couponService.GetCouponByIdAsync(id);
            if (coupon == null)
            {
                return NotFound($"Coupon with ID {id} not found.");
            }
            return Ok(coupon);
        }

        [HttpGet("code/{code}")]
        public async Task<ActionResult<CouponDto>> GetCouponByCode(string code)
        {
            var coupon = await _couponService.GetCouponByCodeAsync(code);
            if (coupon == null)
            {
                return NotFound($"Coupon with code '{code}' not found.");
            }
            return Ok(coupon);
        }

        [HttpPost]
        public async Task<ActionResult<CouponDto>> CreateCoupon([FromBody] CouponCreateDto couponDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var coupon = await _couponService.CreateCouponAsync(couponDto);
                return CreatedAtAction(nameof(GetCoupon), new { id = coupon.Id }, coupon);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CouponDto>> UpdateCoupon(int id, [FromBody] CouponUpdateDto couponDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != couponDto.Id)
            {
                return BadRequest("ID mismatch.");
            }

            try
            {
                var coupon = await _couponService.UpdateCouponAsync(couponDto);
                return Ok(coupon);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteCoupon(int id)
        {
            try
            {
                var result = await _couponService.DeleteCouponAsync(id);
                if (!result)
                {
                    return NotFound($"Coupon with ID {id} not found.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("{id}/activate")]
        public async Task<ActionResult> ActivateCoupon(int id)
        {
            try
            {
                var result = await _couponService.ActivateCouponAsync(id);
                if (!result)
                {
                    return NotFound($"Coupon with ID {id} not found.");
                }
                return Ok(new { Message = "Coupon activated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("{id}/deactivate")]
        public async Task<ActionResult> DeactivateCoupon(int id)
        {
            try
            {
                var result = await _couponService.DeactivateCouponAsync(id);
                if (!result)
                {
                    return NotFound($"Coupon with ID {id} not found.");
                }
                return Ok(new { Message = "Coupon deactivated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("cleanup-expired")]
        public async Task<ActionResult> CleanupExpiredCoupons()
        {
            try
            {
                var result = await _couponService.CleanupExpiredCouponsAsync();
                if (result)
                {
                    return Ok(new { Message = "Expired coupons cleaned up successfully." });
                }
                return StatusCode(500, "Failed to cleanup expired coupons.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("validate")]
        public async Task<ActionResult<CouponValidationResultDto>> ValidateCoupon([FromBody] CouponValidationDto validationDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _couponService.ValidateCouponAsync(validationDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
