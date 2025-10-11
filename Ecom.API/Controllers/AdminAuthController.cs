using System.ComponentModel.DataAnnotations;
using Ecom.Application.DTOs.Auth;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/Auth")]
    [Authorize(Roles = "Admin")]
    public class AdminAuthController : ControllerBase
    {
        private readonly UserManager<AppUsers> _userManager;
        private readonly SignInManager<AppUsers> _signInManager;
        private readonly IAddressService _addressService;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;

        public AdminAuthController(UserManager<AppUsers> userManager, SignInManager<AppUsers> signInManager, IAddressService addressService, IJwtService jwtService, IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _addressService = addressService;
            _jwtService = jwtService;
            _configuration = configuration;
        }

        [HttpGet("isAdmin")]
        public IActionResult Get()
        {
            return User.Identity!.IsAuthenticated ? Ok(new { isAdmin = true }) : Unauthorized();
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult> GetUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed
            });
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAllUsers()
        {
            var users = _userManager.Users.ToList();
            var userDtos = users.Select(user => new UserResponseDto
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed
            });

            return Ok(userDtos);
        }

        [HttpPost("user/{userId}/addresses")]
        public async Task<ActionResult<ShippingAddressDto>> CreateAddressForUser(string userId, [FromBody] ShippingAddressCreateDto addressDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var address = await _addressService.CreateAddressAsync(addressDto, userId);
                return CreatedAtAction(nameof(GetAddress), new { userId, addressId = address.Id }, address);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while creating the address", Error = ex.Message });
            }
        }

        [HttpGet("user/{userId}/addresses")]
        public async Task<ActionResult<IEnumerable<ShippingAddressDto>>> GetUserAddresses(string userId)
        {
            try
            {
                var addresses = await _addressService.GetUserAddressesAsync(userId);
                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while retrieving addresses", Error = ex.Message });
            }
        }

        [HttpGet("user/{userId}/addresses/{addressId}")]
        public async Task<ActionResult<ShippingAddressDto>> GetAddress(string userId, int addressId)
        {
            try
            {
                var address = await _addressService.GetAddressByIdAsync(addressId, userId);
                if (address == null)
                {
                    return NotFound("Address not found");
                }
                return Ok(address);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while retrieving the address", Error = ex.Message });
            }
        }

        [HttpPut("user/{userId}/addresses/{addressId}")]
        public async Task<ActionResult<ShippingAddressDto>> UpdateAddress(string userId, int addressId, [FromBody] ShippingAddressUpdateDto addressDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (addressId != addressDto.Id)
            {
                return BadRequest("Address ID mismatch");
            }

            try
            {
                var address = await _addressService.UpdateAddressAsync(addressDto, userId);
                return Ok(address);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while updating the address", Error = ex.Message });
            }
        }

        [HttpDelete("user/{userId}/addresses/{addressId}")]
        public async Task<ActionResult> DeleteAddress(string userId, int addressId)
        {
            try
            {
                var result = await _addressService.DeleteAddressAsync(addressId, userId);
                if (!result)
                {
                    return NotFound("Address not found");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while deleting the address", Error = ex.Message });
            }
        }

        [HttpPost("user/{userId}/addresses/{addressId}/set-default")]
        public async Task<ActionResult> SetDefaultAddress(string userId, int addressId)
        {
            try
            {
                var result = await _addressService.SetDefaultAddressAsync(addressId, userId);
                if (!result)
                {
                    return NotFound("Address not found");
                }
                return Ok(new { Message = "Default address set successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while setting the default address", Error = ex.Message });
            }
        }
    }
}
