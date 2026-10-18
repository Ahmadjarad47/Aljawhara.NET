using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecom.CheckoutAudit.Tests;

public class SqlServerIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"EcomCheckoutAudit_{Guid.NewGuid():N}";
    private string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Database_transaction_rollback_removes_all_test_changes()
    {
        await using (var context = CreateContext())
        await using (var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            context.Categories.Add(new Category { Name = "Rollback Synthetic", NameAr = "اختبار" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        await using var verification = CreateContext();
        Assert.False(await verification.Categories.AnyAsync(category => category.Name == "Rollback Synthetic", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Kuwaiti_three_decimal_amount_is_rounded_by_two_decimal_database_schema()
    {
        var seeded = await SeedOrderAsync(12.345m);
        await using (var context = CreateContext())
        {
            context.Transactions.Add(new Transaction
            {
                OrderId = seeded.OrderId,
                Amount = 12.345m,
                Status = Ecom.Domain.constant.TransactionStatus.Pending,
                PaymentMethod = Ecom.Domain.constant.PaymentMethod.Bank
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verification = CreateContext();
        var amount = await verification.Transactions.Select(transaction => transaction.Amount).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(12.35m, amount);
    }

    [Fact]
    public async Task Two_buyers_can_both_commit_the_same_final_stock_unit()
    {
        var seeded = await SeedOrderAsync(1m);
        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstView = await first.Products.SingleAsync(product => product.Id == seeded.ProductId, TestContext.Current.CancellationToken);
        var secondView = await second.Products.SingleAsync(product => product.Id == seeded.ProductId, TestContext.Current.CancellationToken);
        Assert.Equal(1, firstView.TotalInStock);
        Assert.Equal(1, secondView.TotalInStock);

        firstView.TotalInStock -= 1;
        secondView.TotalInStock -= 1;
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await second.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var verification = CreateContext();
        Assert.Equal(0, await verification.Products.Where(product => product.Id == seeded.ProductId).Select(product => product.TotalInStock).SingleAsync(TestContext.Current.CancellationToken));
    }

    private EcomDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EcomDbContext>()
            .UseSqlServer(ConnectionString)
            .EnableSensitiveDataLogging(false)
            .Options;
        return new EcomDbContext(options);
    }

    private async Task<(int ProductId, int OrderId)> SeedOrderAsync(decimal total)
    {
        await using var context = CreateContext();
        var category = new Category { Name = $"Synthetic Category {Guid.NewGuid():N}", NameAr = "اختبار" };
        var subCategory = new SubCategory { Name = $"Synthetic Subcategory {Guid.NewGuid():N}", NameAr = "اختبار", Category = category };
        var product = new Product
        {
            Title = $"Synthetic Product {Guid.NewGuid():N}",
            TitleAr = "اختبار",
            Description = "Synthetic",
            DescriptionAr = "اختبار",
            SubCategoryId = 0,
            subCategory = subCategory,
            oldPrice = total,
            newPrice = total,
            TotalInStock = 1,
            IsInStock = true
        };
        var address = new ShippingAddress
        {
            FullName = "Synthetic Guest",
            Phone = "00000000",
            Street = "Synthetic Street"
        };
        var order = new Order
        {
            OrderNumber = $"AUDIT-{Guid.NewGuid():N}",
            ShippingAddress = address,
            Total = total,
            Subtotal = total
        };
        context.AddRange(category, subCategory, product, address, order);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (product.Id, order.Id);
    }
}
