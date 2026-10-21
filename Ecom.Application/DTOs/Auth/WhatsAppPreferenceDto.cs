using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Auth;

public sealed class WhatsAppPreferenceDto
{
    public bool Enabled { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public DateTime? ConsentAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

public sealed class UpdateWhatsAppPreferenceDto
{
    public bool Enabled { get; set; }

    [StringLength(40)]
    public string? PhoneNumber { get; set; }
}

public sealed class RequestWhatsAppPhoneVerificationDto
{
    [Required, StringLength(40)]
    public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class ConfirmWhatsAppPhoneVerificationDto
{
    [Required, StringLength(40)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{6}$")]
    public string Code { get; set; } = string.Empty;
}
