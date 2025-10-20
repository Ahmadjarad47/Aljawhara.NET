using Ecom.Application.DTOs.Coupon;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CouponsController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public CouponsController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CouponSummaryDto>>> GetCoupons()
        {
            var coupons = await _couponService.GetCouponSummariesAsync();
            return Ok(coupons);
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<CouponDto>>> GetActiveCoupons()
        {
            var coupons = await _couponService.GetActiveCouponsAsync();
            return Ok(coupons);
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

        [HttpGet("my-coupons")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<CouponDto>>> GetMyCoupons()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var coupons = await _couponService.GetCouponsByUserAsync(userId);
            return Ok(coupons);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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

        [HttpGet("expired")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<CouponDto>>> GetExpiredCoupons()
        {
            var coupons = await _couponService.GetExpiredCouponsAsync();
            return Ok(coupons);
        }

        [HttpPost("cleanup-expired")]
        [Authorize(Roles = "Admin")]
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
    }
}
