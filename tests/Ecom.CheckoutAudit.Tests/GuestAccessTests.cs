using System.Security.Cryptography;
using System.Text;
using Ecom.Application.Services;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Repositories.Interfaces;
using Ecom.Infrastructure.UnitOfWork;

namespace Ecom.CheckoutAudit.Tests;

public class GuestAccessTests
{
    [Fact]
    public async Task Guest_access_is_order_scoped_hashed_and_constant_time_compared()
    {
        const string secret = "guest-secret-with-high-entropy-in-production";
        var guest = new Order
        {
            Id = 10,
            GuestAccessTokenHash = Hash(secret),
            GuestAccessExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };
        var registered = new Order { Id = 11, AppUserId = "user-b" };
        var service = ServiceFor(guest, registered);

        Assert.True(await service.CanAccessOrderAsync(10, null, secret));
        Assert.False(await service.CanAccessOrderAsync(10, null, "wrong-secret"));
        Assert.False(await service.CanAccessOrderAsync(11, "user-a", secret));
        Assert.True(await service.CanAccessOrderAsync(11, "user-b", null));
    }

    [Fact]
    public async Task Expired_or_revoked_guest_secret_is_rejected()
    {
        const string secret = "expired-secret";
        var expired = new Order
        {
            Id = 20,
            GuestAccessTokenHash = Hash(secret),
            GuestAccessExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1)
        };
        var revoked = new Order
        {
            Id = 21,
            GuestAccessTokenHash = Hash(secret),
            GuestAccessExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            GuestAccessRevokedAtUtc = DateTime.UtcNow
        };
        var service = ServiceFor(expired, revoked);

        Assert.False(await service.CanAccessOrderAsync(20, null, secret));
        Assert.False(await service.CanAccessOrderAsync(21, null, secret));
    }

    [Fact]
    public async Task Rotation_persists_only_a_new_hash_and_expiration()
    {
        var saves = 0;
        var guest = new Order { Id = 30 };
        var orders = InterfaceStub.Create<IOrderRepository>((method, args) => method.Name switch
        {
            "FirstOrDefaultAsync" => Task.FromResult<Order?>(guest),
            "Update" => null,
            "GetByIdAsync" => Task.FromResult<Order?>(guest),
            _ => throw new InvalidOperationException($"Unexpected order call: {method.Name}")
        });
        var uow = InterfaceStub.Create<IUnitOfWork>((method, _) => method.Name switch
        {
            "get_Orders" => orders,
            "SaveChangesAsync" => Task.FromResult(++saves),
            "Dispose" => null,
            _ => throw new InvalidOperationException($"Unexpected unit of work call: {method.Name}")
        });
        var service = new OrderService(uow, TestMapper.Create(), null!, null!, null!);

        var rawToken = await service.RotateGuestAccessTokenAsync(guest.Id);

        Assert.NotNull(rawToken);
        Assert.NotEqual(rawToken, guest.GuestAccessTokenHash);
        Assert.Equal(64, guest.GuestAccessTokenHash!.Length);
        Assert.True(guest.GuestAccessExpiresAtUtc > DateTime.UtcNow.AddDays(29));
        Assert.Equal(1, saves);
        Assert.True(await service.CanAccessOrderAsync(guest.Id, null, rawToken));
    }

    private static OrderService ServiceFor(params Order[] orders)
    {
        var repository = InterfaceStub.Create<IOrderRepository>((method, args) => method.Name switch
        {
            "GetByIdAsync" => Task.FromResult(orders.SingleOrDefault(order => order.Id == (int)args![0]!)),
            _ => throw new InvalidOperationException($"Unexpected order call: {method.Name}")
        });
        var uow = InterfaceStub.Create<IUnitOfWork>((method, _) => method.Name switch
        {
            "get_Orders" => repository,
            "Dispose" => null,
            _ => throw new InvalidOperationException($"Unexpected unit of work call: {method.Name}")
        });
        return new OrderService(uow, TestMapper.Create(), null!, null!, null!);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
