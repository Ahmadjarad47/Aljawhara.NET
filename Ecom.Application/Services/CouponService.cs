using AutoMapper;
using Ecom.Application.DTOs.Coupon;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.UnitOfWork;

namespace Ecom.Application.Services
{
    public class CouponService : ICouponService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CouponService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IReadOnlyList<StorefrontCouponDto>> GetPublicCouponsAsync() =>
            (await _unitOfWork.Coupons.GetPublicCouponsAsync()).Select(ToStorefront).ToList();

        public async Task<StorefrontCouponDto?> GetVisibleCouponAsync(int? id, string? code, string? userId)
        {
            var coupon = id.HasValue
                ? await _unitOfWork.Coupons.GetCouponWithDetailsAsync(id.Value)
                : await _unitOfWork.Coupons.GetByCodeAsync(code!);
            return coupon != null && (IsPublicPromotion(coupon, DateTime.UtcNow) ||
                (coupon.AppUserId != null && coupon.AppUserId == userId)) ? ToStorefront(coupon) : null;
        }

        public async Task<IReadOnlyList<StorefrontCouponDto>> GetOwnedCouponsAsync(string userId) =>
            (await _unitOfWork.Coupons.GetCouponsByUserAsync(userId)).Select(ToStorefront).ToList();

        public async Task<StorefrontCouponValidationDto> PreviewCouponAsync(CouponValidationDto validation, string? userId)
        {
            var coupon = await _unitOfWork.Coupons.GetByCodeAsync(validation.Code);
            var now = DateTime.UtcNow;
            if (coupon == null || EligibilityError(coupon, validation.OrderAmount, userId, now) != null)
                return new(false, "Coupon is invalid or unavailable.", null, 0m, 0m);

            var discount = CalculateDiscountAmount(coupon, validation.OrderAmount);
            var visible = IsPublicPromotion(coupon, now) || (coupon.AppUserId != null && coupon.AppUserId == userId);
            return new(true, "Coupon is valid.", visible ? ToStorefront(coupon) : null,
                discount, validation.OrderAmount - discount);
        }

        private static StorefrontCouponDto ToStorefront(Coupon coupon) => new(coupon.Id, coupon.Code,
            coupon.Description, coupon.Type, coupon.Value, coupon.MinimumOrderAmount, coupon.MaximumDiscountAmount,
            coupon.StartDate, coupon.EndDate);

        private static bool IsPublicPromotion(Coupon coupon, DateTime now) =>
            coupon.IsPublic && coupon.AppUserId == null && !coupon.IsDeleted && coupon.IsActive &&
            coupon.StartDate <= now && coupon.EndDate >= now &&
            (!coupon.UsageLimit.HasValue || coupon.UsedCount < coupon.UsageLimit.Value) &&
            (!coupon.IsSingleUse || coupon.UsedCount == 0);

        public async Task<CouponDto?> GetCouponByIdAsync(int id)
        {
            var coupon = await _unitOfWork.Coupons.GetCouponWithDetailsAsync(id);
            return coupon != null ? _mapper.Map<CouponDto>(coupon) : null;
        }

        public async Task<CouponDto?> GetCouponByCodeAsync(string code)
        {
            var coupon = await _unitOfWork.Coupons.GetByCodeAsync(code);
            return coupon != null ? _mapper.Map<CouponDto>(coupon) : null;
        }

        public async Task<IEnumerable<CouponDto>> GetAllCouponsAsync()
        {
            var coupons = await _unitOfWork.Coupons.GetAllAsync();
            return _mapper.Map<IEnumerable<CouponDto>>(coupons);
        }

        public async Task<IEnumerable<CouponDto>> GetActiveCouponsAsync()
        {
            var coupons = await _unitOfWork.Coupons.GetActiveCouponsAsync();
            return _mapper.Map<IEnumerable<CouponDto>>(coupons);
        }

        public async Task<IEnumerable<CouponDto>> GetCouponsByUserAsync(string userId)
        {
            var coupons = await _unitOfWork.Coupons.GetCouponsByUserAsync(userId);
            return _mapper.Map<IEnumerable<CouponDto>>(coupons);
        }

        public async Task<IEnumerable<CouponSummaryDto>> GetCouponSummariesAsync()
        {
            var coupons = await _unitOfWork.Coupons.GetAllAsync();
            return _mapper.Map<IEnumerable<CouponSummaryDto>>(coupons);
        }

        public async Task<CouponDto> CreateCouponAsync(CouponCreateDto couponDto)
        {
            // Check if code is unique
            if (!await _unitOfWork.Coupons.IsCodeUniqueAsync(couponDto.Code))
            {
                throw new ArgumentException("Coupon code already exists.");
            }

            // Validate dates
            if (couponDto.StartDate >= couponDto.EndDate)
            {
                throw new ArgumentException("Start date must be before end date.");
            }

            // Validate value based on type
            if (couponDto.Type == CouponType.Percentage && couponDto.Value > 100)
            {
                throw new ArgumentException("Percentage value cannot exceed 100.");
            }

            if (couponDto.IsPublic && couponDto.AppUserId != null)
                throw new ArgumentException("Assigned coupons cannot be public.");

            var coupon = _mapper.Map<Coupon>(couponDto);
            await _unitOfWork.Coupons.AddAsync(coupon);
            await _unitOfWork.SaveChangesAsync();

            var createdCoupon = await _unitOfWork.Coupons.GetCouponWithDetailsAsync(coupon.Id);
            return _mapper.Map<CouponDto>(createdCoupon);
        }

        public async Task<CouponDto> UpdateCouponAsync(CouponUpdateDto couponDto)
        {
            var existingCoupon = await _unitOfWork.Coupons.GetByIdAsync(couponDto.Id);
            if (existingCoupon == null)
                throw new ArgumentException($"Coupon with ID {couponDto.Id} not found.");

            // Check if code is unique (excluding current coupon)
            if (!await _unitOfWork.Coupons.IsCodeUniqueAsync(couponDto.Code, couponDto.Id))
            {
                throw new ArgumentException("Coupon code already exists.");
            }

            // Validate dates
            if (couponDto.StartDate >= couponDto.EndDate)
            {
                throw new ArgumentException("Start date must be before end date.");
            }

            // Validate value based on type
            if (couponDto.Type == CouponType.Percentage && couponDto.Value > 100)
            {
                throw new ArgumentException("Percentage value cannot exceed 100.");
            }

            if (couponDto.IsPublic == true && couponDto.AppUserId != null)
                throw new ArgumentException("Assigned coupons cannot be public.");

            var isPublic = couponDto.AppUserId == null && (couponDto.IsPublic ?? existingCoupon.IsPublic);
            _mapper.Map(couponDto, existingCoupon);
            existingCoupon.IsPublic = isPublic;
            _unitOfWork.Coupons.Update(existingCoupon);
            await _unitOfWork.SaveChangesAsync();

            var updatedCoupon = await _unitOfWork.Coupons.GetCouponWithDetailsAsync(couponDto.Id);
            return _mapper.Map<CouponDto>(updatedCoupon);
        }

        public async Task<bool> DeleteCouponAsync(int id)
        {
            var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
            if (coupon == null)
                return false;

            _unitOfWork.Coupons.SoftDelete(coupon);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateCouponAsync(int id)
        {
            var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
            if (coupon == null)
                return false;

            coupon.IsActive = true;
            _unitOfWork.Coupons.Update(coupon);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateCouponAsync(int id)
        {
            var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
            if (coupon == null)
                return false;

            coupon.IsActive = false;
            _unitOfWork.Coupons.Update(coupon);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<CouponValidationResultDto> ValidateCouponAsync(CouponValidationDto dto, string? userId)
        {
            var coupon = await _unitOfWork.Coupons.GetByCodeAsync(dto.Code);
            var error = coupon == null ? "Invalid coupon code." : EligibilityError(coupon, dto.OrderAmount, userId, DateTime.UtcNow);
            if (error != null)
                return new CouponValidationResultDto { IsValid = false, Message = error };

            var discount = CalculateDiscountAmount(coupon!, dto.OrderAmount);
            return new CouponValidationResultDto
            {
                IsValid = true, Message = "Coupon is valid.", Coupon = _mapper.Map<CouponDto>(coupon),
                DiscountAmount = discount, FinalAmount = dto.OrderAmount - discount
            };
        }

        private static string? EligibilityError(Coupon coupon, decimal amount, string? userId, DateTime now)
        {
            if (coupon.IsDeleted || (coupon.AppUserId != null && coupon.AppUserId != userId))
                return "Invalid coupon code.";
            if (!coupon.IsActive || now < coupon.StartDate || now > coupon.EndDate)
                return "Coupon is not active.";
            if ((coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit.Value) ||
                (coupon.IsSingleUse && coupon.UsedCount > 0))
                return "Coupon usage limit has been reached.";
            if (coupon.MinimumOrderAmount.HasValue && amount < coupon.MinimumOrderAmount.Value)
                return "Order amount is below the coupon minimum.";
            return null;
        }

        public async Task<CouponDto> ApplyCouponToOrderAsync(string couponCode, decimal orderAmount, string? userId = null)
        {
            var result = await ValidateCouponAsync(new CouponValidationDto { Code = couponCode, OrderAmount = orderAmount }, userId);
            if (!result.IsValid)
                throw new ArgumentException(result.Message);
            return result.Coupon!;
        }

        public async Task<bool> IncrementCouponUsageAsync(int couponId)
        {
            try
            {
                await _unitOfWork.Coupons.IncrementUsageCountAsync(couponId);
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IEnumerable<CouponDto>> GetExpiredCouponsAsync()
        {
            var coupons = await _unitOfWork.Coupons.GetExpiredCouponsAsync();
            return _mapper.Map<IEnumerable<CouponDto>>(coupons);
        }

        public async Task<bool> CleanupExpiredCouponsAsync()
        {
            try
            {
                var expiredCoupons = await _unitOfWork.Coupons.GetExpiredCouponsAsync();
                foreach (var coupon in expiredCoupons)
                {
                    coupon.IsActive = false;
                    _unitOfWork.Coupons.Update(coupon);
                }
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private decimal CalculateDiscountAmount(Coupon coupon, decimal orderAmount)
        {
            decimal discountAmount = 0;

            switch (coupon.Type)
            {
                case CouponType.Percentage:
                    discountAmount = orderAmount * (coupon.Value / 100);
                    break;
                case CouponType.FixedAmount:
                    discountAmount = coupon.Value;
                    break;
                case CouponType.FreeShipping:
                    // This would be handled separately in shipping calculation
                    discountAmount = 0;
                    break;
            }

            // Apply maximum discount limit if specified
            if (coupon.MaximumDiscountAmount.HasValue && discountAmount > coupon.MaximumDiscountAmount.Value)
            {
                discountAmount = coupon.MaximumDiscountAmount.Value;
            }

            // Ensure discount doesn't exceed order amount
            if (discountAmount > orderAmount)
            {
                discountAmount = orderAmount;
            }

            return discountAmount;
        }
    }
}
