using System.ComponentModel.DataAnnotations;
using Ecom.Application.DTOs.Auth;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserManagerService _userManagerService;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;

        public AuthController(IUserManagerService userManagerService, IJwtService jwtService, IConfiguration configuration)
        {
            _userManagerService = userManagerService;
            _jwtService = jwtService;
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
        public async Task<ActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _userManagerService.CreateUserAsync(registerDto);
                
                if (result.Success)
                {
                    return Ok(new { Message = result.Message, UserId = result.UserId });
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while registering the user", Error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _userManagerService.ValidateLoginAsync(loginDto);

                if (result.Success && result.User != null)
                {
                    var token = _jwtService.GenerateToken(result.User);
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
                            Id = result.User.Id,
                            Username = result.User.UserName ?? string.Empty,
                            Email = result.User.Email ?? string.Empty,
                            PhoneNumber = result.User.PhoneNumber ?? string.Empty,
                            EmailConfirmed = result.User.EmailConfirmed
                        }
                    };

                    return Ok(response);
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while logging in", Error = ex.Message });
            }
        }

        [HttpPost("confirm-email")]
        public async Task<ActionResult> ConfirmEmail([FromBody] ConfirmEmailDto confirmEmailDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _userManagerService.ConfirmUserEmailAsync(confirmEmailDto.UserId, confirmEmailDto.Token);
                if (result)
                {
                    return Ok(new { Message = "Email confirmed successfully. You can now log in." });
                }
                return BadRequest("Invalid or expired confirmation token");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while confirming email", Error = ex.Message });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            // With JWT, logout is typically handled client-side by removing the token
            // Server-side logout would require token blacklisting which is more complex
            return Ok(new { Message = "Logout successful" });
        }

        [HttpPost("refresh-token")]
        [Authorize]
        public async Task<ActionResult<LoginResponseDto>> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // In a real application, you would validate the refresh token against a database
                // For now, we'll generate a new token (this is a simplified implementation)
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid refresh token");
                }

                // Get user from UserManagerService
                var userDto = await _userManagerService.GetUserByIdAsync(userId);
                if (userDto == null)
                {
                    return Unauthorized("User not found");
                }

                // Check if user is still active
                if (userDto.IsBlocked)
                {
                    return Unauthorized("Account is blocked");
                }

                // Create a temporary user object for token generation
                var user = new AppUsers
                {
                    Id = userDto.Id,
                    UserName = userDto.Username,
                    Email = userDto.Email,
                    PhoneNumber = userDto.PhoneNumber,
                    EmailConfirmed = userDto.EmailConfirmed
                };

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
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while refreshing token", Error = ex.Message });
            }
        }

        [HttpPost("send-otp")]
        public async Task<ActionResult> SendOtp([FromBody] SendOtpDto sendOtpDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Basic email validation
            if (string.IsNullOrWhiteSpace(sendOtpDto.Email) || !sendOtpDto.Email.Contains("@"))
            {
                return BadRequest("Please provide a valid email address");
            }

            try
            {
                var result = await _userManagerService.SendOtpAsync(sendOtpDto);
                if (result)
                {
                    return Ok(new { Message = "OTP sent successfully to your email" });
                }
                return BadRequest("Failed to send OTP. Please check your email address.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while sending OTP", Error = ex.Message });
            }
        }

        [HttpPost("verify-otp")]
        public async Task<ActionResult> VerifyOtp([FromBody] VerifyOtpDto verifyOtpDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Basic validation
            if (string.IsNullOrWhiteSpace(verifyOtpDto.Email) || !verifyOtpDto.Email.Contains("@"))
            {
                return BadRequest("Please provide a valid email address");
            }

            if (string.IsNullOrWhiteSpace(verifyOtpDto.Otp) || verifyOtpDto.Otp.Length != 6)
            {
                return BadRequest("Please provide a valid 6-digit OTP");
            }

            try
            {
                var result = await _userManagerService.VerifyOtpAsync(verifyOtpDto);
                if (result)
                {
                    return Ok(new { Message = "OTP verified successfully" });
                }
                return BadRequest("Invalid OTP or OTP has expired");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while verifying OTP", Error = ex.Message });
            }
        }

        [HttpPost("resend-otp")]
        public async Task<ActionResult> ResendOtp([FromBody] SendOtpDto sendOtpDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Basic email validation
            if (string.IsNullOrWhiteSpace(sendOtpDto.Email) || !sendOtpDto.Email.Contains("@"))
            {
                return BadRequest("Please provide a valid email address");
            }

            try
            {
                var result = await _userManagerService.SendOtpAsync(sendOtpDto);
                if (result)
                {
                    return Ok(new { Message = "OTP resent successfully to your email" });
                }
                return BadRequest("Failed to resend OTP. Please check your email address.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while resending OTP", Error = ex.Message });
            }
        }

        [HttpPost("change-password")]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Basic validation
            if (string.IsNullOrWhiteSpace(changePasswordDto.Email) || !changePasswordDto.Email.Contains("@"))
            {
                return BadRequest("Please provide a valid email address");
            }

            if (string.IsNullOrWhiteSpace(changePasswordDto.Otp) || changePasswordDto.Otp.Length != 6)
            {
                return BadRequest("Please provide a valid 6-digit OTP");
            }

            if (string.IsNullOrWhiteSpace(changePasswordDto.NewPassword) || changePasswordDto.NewPassword.Length < 6)
            {
                return BadRequest("Password must be at least 6 characters long");
            }

            try
            {
                var result = await _userManagerService.ChangePasswordWithOtpAsync(changePasswordDto);
                if (result)
                {
                    return Ok(new { Message = "Password changed successfully" });
                }
                return BadRequest("Failed to change password. Please verify your OTP and try again.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while changing password", Error = ex.Message });
            }
        }
    }

}

