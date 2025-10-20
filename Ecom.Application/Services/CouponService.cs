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

            _mapper.Map(couponDto, existingCoupon);
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

        public async Task<CouponValidationResultDto> ValidateCouponAsync(CouponValidationDto validationDto)
        {
            var coupon = await _unitOfWork.Coupons.GetByCodeAsync(validationDto.Code);
            
            if (coupon == null)
            {
                return new CouponValidationResultDto
                {
                    IsValid = false,
                    Message = "Invalid coupon code."
                };
            }

            var now = DateTime.UtcNow;

            // Check if coupon is active
            if (!coupon.IsActive)
            {
                return new CouponValidationResultDto
                {
                    IsValid = false,
                    Message = "Coupon is not active."
                };
            }

            // Check if coupon is expired
            if (now < coupon.StartDate || now > coupon.EndDate)
            {
                return new CouponValidationResultDto
                {
                    IsValid = false,
                    Message = "Coupon has expired or is not yet active."
                };
            }

            // Check usage limit
            if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit.Value)
            {
                return new CouponValidationResultDto
                {
                    IsValid = false,
                    Message = "Coupon usage limit has been reached."
                };
            }

            // Check minimum order amount
            if (coupon.MinimumOrderAmount.HasValue && validationDto.OrderAmount < coupon.MinimumOrderAmount.Value)
            {
                return new CouponValidationResultDto
                {
                    IsValid = false,
                    Message = $"Minimum order amount of ${coupon.MinimumOrderAmount.Value:F2} required."
                };
            }

            // Check if coupon is user-specific
            if (!string.IsNullOrEmpty(coupon.AppUserId) && validationDto.UserId != coupon.AppUserId)
            {
                return new CouponValidationResultDto
                {
                    IsValid = false,
                    Message = "This coupon is not valid for your account."
                };
            }

            // Calculate discount amount
            var discountAmount = CalculateDiscountAmount(coupon, validationDto.OrderAmount);

            return new CouponValidationResultDto
            {
                IsValid = true,
                Message = "Coupon is valid.",
                Coupon = _mapper.Map<CouponDto>(coupon),
                DiscountAmount = discountAmount,
                FinalAmount = validationDto.OrderAmount - discountAmount
            };
        }

        public async Task<CouponDto> ApplyCouponToOrderAsync(string couponCode, decimal orderAmount, string? userId = null)
        {
            var validationDto = new CouponValidationDto
            {
                Code = couponCode,
                OrderAmount = orderAmount,
                UserId = userId
            };

            var validationResult = await ValidateCouponAsync(validationDto);
            
            if (!validationResult.IsValid)
            {
                throw new ArgumentException(validationResult.Message);
            }

            return validationResult.Coupon!;
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
