using Ecom.Infrastructure.Repositories.Interfaces;

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
        IBaseRepository<Domain.Entity.Transaction> Transactions { get; }
        IBaseRepository<Domain.Entity.Rating> Ratings { get; }
        IBaseRepository<Domain.Entity.ProductDetails> ProductDetails { get; }

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}

