using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Auth
{
    public class UserManagerDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public int AccessFailedCount { get; set; }
        public bool TwoFactorEnabled { get; set; }
    }

    public class BlockUserDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        public DateTime? BlockUntil { get; set; }
        
        public string Reason { get; set; } = string.Empty;
    }

    public class UnblockUserDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }

    public class ChangeUserPasswordDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangeUserEmailDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string NewEmail { get; set; } = string.Empty;
    }

    public class SendEmailConfirmationDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }

    public class UserSearchDto
    {
        public string? SearchTerm { get; set; }
        public bool? IsBlocked { get; set; }
        public bool? EmailConfirmed { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
