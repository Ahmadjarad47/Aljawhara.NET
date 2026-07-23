using Ecom.Application.DTOs.Auth;
using Ecom.Application.DTOs.Order;

namespace Ecom.Application.Services.Interfaces
{
    public interface IAddressService
    {
        // User address management methods
        Task<IEnumerable<UserAddressDto>> GetUserAddressesAsync(string userId);
        Task<UserAddressDto?> GetAddressByIdAsync(int addressId, string userId);
        Task<UserAddressDto> CreateAddressAsync(CreateAddressDto addressDto, string userId);
        Task<UserAddressDto> UpdateAddressAsync(UpdateAddressDto addressDto, string userId);
        Task<bool> DeleteAddressAsync(int addressId, string userId);
        Task<bool> SetDefaultAddressAsync(int addressId, string userId);
        
        // Legacy methods for backward compatibility
        Task<IEnumerable<ShippingAddressDto>> GetUserAddressesLegacyAsync(string userId);
        Task<ShippingAddressDto?> GetAddressByIdLegacyAsync(int addressId, string userId);
        Task<ShippingAddressDto> CreateAddressLegacyAsync(ShippingAddressCreateDto addressDto, string userId);
        Task<ShippingAddressDto> UpdateAddressLegacyAsync(ShippingAddressUpdateDto addressDto, string userId);
    }
}
