using Ecom.Infrastructure.Data;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

public class ProductionSeedingTests
{
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Testing", true)]
    [InlineData("Preview", true)]
    [InlineData("Development", null)]
    [InlineData("Development", false)]
    public async Task Disabled_startup_does_not_even_resolve_the_fixture_database(string environment, bool? enabled)
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(new TestEnvironment { EnvironmentName = environment })
            .AddSingleton<IConfiguration>(Configuration(enabled)).BuildServiceProvider();
        await DataSeeder.SeedAsync(provider);
    }

    [Fact]
    public async Task Local_hosts_apply_the_fixture_gate_to_their_configured_startup_environment()
    {
        var ct = TestContext.Current.CancellationToken;
        var scenarios = new[] { ("Production", true), ("Staging", true), ("Development", false), ("Development", true) };
        // Startup reads the DB connection from the process environment. Run these hosts
        // serially in the non-parallel collection so other tests cannot observe an override.
        var keys = new[] { "ConnectionStrings__DefaultConnection", "BootstrapAdmin__Email", "BootstrapAdmin__Password",
            "CheckoutSecurity__GuestTokenDerivationKey", "JwtSettings__SecretKey" };
        var saved = keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("BootstrapAdmin__Email", null);
            Environment.SetEnvironmentVariable("BootstrapAdmin__Password", null);
            Environment.SetEnvironmentVariable("CheckoutSecurity__GuestTokenDerivationKey", "test-only-guest-secret-for-isolated-startup");
            Environment.SetEnvironmentVariable("JwtSettings__SecretKey", "test-only-signing-secret-for-isolated-startup");
            foreach (var (name, enabled) in scenarios)
            {
                var connection = Ecom.CheckoutAudit.Tests.PostgresTestDatabase.ConnectionString($"ecom_p2_startup_{Guid.NewGuid():N}");
                Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connection);
                await using var db = new EcomDbContext(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection).Options);
                try
                {
                    using var factory = new WebApplicationFactory<Program>();
                    using var host = factory.WithWebHostBuilder(builder =>
                    {
                        builder.UseEnvironment(name);
                        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["DevelopmentFixtures:Enabled"] = enabled.ToString(),
                            ["DatabaseBackup:Enabled"] = "false",
                            ["Telegram:Enabled"] = "false"
                        }));
                        builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
                    });
                    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
                    Assert.Equal(name == "Development" && enabled ? 10 : 0, await db.Users.CountAsync(ct));
                }
                finally { await db.Database.EnsureDeletedAsync(ct); }
            }
        }
        finally { foreach (var (key, value) in saved) Environment.SetEnvironmentVariable(key, value); }
    }

    [Fact]
    public async Task Opted_in_development_uses_unique_credentials_and_preserves_existing_identity()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = Ecom.CheckoutAudit.Tests.PostgresTestDatabase.ConnectionString($"ecom_p2_fixtures_{Guid.NewGuid():N}");
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<IHostEnvironment>(new TestEnvironment { EnvironmentName = Environments.Development })
            .AddSingleton<IConfiguration>(Configuration(true))
            .AddDbContext<EcomDbContext>(options => options.UseNpgsql(connection));
        var credentials = new FixturePasswordValidator();
        services.AddIdentityCore<AppUsers>().AddRoles<IdentityRole>().AddEntityFrameworkStores<EcomDbContext>();
        services.AddSingleton<IPasswordValidator<AppUsers>>(credentials);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        try
        {
            await db.Database.MigrateAsync(ct);
            var existing = new AppUsers
            {
                Id = "existing-user", Email = "user1@example.com", NormalizedEmail = "USER1@EXAMPLE.COM",
                UserName = "retained-user", NormalizedUserName = "RETAINED-USER",
                PasswordHash = "synthetic-original-hash", SecurityStamp = "original-stamp"
            };
            db.Users.Add(existing);
            await db.SaveChangesAsync(ct);
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roles.CreateAsync(new IdentityRole("User"));

            await DataSeeder.SeedAsync(provider);
            var users = await db.Users.AsNoTracking().ToListAsync(ct);
            Assert.Equal(10, users.Count);
            Assert.Equal(9, credentials.Hashes.Count);
            Assert.Equal(9, credentials.Hashes.Distinct().Count());
            Assert.True(credentials.AllSatisfiedPolicy);
            var preserved = Assert.Single(users, u => u.Id == existing.Id);
            Assert.Equal(existing.PasswordHash, preserved.PasswordHash);
            Assert.Equal(existing.SecurityStamp, preserved.SecurityStamp);
            Assert.Equal(existing.UserName, preserved.UserName);
            await DataSeeder.SeedAsync(provider);
            Assert.Equal(10, await db.Users.CountAsync(ct));
            Assert.Equal(9, credentials.Hashes.Count);
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }

    private sealed class FixturePasswordValidator : IPasswordValidator<AppUsers>
    {
        public List<string> Hashes { get; } = [];
        public bool AllSatisfiedPolicy { get; private set; } = true;
        public Task<IdentityResult> ValidateAsync(UserManager<AppUsers> manager, AppUsers user, string? password)
        {
            AllSatisfiedPolicy &= password is { Length: >= 64 } && password.Any(char.IsUpper) &&
                password.Any(char.IsLower) && password.Any(char.IsDigit) && password.Any(c => !char.IsLetterOrDigit(c));
            Hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password!))));
            return Task.FromResult(IdentityResult.Success);
        }
    }

    private static IConfiguration Configuration(bool? enabled) => new ConfigurationBuilder().AddInMemoryCollection(
        enabled.HasValue ? new Dictionary<string, string?> { ["DevelopmentFixtures:Enabled"] = enabled.ToString() } : []).Build();

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
