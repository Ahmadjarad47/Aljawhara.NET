using Ecom.Application.DTOs.Order;

namespace Ecom.Application.Services.Interfaces
{
    public interface IAddressService
    {
        Task<IEnumerable<ShippingAddressDto>> GetUserAddressesAsync(string userId);
        Task<ShippingAddressDto?> GetAddressByIdAsync(int addressId, string userId);
        Task<ShippingAddressDto> CreateAddressAsync(ShippingAddressCreateDto addressDto, string userId);
        Task<ShippingAddressDto> UpdateAddressAsync(ShippingAddressUpdateDto addressDto, string userId);
        Task<bool> DeleteAddressAsync(int addressId, string userId);
        Task<bool> SetDefaultAddressAsync(int addressId, string userId);
    }
}
