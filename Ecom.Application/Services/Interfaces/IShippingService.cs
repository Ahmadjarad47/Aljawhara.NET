using Ecom.Application.DTOs.Order;
using Ecom.Domain.constant;

namespace Ecom.Application.Services.Interfaces;

public interface IShippingService
{
    Task<ShippingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<ShippingSettingsDto> UpdateSettingsAsync(ShippingSettingsDto settings, CancellationToken cancellationToken = default);
    Task<decimal> CalculateAsync(decimal subtotal, CouponType? couponType, string? city, string? state = null, CancellationToken cancellationToken = default);
}
