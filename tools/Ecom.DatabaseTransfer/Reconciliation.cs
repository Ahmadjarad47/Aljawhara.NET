using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class Reconciliation
{
    public static void Append(IncrementalHash hash, object? value)
    {
        var text = value is null or DBNull ? "N" : "V" + (value switch {
            decimal n => n.ToString("G29", CultureInfo.InvariantCulture),
            DateTime d => d.Ticks.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset d => d.UtcTicks.ToString(CultureInfo.InvariantCulture),
            byte[] b => Convert.ToBase64String(b),
            string[] a => JsonSerializer.Serialize(a),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }
}
