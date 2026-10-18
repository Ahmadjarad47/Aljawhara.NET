using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ecom.Infrastructure.Data
{
    public class EcomDbContextFactory : IDesignTimeDbContextFactory<EcomDbContext>
    {
        public EcomDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection is not configured. Set the ConnectionStrings__DefaultConnection environment variable.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<EcomDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new EcomDbContext(optionsBuilder.Options);
        }

    }
}
