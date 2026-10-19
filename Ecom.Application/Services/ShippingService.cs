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
    // Only migrated rules whose governorate could not be determined use this group.
    public const string UnassignedState = "مناطق غير مصنفة";

    public async Task<ShippingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
        => ToDto(await db.ShippingSettings.AsNoTracking().Include(s => s.Governorates).ThenInclude(g => g.Areas).AsSingleQuery()
            .SingleAsync(s => s.Id == 1, cancellationToken));

    public async Task<ShippingSettingsDto> UpdateSettingsAsync(ShippingSettingsDto settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateMoney(settings.DefaultFee, nameof(settings.DefaultFee));
        if (settings.FreeShippingThreshold is { } threshold)
            ValidateMoney(threshold, nameof(settings.FreeShippingThreshold));
        if (settings.Version < 1 || settings.Governorates == null || settings.Governorates.Count > 20)
            throw new ArgumentException("A valid version and at most 20 governorates are required.");

        var groups = new Dictionary<string, ShippingGovernorateDto>(StringComparer.Ordinal);
        foreach (var governorate in settings.Governorates)
        {
            if (governorate == null || string.IsNullOrWhiteSpace(governorate.State) || governorate.State.Length > 100
                || string.IsNullOrEmpty(StateKey(governorate.State)) || governorate.Areas == null || governorate.Areas.Count > 100)
                throw new ArgumentException("Every governorate requires a name of at most 100 characters and at most 100 areas.");
            if (!groups.TryAdd(StateKey(governorate.State), governorate))
                throw new ArgumentException("Governorate names must be unique.");
            var cities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var area in governorate.Areas)
            {
                if (area == null || string.IsNullOrWhiteSpace(area.City) || area.City.Length > 100)
                    throw new ArgumentException("Every area must have a city name of at most 100 characters.");
                ValidateMoney(area.Fee, nameof(area.Fee));
                if (!cities.Add(CityKey(area.City)))
                    throw new ArgumentException("City names must be unique within each governorate.");
            }
        }
        if (groups.Values.Sum(g => g.Areas.Count) > 1000)
            throw new ArgumentException("At most 1000 areas are allowed.");

        var current = await db.ShippingSettings.Include(s => s.Governorates).ThenInclude(g => g.Areas).AsSingleQuery()
            .SingleAsync(s => s.Id == 1, cancellationToken);
        if (current.Version != settings.Version)
            throw new DbUpdateConcurrencyException("Shipping settings changed. Reload before saving.");
        current.DefaultFee = settings.DefaultFee;
        current.FreeShippingThreshold = settings.FreeShippingThreshold;
        current.Version = checked(current.Version + 1);

        foreach (var existing in current.Governorates.ToList())
        {
            if (!groups.ContainsKey(existing.StateKey))
            {
                db.ShippingGovernorates.Remove(existing);
                current.Governorates.Remove(existing);
            }
        }
        foreach (var (stateKey, group) in groups)
        {
            var governorate = current.Governorates.SingleOrDefault(g => g.StateKey == stateKey);
            if (governorate == null)
            {
                governorate = new ShippingGovernorate { ShippingSettingsId = 1, StateKey = stateKey };
                current.Governorates.Add(governorate);
            }
            governorate.State = CleanName(group.State);
            var rates = group.Areas.ToDictionary(a => CityKey(a.City), StringComparer.Ordinal);
            foreach (var existing in governorate.Areas.ToList())
            {
                if (!rates.ContainsKey(existing.CityKey))
                {
                    db.ShippingAreaRates.Remove(existing);
                    governorate.Areas.Remove(existing);
                }
            }
            foreach (var (cityKey, rate) in rates)
            {
                var entity = governorate.Areas.SingleOrDefault(a => a.CityKey == cityKey);
                if (entity == null)
                {
                    entity = new ShippingAreaRate { ShippingSettingsId = 1, StateKey = stateKey, CityKey = cityKey };
                    governorate.Areas.Add(entity);
                }
                entity.City = CleanName(rate.City);
                entity.Fee = rate.Fee;
            }
        }
        // Settings, governorates and areas are saved atomically; Version prevents lost edits.
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(current);
    }

    public async Task<decimal> CalculateAsync(decimal subtotal, CouponType? couponType, string? city, string? state = null, CancellationToken cancellationToken = default)
    {
        if (couponType == CouponType.FreeShipping) return 0m;
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings.FreeShippingThreshold is { } threshold && subtotal >= threshold) return 0m;
        var cityKey = CityKey(city);
        var stateKey = StateKey(state);
        if (stateKey.Length > 0)
        {
            var rate = settings.Governorates.FirstOrDefault(g => StateKey(g.State) == stateKey)
                ?.Areas.FirstOrDefault(a => CityKey(a.City) == cityKey);
            if (rate != null) return rate.Fee;
        }
        else
        {
            // Older addresses can omit state. Use a city-only rate only when it is unambiguous.
            var matches = settings.Governorates.SelectMany(g => g.Areas).Where(a => CityKey(a.City) == cityKey).Take(2).ToList();
            if (matches.Count == 1) return matches[0].Fee;
        }
        return settings.Governorates.FirstOrDefault(g => g.State == UnassignedState)
            ?.Areas.FirstOrDefault(a => CityKey(a.City) == cityKey)?.Fee ?? settings.DefaultFee;
    }

    private static string CleanName(string? value)
        => string.Join(' ', (value ?? string.Empty).Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string CityKey(string? city) => CleanName(city).ToUpperInvariant();

    private static string StateKey(string? state)
    {
        var clean = CleanName(state);
        if (clean == "محافظة") return string.Empty;
        return CityKey(clean.StartsWith("محافظة ", StringComparison.Ordinal) ? clean[7..] : clean);
    }

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
        Governorates = settings.Governorates.OrderBy(g => g.State, StringComparer.Ordinal)
            .Select(g => new ShippingGovernorateDto
            {
                State = g.State,
                Areas = g.Areas.OrderBy(a => a.City, StringComparer.Ordinal)
                    .Select(a => new ShippingAreaRateDto { City = a.City, Fee = a.Fee }).ToList()
            }).ToList()
    };
}
