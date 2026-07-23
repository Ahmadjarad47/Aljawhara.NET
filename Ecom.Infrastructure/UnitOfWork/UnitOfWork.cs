using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace Ecom.Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly EcomDbContext _context;
        private readonly IMemoryCache _cache;
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
        private IVisitorRepository? _visitors;
        private IHealthPingRepository? _healthPings;
        private ICarouselRepository? _carousels;

        public DbContext Context => _context; // REQUIRED for ExecutionStrategy

        public UnitOfWork(EcomDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public ICategoryRepository Categories =>
            _categories ??= new CategoryRepository(_context, _cache);

        public IProductRepository Products =>
            _products ??= new ProductRepository(_context, _cache);

        public IOrderRepository Orders =>
            _orders ??= new OrderRepository(_context, _cache);

        public ISubCategoryRepository SubCategories =>
            _subCategories ??= new SubCategoryRepository(_context, _cache);

        public IBaseRepository<Domain.Entity.OrderItem> OrderItems =>
            _orderItems ??= new BaseRepository<Domain.Entity.OrderItem>(_context, _cache);

        public IBaseRepository<Domain.Entity.ShippingAddress> ShippingAddresses =>
            _shippingAddresses ??= new BaseRepository<Domain.Entity.ShippingAddress>(_context, _cache);

        public ITransactionRepository Transactions =>
            _transactions ??= new TransactionRepository(_context, _cache);

        public IBaseRepository<Domain.Entity.Rating> Ratings =>
            _ratings ??= new BaseRepository<Domain.Entity.Rating>(_context, _cache);

        public IBaseRepository<Domain.Entity.ProductDetails> ProductDetails =>
            _productDetails ??= new BaseRepository<Domain.Entity.ProductDetails>(_context, _cache);

        public ICouponRepository Coupons =>
            _coupons ??= new CouponRepository(_context, _cache);

        public IVisitorRepository Visitors =>
            _visitors ??= new VisitorRepository(_context, _cache);

        public IHealthPingRepository HealthPings =>
            _healthPings ??= new HealthPingRepository(_context, _cache);

        public ICarouselRepository Carousels =>
            _carousels ??= new CarouselRepository(_context, _cache);

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task BeginTransactionAsync()
        {
            if (_transaction != null)
                return; // Prevent double-open transaction

            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction == null)
                return;

            try
            {
                await _context.SaveChangesAsync();  // Ensure final save
                await _transaction.CommitAsync();
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction == null)
                return;

            try
            {
                await _transaction.RollbackAsync();
            }
            finally
            {
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
