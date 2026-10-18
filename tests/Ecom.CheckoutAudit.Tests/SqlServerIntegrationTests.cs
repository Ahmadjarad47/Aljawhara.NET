using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Ecom.Infrastructure.Repositories;

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
    public async Task Kuwaiti_three_decimal_amount_is_stored_without_precision_loss()
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
        Assert.Equal(12.345m, amount);
    }

    [Fact]
    public async Task Twenty_concurrent_stock_decrements_allow_only_one_buyer_for_the_final_unit()
    {
        var seeded = await SeedOrderAsync(1m);
        var contexts = Enumerable.Range(0, 20).Select(_ => CreateContext()).ToList();
        var caches = Enumerable.Range(0, 20).Select(_ => new MemoryCache(new MemoryCacheOptions())).ToList();
        try
        {
            var attempts = await Task.WhenAll(contexts.Select((context, index) =>
                new ProductRepository(context, caches[index]).TryReduceStockAsync(seeded.ProductId, 1)));

            await using var verification = CreateContext();
            Assert.Single(attempts, succeeded => succeeded);
            Assert.Equal(0, await verification.Products.Where(product => product.Id == seeded.ProductId).Select(product => product.TotalInStock).SingleAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            foreach (var context in contexts)
                await context.DisposeAsync();
            foreach (var cache in caches)
                cache.Dispose();
        }
    }

    [Fact]
    public async Task Concurrent_coupon_claims_cannot_exceed_the_final_usage_limit()
    {
        int couponId;
        await using (var seed = CreateContext())
        {
            var coupon = new Coupon { Code = $"FINAL-{Guid.NewGuid():N}", Description = "Final use", Type = Ecom.Domain.constant.CouponType.FixedAmount, Value = 1m, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1), UsageLimit = 1 };
            seed.Coupons.Add(coupon);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            couponId = coupon.Id;
        }
        var contexts = Enumerable.Range(0, 20).Select(_ => CreateContext()).ToList();
        var caches = Enumerable.Range(0, 20).Select(_ => new MemoryCache(new MemoryCacheOptions())).ToList();
        try
        {
            var results = await Task.WhenAll(contexts.Select((db, index) => new CouponRepository(db, caches[index]).TryReserveUsageAsync(couponId, DateTime.UtcNow)));
            Assert.Single(results, result => result);
            await using var verification = CreateContext();
            Assert.Equal(1, await verification.Coupons.Where(c => c.Id == couponId).Select(c => c.UsedCount).SingleAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            foreach (var db in contexts) await db.DisposeAsync();
            foreach (var cache in caches) cache.Dispose();
        }
    }

    [Fact]
    public async Task Database_constraints_reject_non_positive_quantity_and_negative_stock()
    {
        var seeded = await SeedOrderAsync(1m);
        await using var context = CreateContext();
        context.OrderItems.Add(new OrderItem
        {
            OrderId = seeded.OrderId,
            ProductId = seeded.ProductId,
            Name = "Invalid",
            Price = 1m,
            Quantity = 0
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        context.ChangeTracker.Clear();

        var product = await context.Products.SingleAsync(item => item.Id == seeded.ProductId, TestContext.Current.CancellationToken);
        product.TotalInStock = -1;
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
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
