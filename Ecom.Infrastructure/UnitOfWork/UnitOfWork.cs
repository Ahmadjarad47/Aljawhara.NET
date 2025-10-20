using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Ecom.Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly EcomDbContext _context;
        private IDbContextTransaction? _transaction;

        private ICategoryRepository? _categories;
        private IProductRepository? _products;
        private IOrderRepository? _orders;
        private ISubCategoryRepository? _subCategories;
        private IBaseRepository<Domain.Entity.OrderItem>? _orderItems;
        private IBaseRepository<Domain.Entity.ShippingAddress>? _shippingAddresses;
        private ITransactionRepository? _transactions;
        private IBaseRepository<Domain.Entity.Rating>? _ratings;
        private IBaseRepository<Domain.Entity.ProductDetails>? _productDetails;
        private ICouponRepository? _coupons;

        public UnitOfWork(EcomDbContext context)
        {
            _context = context;
        }

        public ICategoryRepository Categories =>
            _categories ??= new CategoryRepository(_context);

        public IProductRepository Products =>
            _products ??= new ProductRepository(_context);

        public IOrderRepository Orders =>
            _orders ??= new OrderRepository(_context);

        public ISubCategoryRepository SubCategories =>
            _subCategories ??= new SubCategoryRepository(_context);

        public IBaseRepository<Domain.Entity.OrderItem> OrderItems =>
            _orderItems ??= new BaseRepository<Domain.Entity.OrderItem>(_context);

        public IBaseRepository<Domain.Entity.ShippingAddress> ShippingAddresses =>
            _shippingAddresses ??= new BaseRepository<Domain.Entity.ShippingAddress>(_context);

        public ITransactionRepository Transactions =>
            _transactions ??= new TransactionRepository(_context);

        public IBaseRepository<Domain.Entity.Rating> Ratings =>
            _ratings ??= new BaseRepository<Domain.Entity.Rating>(_context);

        public IBaseRepository<Domain.Entity.ProductDetails> ProductDetails =>
            _productDetails ??= new BaseRepository<Domain.Entity.ProductDetails>(_context);

        public ICouponRepository Coupons =>
            _coupons ??= new CouponRepository(_context);

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }
}

