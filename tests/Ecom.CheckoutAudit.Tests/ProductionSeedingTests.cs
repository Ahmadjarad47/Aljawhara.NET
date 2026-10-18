using Ecom.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

public class ProductionSeedingTests
{
    [Fact]
    public async Task Production_does_not_resolve_database_or_create_demo_data()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(new ProductionEnvironment()).BuildServiceProvider();
        await DataSeeder.SeedAsync(provider);
    }
    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
