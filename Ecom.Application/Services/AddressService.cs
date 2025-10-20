using AutoMapper;
using Ecom.Application.DTOs.Auth;
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

        // User address management methods
        public async Task<IEnumerable<UserAddressDto>> GetUserAddressesAsync(string userId)
        {
            var addresses = await _unitOfWork.ShippingAddresses.GetAllAsync();
            var userAddresses = addresses.Where(a => a.AppUserId == userId && !a.IsDeleted);
            return _mapper.Map<IEnumerable<UserAddressDto>>(userAddresses);
        }

        public async Task<UserAddressDto?> GetAddressByIdAsync(int addressId, string userId)
        {
            var address = await _unitOfWork.ShippingAddresses.GetByIdAsync(addressId);
            
            if (address == null || address.AppUserId != userId || address.IsDeleted)
            {
                return null;
            }

            return _mapper.Map<UserAddressDto>(address);
        }

        public async Task<UserAddressDto> CreateAddressAsync(CreateAddressDto addressDto, string userId)
        {
            // If this is set as default, unset all other default addresses
            if (addressDto.IsDefault)
            {
                await SetAllAddressesNonDefaultAsync(userId);
            }

            var address = _mapper.Map<Domain.Entity.ShippingAddress>(addressDto);
            address.AppUserId = userId;
            address.CreatedAt = DateTime.UtcNow;
            address.UpdatedAt = DateTime.UtcNow;
            address.IsDeleted = false;

            await _unitOfWork.ShippingAddresses.AddAsync(address);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<UserAddressDto>(address);
        }

        public async Task<UserAddressDto> UpdateAddressAsync(UpdateAddressDto addressDto, string userId)
        {
            var existingAddress = await _unitOfWork.ShippingAddresses.GetByIdAsync(addressDto.Id);
            
            if (existingAddress == null || existingAddress.AppUserId != userId || existingAddress.IsDeleted)
            {
                throw new ArgumentException("Address not found or access denied");
            }

            // If this is set as default, unset all other default addresses
            if (addressDto.IsDefault)
            {
                await SetAllAddressesNonDefaultAsync(userId);
            }

            _mapper.Map(addressDto, existingAddress);
            existingAddress.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.ShippingAddresses.Update(existingAddress);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<UserAddressDto>(existingAddress);
        }

        public async Task<bool> DeleteAddressAsync(int addressId, string userId)
        {
            var address = await _unitOfWork.ShippingAddresses.GetByIdAsync(addressId);
            
            if (address == null || address.AppUserId != userId || address.IsDeleted)
            {
                return false;
            }

            // Check if address is being used in any orders
            var ordersUsingAddress = await _unitOfWork.Orders.GetAllAsync();
            if (ordersUsingAddress.Any(o => o.ShippingAddressId == addressId))
            {
                // Soft delete if address is used in orders
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
            
            if (address == null || address.AppUserId != userId || address.IsDeleted)
            {
                return false;
            }

            // Unset all other default addresses
            await SetAllAddressesNonDefaultAsync(userId);

            // Set this address as default
            address.IsDefault = true;
            address.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ShippingAddresses.Update(address);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        private async Task SetAllAddressesNonDefaultAsync(string userId)
        {
            var addresses = await _unitOfWork.ShippingAddresses.GetAllAsync();
            var userAddresses = addresses.Where(a => a.AppUserId == userId && !a.IsDeleted);
            
            foreach (var addr in userAddresses)
            {
                addr.IsDefault = false;
                addr.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.ShippingAddresses.Update(addr);
            }
        }

        // Legacy methods for backward compatibility
        public async Task<IEnumerable<ShippingAddressDto>> GetUserAddressesLegacyAsync(string userId)
        {
            var addresses = await _unitOfWork.ShippingAddresses.GetAllAsync();
            var userAddresses = addresses.Where(a => a.AppUserId == userId);
            return _mapper.Map<IEnumerable<ShippingAddressDto>>(userAddresses);
        }

        public async Task<ShippingAddressDto?> GetAddressByIdLegacyAsync(int addressId, string userId)
        {
            var address = await _unitOfWork.ShippingAddresses.GetByIdAsync(addressId);
            
            if (address == null || address.AppUserId != userId)
            {
                return null;
            }

            return _mapper.Map<ShippingAddressDto>(address);
        }

        public async Task<ShippingAddressDto> CreateAddressLegacyAsync(ShippingAddressCreateDto addressDto, string userId)
        {
            var address = _mapper.Map<Domain.Entity.ShippingAddress>(addressDto);
            address.AppUserId = userId;
            address.CreatedAt = DateTime.UtcNow;
            address.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.ShippingAddresses.AddAsync(address);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<ShippingAddressDto>(address);
        }

        public async Task<ShippingAddressDto> UpdateAddressLegacyAsync(ShippingAddressUpdateDto addressDto, string userId)
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
    }
}
