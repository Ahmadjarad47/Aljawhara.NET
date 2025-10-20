using System.ComponentModel.DataAnnotations;
using Ecom.Application.DTOs.Auth;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserManagerService _userManagerService;
        private readonly IJwtService _jwtService;
        private readonly IAddressService _addressService;
        private readonly IConfiguration _configuration;

        public AuthController(
            IUserManagerService userManagerService, 
            IJwtService jwtService, 
            IAddressService addressService,
            IConfiguration configuration)
        {
            _userManagerService = userManagerService;
            _jwtService = jwtService;
            _addressService = addressService;
            _configuration = configuration;
        }

        [HttpGet("isAuth")]
        public IActionResult isAuth() => User.Identity!.IsAuthenticated ? Ok() : Unauthorized();
        
        [HttpGet("isAdmin")]
        [Authorize(Roles = "Admin")]
        public IActionResult Get()
        {
            return User.IsInRole("Admin") ? Ok(new { isAdmin = true }) : Unauthorized();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var result = await _userManagerService.RegisterAsync(registerDto);
            
            if (!result.Success)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = result.Message
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = result.Message
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var result = await _userManagerService.LoginAsync(loginDto);
            
            if (!result.Success || result.User == null)
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = result.Message
                });
            }

            // Generate JWT token
            var token = await _jwtService.GenerateTokenAsync(result.User);
            var refreshToken = _jwtService.GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60"));

            var userResponse = new UserResponseDto
            {
                Id = result.User.Id,
                Username = result.User.UserName ?? string.Empty,
                Email = result.User.Email ?? string.Empty,
                PhoneNumber = result.User.PhoneNumber ?? string.Empty,
                EmailConfirmed = result.User.EmailConfirmed,
                CreatedAt = result.User.CreatedAt
            };

            var loginResponse = new LoginResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = userResponse
            };

            return Ok(new ApiResponseDto<LoginResponseDto>
            {
                Success = true,
                Message = result.Message,
                Data = loginResponse
            });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyAccount([FromBody] VerifyAccountDto verifyDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var result = await _userManagerService.VerifyAccountAsync(verifyDto);
            
            if (!result.Success)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = result.Message
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = result.Message
            });
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto resendDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var result = await _userManagerService.ResendVerificationAsync(resendDto);
            
            if (!result.Success)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = result.Message
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = result.Message
            });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            var result = await _userManagerService.ChangePasswordAsync(userId, changePasswordDto);
            
            if (!result.Success)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = result.Message
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = result.Message
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var result = await _userManagerService.ForgotPasswordAsync(forgotPasswordDto);
            
            return Ok(new ApiResponseDto
            {
                Success = result.Success,
                Message = result.Message
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var result = await _userManagerService.ResetPasswordAsync(resetPasswordDto);
            
            if (!result.Success)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = result.Message
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = result.Message
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            var result = await _userManagerService.LogoutAsync(userId);
            
            return Ok(new ApiResponseDto
            {
                Success = result.Success,
                Message = result.Message
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            // In a real application, you would validate the refresh token here
            // For now, we'll return an error as this requires additional implementation
            return BadRequest(new ApiResponseDto
            {
                Success = false,
                Message = "Refresh token functionality not implemented yet"
            });
        }

        // Address Management Endpoints
        [HttpGet("addresses")]
        [Authorize]
        public async Task<IActionResult> GetUserAddresses()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            try
            {
                var addresses = await _addressService.GetUserAddressesAsync(userId);
                return Ok(new ApiResponseDto<IEnumerable<UserAddressDto>>
                {
                    Success = true,
                    Message = "Addresses retrieved successfully",
                    Data = addresses
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Error retrieving addresses",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("addresses/{addressId}")]
        [Authorize]
        public async Task<IActionResult> GetAddressById(int addressId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            try
            {
                var address = await _addressService.GetAddressByIdAsync(addressId, userId);
                if (address == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Address not found"
                    });
                }

                return Ok(new ApiResponseDto<UserAddressDto>
                {
                    Success = true,
                    Message = "Address retrieved successfully",
                    Data = address
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Error retrieving address",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("addresses")]
        [Authorize]
        public async Task<IActionResult> CreateAddress([FromBody] CreateAddressDto addressDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            try
            {
                var address = await _addressService.CreateAddressAsync(addressDto, userId);
                return CreatedAtAction(nameof(GetAddressById), new { addressId = address.Id }, new ApiResponseDto<UserAddressDto>
                {
                    Success = true,
                    Message = "Address created successfully",
                    Data = address
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Error creating address",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPut("addresses")]
        [Authorize]
        public async Task<IActionResult> UpdateAddress([FromBody] UpdateAddressDto addressDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            try
            {
                var address = await _addressService.UpdateAddressAsync(addressDto, userId);
                return Ok(new ApiResponseDto<UserAddressDto>
                {
                    Success = true,
                    Message = "Address updated successfully",
                    Data = address
                });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Error updating address",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpDelete("addresses/{addressId}")]
        [Authorize]
        public async Task<IActionResult> DeleteAddress(int addressId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            try
            {
                var result = await _addressService.DeleteAddressAsync(addressId, userId);
                if (!result)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Address not found"
                    });
                }

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Address deleted successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Error deleting address",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("addresses/set-default")]
        [Authorize]
        public async Task<IActionResult> SetDefaultAddress([FromBody] SetDefaultAddressDto setDefaultDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid input data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            try
            {
                var result = await _addressService.SetDefaultAddressAsync(setDefaultDto.AddressId, userId);
                if (!result)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Address not found"
                    });
                }

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Default address set successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Error setting default address",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}

