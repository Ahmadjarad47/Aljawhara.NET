using AutoMapper;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Infrastructure.UnitOfWork;

namespace Ecom.Application.Services
{
    public class AddressService : IAddressService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public AddressService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ShippingAddressDto>> GetUserAddressesAsync(string userId)
        {
            var addresses = await _unitOfWork.ShippingAddresses.GetAllAsync();
            var userAddresses = addresses.Where(a => a.AppUserId == userId);
            return _mapper.Map<IEnumerable<ShippingAddressDto>>(userAddresses);
        }

        public async Task<ShippingAddressDto?> GetAddressByIdAsync(int addressId, string userId)
        {
            var address = await _unitOfWork.ShippingAddresses.GetByIdAsync(addressId);
            
            if (address == null || address.AppUserId != userId)
            {
                return null;
            }

            return _mapper.Map<ShippingAddressDto>(address);
        }

        public async Task<ShippingAddressDto> CreateAddressAsync(ShippingAddressCreateDto addressDto, string userId)
        {
            var address = _mapper.Map<Domain.Entity.ShippingAddress>(addressDto);
            address.AppUserId = userId;
            address.CreatedAt = DateTime.UtcNow;
            address.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.ShippingAddresses.AddAsync(address);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<ShippingAddressDto>(address);
        }

        public async Task<ShippingAddressDto> UpdateAddressAsync(ShippingAddressUpdateDto addressDto, string userId)
        {
            var existingAddress = await _unitOfWork.ShippingAddresses.GetByIdAsync(addressDto.Id);
            
            if (existingAddress == null || existingAddress.AppUserId != userId)
            {
                throw new ArgumentException("Address not found or access denied");
            }

            _mapper.Map(addressDto, existingAddress);
            existingAddress.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.ShippingAddresses.Update(existingAddress);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<ShippingAddressDto>(existingAddress);
        }

        public async Task<bool> DeleteAddressAsync(int addressId, string userId)
        {
            var address = await _unitOfWork.ShippingAddresses.GetByIdAsync(addressId);
            
            if (address == null || address.AppUserId != userId)
            {
                return false;
            }

            // Check if address is being used in any orders
            var ordersUsingAddress = await _unitOfWork.Orders.GetAllAsync();
            if (ordersUsingAddress.Any(o => o.ShippingAddressId == addressId))
            {
                // Soft delete instead of hard delete if address is used in orders
                address.IsDeleted = true;
                address.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.ShippingAddresses.Update(address);
            }
            else
            {
                // Hard delete if not used in any orders
                _unitOfWork.ShippingAddresses.Remove(address);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetDefaultAddressAsync(int addressId, string userId)
        {
            var address = await _unitOfWork.ShippingAddresses.GetByIdAsync(addressId);
            
            if (address == null || address.AppUserId != userId)
            {
                return false;
            }

            // For now, we'll just return true as the current implementation doesn't have a default flag
            // In a real application, you might want to add a IsDefault property to the ShippingAddress entity
            // and update all other addresses to set IsDefault = false, then set this one to true
            
            return true;
        }
    }
}
