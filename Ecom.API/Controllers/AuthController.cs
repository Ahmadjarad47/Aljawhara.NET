using Ecom.Application.DTOs.Auth;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Ecom.API.Controllers;

public class SadadRefreshResponse
{
    public bool isValid { get; set; }
    public string errorKey { get; set; }
    public SadadRefreshData response { get; set; }
}
public class SadadAccessResponse
{
    public bool isValid { get; set; }
    public string errorKey { get; set; }
    public SadadAccessData response { get; set; }
}

public class SadadAccessData
{
    public string accessToken { get; set; }
    public int expiredAfterSeconds { get; set; }
}


public class SadadRefreshData
{
    public string refreshToken { get; set; }
}
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserManagerService _userManagerService;
    private readonly AuthSessionService _sessions;
    private readonly IAddressService _addressService;
    private readonly IEmailService _emailService;

    public AuthController(
        IUserManagerService userManagerService,
        AuthSessionService sessions,
        IAddressService addressService,
        IEmailService emailService)
    {
        _userManagerService = userManagerService;
        _sessions = sessions;
        _addressService = addressService;
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
    [EnableRateLimiting("auth-issue")]
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
            return BadRequest(new AccountValidationResponseDto
            {
                Success = false,
                Message = result.Message,
                FieldErrors = result.FieldErrors
            });
        }

        return Ok(new ApiResponseDto
        {
            Success = true,
            Message = result.Message
        });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
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

        var loginResponse = await _sessions.StartAsync(result.User, HttpContext.RequestAborted);
        if (loginResponse == null)
            return Unauthorized(new ApiResponseDto { Success = false, Message = "Invalid email or password" });

        return Ok(new ApiResponseDto<LoginResponseDto>
        {
            Success = true,
            Message = result.Message,
            Data = loginResponse
        });
    }

    [HttpPost("verify")]
    [EnableRateLimiting("auth-verify")]
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
    [EnableRateLimiting("auth-issue")]
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
    [EnableRateLimiting("auth-verify")]
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
    [EnableRateLimiting("auth-issue")]
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
    [EnableRateLimiting("auth-verify")]
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

        if (!Guid.TryParse(User.FindFirstValue(JwtService.SessionClaim), out var sessionId))
            return Unauthorized();

        await _userManagerService.LogoutAsync(userId, sessionId, HttpContext.RequestAborted);
        return Ok(new ApiResponseDto { Success = true, Message = "Logged out successfully" });
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
            return BadRequest(new AccountValidationResponseDto
            {
                Success = false,
                Message = result.Message,
                FieldErrors = result.ErrorCode is { } code ? new() { ["username"] = [code] } : null
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
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        var response = await _sessions.RotateAsync(dto.RefreshToken, HttpContext.RequestAborted);
        if (response == null)
            return Unauthorized(new ApiResponseDto { Success = false, Message = "Invalid or expired refresh token" });

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
                Data = addresses.ToList()
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
    [EnableRateLimiting("contact")]
    [RequestSizeLimit(32 * 1024)]
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
        catch (Exception)
        {
            return BadRequest(new ApiResponseDto
            {
                Success = false,
                Message = "An error occurred while sending your message."
            });
        }
    }
}

