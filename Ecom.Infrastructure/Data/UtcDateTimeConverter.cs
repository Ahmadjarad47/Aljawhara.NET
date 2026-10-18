using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ecom.Infrastructure.Data;

// Preserve the API's existing convention: dates without an explicit offset mean UTC.
// PostgreSQL timestamptz requires UTC; local values are converted before storage.
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime()
        : DateTime.SpecifyKind(value, DateTimeKind.Utc),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
