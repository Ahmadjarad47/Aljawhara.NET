using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Order;

public class ShippingSettingsDto
{
    public string Currency => "KWD";
    public required decimal DefaultFee { get; set; }
    // Null disables the order-value offer; zero makes all orders eligible.
    public required decimal? FreeShippingThreshold { get; set; }
    [Range(1, long.MaxValue)]
    public required long Version { get; set; }
    [Required, MaxLength(20)]
    public required List<ShippingGovernorateDto> Governorates { get; set; }
}

public class ShippingGovernorateDto
{
    [Required, StringLength(100)]
    public required string State { get; set; }
    [Required, MaxLength(100)]
    public required List<ShippingAreaRateDto> Areas { get; set; }
}

public class ShippingAreaRateDto
{
    [Required, StringLength(100)]
    public required string City { get; set; }
    public required decimal Fee { get; set; }
}
