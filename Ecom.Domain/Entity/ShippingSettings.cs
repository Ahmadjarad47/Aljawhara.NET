namespace Ecom.Domain.Entity;

public class ShippingSettings
{
    public int Id { get; set; }
    public decimal DefaultFee { get; set; }
    public decimal? FreeShippingThreshold { get; set; }
    public long Version { get; set; }
    public List<ShippingGovernorate> Governorates { get; set; } = [];
}

public class ShippingGovernorate
{
    public int ShippingSettingsId { get; set; }
    public string StateKey { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public List<ShippingAreaRate> Areas { get; set; } = [];
}

public class ShippingAreaRate
{
    public int ShippingSettingsId { get; set; }
    public string StateKey { get; set; } = string.Empty;
    public string CityKey { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal Fee { get; set; }
}
