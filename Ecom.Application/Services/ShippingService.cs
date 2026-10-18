using System.Text;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Payments;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Application.Services;

public class ShippingService(EcomDbContext db) : IShippingService
{
    public async Task<ShippingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
        => ToDto(await db.ShippingSettings.AsNoTracking().Include(s => s.Areas).AsSingleQuery()
            .SingleAsync(s => s.Id == 1, cancellationToken));

    public async Task<ShippingSettingsDto> UpdateSettingsAsync(ShippingSettingsDto settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateMoney(settings.DefaultFee, nameof(settings.DefaultFee));
        if (settings.FreeShippingThreshold is { } threshold)
            ValidateMoney(threshold, nameof(settings.FreeShippingThreshold));
        if (settings.Version < 1 || settings.Areas == null || settings.Areas.Count > 100)
            throw new ArgumentException("A valid version and at most 100 areas are required.");

        var rates = new Dictionary<string, ShippingAreaRateDto>(StringComparer.Ordinal);
        foreach (var area in settings.Areas)
        {
            if (area == null || string.IsNullOrWhiteSpace(area.City) || area.City.Length > 100)
                throw new ArgumentException("Every area must have a city name of at most 100 characters.");
            ValidateMoney(area.Fee, nameof(area.Fee));
            if (!rates.TryAdd(NormalizeCity(area.City), area))
                throw new ArgumentException("City names must be unique, ignoring case and extra whitespace.");
        }

        var current = await db.ShippingSettings.Include(s => s.Areas).AsSingleQuery()
            .SingleAsync(s => s.Id == 1, cancellationToken);
        if (current.Version != settings.Version)
            throw new DbUpdateConcurrencyException("Shipping settings changed. Reload before saving.");
        current.DefaultFee = settings.DefaultFee;
        current.FreeShippingThreshold = settings.FreeShippingThreshold;
        current.Version = checked(current.Version + 1);

        foreach (var existing in current.Areas.ToList())
        {
            if (!rates.ContainsKey(existing.CityKey))
            {
                db.ShippingAreaRates.Remove(existing);
                current.Areas.Remove(existing);
            }
        }
        foreach (var (key, rate) in rates)
        {
            var entity = current.Areas.SingleOrDefault(a => a.CityKey == key);
            if (entity == null)
            {
                entity = new ShippingAreaRate { ShippingSettingsId = 1, CityKey = key };
                current.Areas.Add(entity);
            }
            entity.City = CleanCity(rate.City);
            entity.Fee = rate.Fee;
        }
        // EF saves the settings and all area changes in one transaction. Version prevents lost edits.
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(current);
    }

    public async Task<decimal> CalculateAsync(decimal subtotal, CouponType? couponType, string? city, CancellationToken cancellationToken = default)
    {
        if (couponType == CouponType.FreeShipping) return 0m;
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings.FreeShippingThreshold is { } threshold && subtotal >= threshold) return 0m;
        var cityKey = NormalizeCity(city);
        return settings.Areas.FirstOrDefault(a => NormalizeCity(a.City) == cityKey)?.Fee ?? settings.DefaultFee;
    }

    private static string CleanCity(string? city)
        => string.Join(' ', (city ?? string.Empty).Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeCity(string? city) => CleanCity(city).ToUpperInvariant();

    private static void ValidateMoney(decimal value, string field)
    {
        if (value < 0 || value > 1_000_000m || KwdMoney.Round(value) != value)
            throw new ArgumentException($"{field} must be between 0 and 1000000 KWD with at most three decimal places.");
    }

    private static ShippingSettingsDto ToDto(ShippingSettings settings) => new()
    {
        DefaultFee = settings.DefaultFee,
        FreeShippingThreshold = settings.FreeShippingThreshold,
        Version = settings.Version,
        Areas = settings.Areas.OrderBy(a => a.City, StringComparer.Ordinal)
            .Select(a => new ShippingAreaRateDto { City = a.City, Fee = a.Fee }).ToList()
    };
}
