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
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUsers> _userManager;
        private readonly SignInManager<AppUsers> _signInManager;
        private readonly IAddressService _addressService;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<AppUsers> userManager, SignInManager<AppUsers> signInManager, IAddressService addressService, IJwtService jwtService, IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _addressService = addressService;
            _jwtService = jwtService;
            _configuration = configuration;
        }

        [HttpGet("isAuth")]
        public IActionResult isAuth() => User.Identity!.IsAuthenticated ? Ok() : Unauthorized();
        [HttpGet("isAdmin")]
        public IActionResult Get()
        {
            return User.Identity!.IsAuthenticated ? Ok(new { isAdmin = true }) : Unauthorized();
        }


        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new AppUsers
            {
                UserName = registerDto.Username,
                Email = registerDto.Email,
                PhoneNumber = registerDto.PhoneNumber,
                EmailConfirmed = true // For demo purposes
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (result.Succeeded)
            {
                return Ok(new { Message = "User registered successfully", UserId = user.Id });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return BadRequest(ModelState);
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(loginDto.Email);
            if (user == null)
            {
                return BadRequest("Invalid email or password");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var token = _jwtService.GenerateToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var expiryMinutes = int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");
                var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

                var response = new LoginResponseDto
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt,
                    User = new UserResponseDto
                    {
                        Id = user.Id,
                        Username = user.UserName ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        PhoneNumber = user.PhoneNumber ?? string.Empty,
                        EmailConfirmed = user.EmailConfirmed
                    }
                };

                return Ok(response);
            }

            return BadRequest("Invalid email or password");
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult> Logout()
        {
            // With JWT, logout is typically handled client-side by removing the token
            // Server-side logout would require token blacklisting which is more complex
            return Ok(new { Message = "Logout successful" });
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<LoginResponseDto>> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // In a real application, you would validate the refresh token against a database
            // For now, we'll generate a new token (this is a simplified implementation)
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid refresh token");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized("User not found");
            }

            var token = _jwtService.GenerateToken(user);
            var newRefreshToken = _jwtService.GenerateRefreshToken();
            var expiryMinutes = int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");
            var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var response = new LoginResponseDto
            {
                Token = token,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiresAt,
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    EmailConfirmed = user.EmailConfirmed
                }
            };

            return Ok(response);
        }

        // User's own address management endpoints
        [HttpGet("my-addresses")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<ShippingAddressDto>>> GetMyAddresses()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var addresses = await _addressService.GetUserAddressesAsync(userId);
                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while retrieving addresses", Error = ex.Message });
            }
        }

        [HttpGet("my-addresses/{addressId}")]
        [Authorize]
        public async Task<ActionResult<ShippingAddressDto>> GetMyAddress(int addressId)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

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

        [HttpPost("my-addresses")]
        [Authorize]
        public async Task<ActionResult<ShippingAddressDto>> CreateMyAddress([FromBody] ShippingAddressCreateDto addressDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var address = await _addressService.CreateAddressAsync(addressDto, userId);
                return CreatedAtAction(nameof(GetMyAddress), new { addressId = address.Id }, address);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while creating the address", Error = ex.Message });
            }
        }

        [HttpPut("my-addresses/{addressId}")]
        [Authorize]
        public async Task<ActionResult<ShippingAddressDto>> UpdateMyAddress(int addressId, [FromBody] ShippingAddressUpdateDto addressDto)
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
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

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

        [HttpDelete("my-addresses/{addressId}")]
        [Authorize]
        public async Task<ActionResult> DeleteMyAddress(int addressId)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

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

        [HttpPost("my-addresses/{addressId}/set-default")]
        [Authorize]
        public async Task<ActionResult> SetMyDefaultAddress(int addressId)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

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

