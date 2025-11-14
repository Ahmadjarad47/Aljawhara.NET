using Ecom.Application.DTOs.Auth;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/user-manager")]
    [Authorize(Roles = "Admin")]
    public class AdminUserManagerController : ControllerBase
    {
        private readonly IUserManagerService _userManagerService;
        private readonly IAddressService addressService;
        private readonly ILogger<AdminUserManagerController> _logger;

        public AdminUserManagerController(
            IUserManagerService userManagerService,
            ILogger<AdminUserManagerController> logger,
            IAddressService addressService)
        {
            _userManagerService = userManagerService;
            _logger = logger;
            this.addressService = addressService;
        }

        /// <summary>
        /// Get all users with search and filter options
        /// </summary>
        [HttpGet("users")]
        public async Task<ActionResult> GetUsers([FromQuery] UserSearchDto searchDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                Application.DTOs.Common.PagedResult<UserManagerDto>? result = await _userManagerService.GetUsersAsync(searchDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new { Message = "An error occurred while retrieving users", Error = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific user by ID
        /// </summary>
        [HttpGet("users/{userId}")]
        public async Task<ActionResult> GetUser(string userId)
        {
            try
            {
                var user = await _userManagerService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving the user", Error = ex.Message });
            }
        }

        /// <summary>
        /// Block a user
        /// </summary>
        [HttpPost("users/block")]
        public async Task<ActionResult> BlockUser([FromBody] BlockUserDto blockUserDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userManagerService.BlockUserAsync(blockUserDto);
                if (!result)
                {
                    return BadRequest(new { Message = "Failed to block user. User may not exist." });
                }

                return Ok(new { Message = "User blocked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user: {UserId}", blockUserDto.UserId);
                return StatusCode(500, new { Message = "An error occurred while blocking the user", Error = ex.Message });
            }
        }

        /// <summary>
        /// Unblock a user
        /// </summary>
        [HttpPost("users/unblock")]
        public async Task<ActionResult> UnblockUser([FromBody] UnblockUserDto unblockUserDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userManagerService.UnblockUserAsync(unblockUserDto);
                if (!result)
                {
                    return BadRequest(new { Message = "Failed to unblock user. User may not exist." });
                }

                return Ok(new { Message = "User unblocked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking user: {UserId}", unblockUserDto.UserId);
                return StatusCode(500, new { Message = "An error occurred while unblocking the user", Error = ex.Message });
            }
        }

        /// <summary>
        /// Change a user's password
        /// </summary>
        [HttpPost("users/change-password")]
        public async Task<ActionResult> ChangeUserPassword([FromBody] ChangeUserPasswordDto changePasswordDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userManagerService.ChangeUserPasswordAsync(changePasswordDto);
                if (!result)
                {
                    return BadRequest(new { Message = "Failed to change user password. User may not exist." });
                }

                return Ok(new { Message = "User password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", changePasswordDto.UserId);
                return StatusCode(500, new { Message = "An error occurred while changing the user password", Error = ex.Message });
            }
        }

        /// <summary>
        /// Change a user's email (sends confirmation email)
        /// </summary>
        [HttpPost("users/change-email")]
        public async Task<ActionResult> ChangeUserEmail([FromBody] ChangeUserEmailDto changeEmailDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userManagerService.ChangeUserEmailAsync(changeEmailDto);
                if (!result)
                {
                    return BadRequest(new { Message = "Failed to initiate email change. User may not exist." });
                }

                return Ok(new { Message = "Email change confirmation sent to the new email address" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing email for user: {UserId}", changeEmailDto.UserId);
                return StatusCode(500, new { Message = "An error occurred while changing the user email", Error = ex.Message });
            }
        }

        /// <summary>
        /// Send email confirmation to a user
        /// </summary>
        [HttpPost("users/send-email-confirmation")]
        public async Task<ActionResult> SendEmailConfirmation([FromBody] SendEmailConfirmationDto sendEmailDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userManagerService.SendEmailConfirmationAsync(sendEmailDto);
                if (!result)
                {
                    return BadRequest(new { Message = "Failed to send email confirmation. User may not exist or email may already be confirmed." });
                }

                return Ok(new { Message = "Email confirmation sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email confirmation to user: {UserId}", sendEmailDto.UserId);
                return StatusCode(500, new { Message = "An error occurred while sending email confirmation", Error = ex.Message });
            }
        }

        /// <summary>
        /// Confirm a user's email (for testing purposes - normally handled by frontend)
        /// </summary>
        [HttpPost("users/{userId}/confirm-email")]
        public async Task<ActionResult> ConfirmUserEmail(string userId, [FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { Message = "Token is required" });
                }

                var result = await _userManagerService.ConfirmUserEmailAsync(userId, token);
                if (!result)
                {
                    return BadRequest(new { Message = "Failed to confirm email. Invalid token or user not found." });
                }

                return Ok(new { Message = "Email confirmed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming email for user: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while confirming the email", Error = ex.Message });
            }
        }

        /// <summary>
        /// Reset a user's password (admin can reset without knowing current password)
        /// </summary>
        [HttpPost("users/{userId}/reset-password")]
        public async Task<ActionResult> ResetUserPassword(string userId, [FromBody] string newPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
                {
                    return BadRequest(new { Message = "Password must be at least 6 characters long" });
                }

                var result = await _userManagerService.ResetUserPasswordAsync(userId, newPassword);
                if (!result)
                {
                    return BadRequest(new { Message = "Failed to reset password. User may not exist." });
                }

                return Ok(new { Message = "Password reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while resetting the password", Error = ex.Message });
            }


        }
        // Address Management Endpoints
        [HttpGet("addresses")]
        public async Task<IActionResult> GetUserAddresses([FromQuery]string userId)
        {
          

            try
            {
                var addresses = await addressService.GetUserAddressesAsync(userId);
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
    }
}
