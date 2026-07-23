using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Infrastructure.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        ICategoryRepository Categories { get; }
        IProductRepository Products { get; }
        IOrderRepository Orders { get; }
        ISubCategoryRepository SubCategories { get; }
        IBaseRepository<Domain.Entity.OrderItem> OrderItems { get; }
        IBaseRepository<Domain.Entity.ShippingAddress> ShippingAddresses { get; }
        ITransactionRepository Transactions { get; }
        IBaseRepository<Domain.Entity.Rating> Ratings { get; }
        IBaseRepository<Domain.Entity.ProductDetails> ProductDetails { get; }
        ICouponRepository Coupons { get; }
        IVisitorRepository Visitors { get; }
        IHealthPingRepository HealthPings { get; }
        ICarouselRepository Carousels { get; }
        DbContext Context { get; }  // ✔ REQUIRED

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}

