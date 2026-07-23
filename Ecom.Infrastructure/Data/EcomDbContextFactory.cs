using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace Ecom.Infrastructure.Data
{
    public class EcomDbContextFactory : IDesignTimeDbContextFactory<EcomDbContext>
    {
        public EcomDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            var apiSettingsPath = Path.Combine(basePath, "..", "Ecom.API");

            if (!Directory.Exists(apiSettingsPath))
            {
                apiSettingsPath = Path.Combine(basePath, "Ecom.API");
            }

            var settingsPath = Directory.Exists(apiSettingsPath) ? apiSettingsPath : basePath;
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? ReadConnectionString(Path.Combine(settingsPath, "appsettings.Development.json"))
                ?? ReadConnectionString(Path.Combine(settingsPath, "appsettings.json"));

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<EcomDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new EcomDbContext(optionsBuilder.Options);
        }

        private static string? ReadConnectionString(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                && connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
            {
                return defaultConnection.GetString();
            }

            return null;
        }
    }
}
