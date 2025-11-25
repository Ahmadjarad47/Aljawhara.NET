using System.ComponentModel.DataAnnotations;
using Ecom.Application.DTOs.Auth;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Diagnostics;

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
        private readonly IEmailService _emailService;

        public AuthController(
            IUserManagerService userManagerService, 
            IJwtService jwtService, 
            IAddressService addressService,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _userManagerService = userManagerService;
            _jwtService = jwtService;
            _addressService = addressService;
            _configuration = configuration;
            _emailService = emailService;
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
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenDays"] ?? "7"));

            // Persist refresh token to user
            await _userManagerService.SetRefreshTokenAsync(result.User.Id, refreshToken, refreshTokenExpiresAt);

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

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
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

            var user = await _userManagerService.GetUserDetailsAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            return Ok(new ApiResponseDto<UserResponseDto>
            {
                Success = true,
                Message = "User details retrieved successfully",
                Data = user
            });
        }

        [HttpPut("me/username")]
        [Authorize]
        public async Task<IActionResult> UpdateUsername([FromBody] UpdateUsernameDto dto)
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

            var result = await _userManagerService.UpdateUsernameAsync(userId, dto.Username);
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

        [HttpPut("me/phone")]
        [Authorize]
        public async Task<IActionResult> UpdatePhoneNumber([FromBody] UpdatePhoneNumberDto dto)
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

            var result = await _userManagerService.UpdatePhoneNumberAsync(userId, dto.PhoneNumber);
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

            // Validate refresh token format early
            if (!_jwtService.ValidateRefreshTokenFormat(refreshTokenDto.RefreshToken))
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid refresh token format"
                });
            }

            // Lookup user by valid refresh token
            var userId = await _userManagerService.GetUserByRefreshTokenAsync(refreshTokenDto.RefreshToken);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not authenticated"
                });
            }

            // Optionally validate format of refresh token (basic sanity check)
            if (string.IsNullOrWhiteSpace(refreshTokenDto.RefreshToken))
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "Refresh token is required"
                });
            }

            var newAccessToken = await _jwtService.GenerateTokenForUserIdAsync(userId);
            if (string.IsNullOrEmpty(newAccessToken))
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var newRefreshToken = _jwtService.GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60"));
            var newRefreshTokenExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenDays"] ?? "7"));

            // Rotate and persist new refresh token
            await _userManagerService.SetRefreshTokenAsync(userId, newRefreshToken, newRefreshTokenExpiresAt);

            var user = await _userManagerService.GetUserDetailsAsync(userId);
            if (user == null)
            {
                return Unauthorized(new ApiResponseDto
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var response = new LoginResponseDto
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiresAt,
                User = user
            };

            return Ok(new ApiResponseDto<LoginResponseDto>
            {
                Success = true,
                Message = "Token refreshed successfully",
                Data = response
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

        //[HttpPost("addresses/set-default")]
        //[Authorize]
        //public async Task<IActionResult> SetDefaultAddress([FromBody] SetDefaultAddressDto setDefaultDto)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(new ApiResponseDto
        //        {
        //            Success = false,
        //            Message = "Invalid input data",
        //            Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
        //        });
        //    }

        //    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        return Unauthorized(new ApiResponseDto
        //        {
        //            Success = false,
        //            Message = "User not authenticated"
        //        });
        //    }

        //    try
        //    {
        //        var result = await _addressService.SetDefaultAddressAsync(setDefaultDto.AddressId, userId);
        //        if (!result)
        //        {
        //            return NotFound(new ApiResponseDto
        //            {
        //                Success = false,
        //                Message = "Address not found"
        //            });
        //        }

        //        return Ok(new ApiResponseDto
        //        {
        //            Success = true,
        //            Message = "Default address set successfully"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new ApiResponseDto
        //        {
        //            Success = false,
        //            Message = "Error setting default address",
        //            Errors = new List<string> { ex.Message }
        //        });
        //    }
        //}

        [HttpPost("contact")]
        public async Task<IActionResult> Contact([FromBody] ContactDto contactDto)
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

            try
            {
                var result = await _emailService.SendContactEmailAsync(
                    contactDto.Name,
                    contactDto.Email,
                    contactDto.PhoneNumber,
                    contactDto.Subject,
                    contactDto.Message
                );

                if (!result)
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Failed to send contact email. Please try again later."
                    });
                }

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Your message has been sent successfully. We will get back to you soon."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponseDto
                {
                    Success = false,
                    Message = "An error occurred while sending your message.",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}

