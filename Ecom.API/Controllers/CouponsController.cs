using Ecom.Application.DTOs.Coupon;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        [HttpGet("active")]
        public async Task<ActionResult<IReadOnlyList<StorefrontCouponDto>>> GetCoupons() =>
            Ok(await _couponService.GetPublicCouponsAsync());

        [HttpGet("{id:int}")]
        public async Task<ActionResult<StorefrontCouponDto>> GetCoupon(int id)
        {
            var coupon = await _couponService.GetVisibleCouponAsync(id, null, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return coupon == null ? NotFound() : Ok(coupon);
        }

        [HttpGet("code/{code}")]
        public async Task<ActionResult<StorefrontCouponDto>> GetCouponByCode(string code)
        {
            var coupon = await _couponService.GetVisibleCouponAsync(null, code, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return coupon == null ? NotFound() : Ok(coupon);
        }

        [HttpPost("validate")]
        public async Task<ActionResult<StorefrontCouponValidationDto>> ValidateCoupon([FromBody] CouponValidationDto dto) =>
            Ok(await _couponService.PreviewCouponAsync(dto, User.FindFirstValue(ClaimTypes.NameIdentifier)));

        [HttpGet("my-coupons")]
        [Authorize]
        public async Task<ActionResult<IReadOnlyList<StorefrontCouponDto>>> GetMyCoupons()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userId) ? Unauthorized() : Ok(await _couponService.GetOwnedCouponsAsync(userId));
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
                return CreatedAtAction(nameof(AdminCouponsController.GetCoupon), "AdminCoupons", new { id = coupon.Id }, coupon);
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
