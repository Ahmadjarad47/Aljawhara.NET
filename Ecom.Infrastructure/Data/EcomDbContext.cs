using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Ecom.Infrastructure.Data
{
    public class EcomDbContext : IdentityDbContext<AppUsers>
    {
        private readonly IServiceProvider? _serviceProvider;
        
        public EcomDbContext(DbContextOptions<EcomDbContext> options) : base(options)
        {
        }

        // Constructor with IServiceProvider for resolving IHttpContextAccessor
        public EcomDbContext(DbContextOptions<EcomDbContext> options, IServiceProvider serviceProvider) : base(options)
        {
            _serviceProvider = serviceProvider;
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductDetails> ProductDetails { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ShippingAddress> ShippingAddresses { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<Visitor> Visitors { get; set; }
        public DbSet<HealthPing> HealthPings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure SubCategory
            modelBuilder.Entity<SubCategory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasOne(e => e.Category)
                      .WithMany(e => e.SubCategories)
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure Product
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.oldPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.newPrice).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.subCategory)
                      .WithMany(e => e.Products)
                      .HasForeignKey(e => e.SubCategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure ProductDetails
            modelBuilder.Entity<ProductDetails>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
                entity.HasOne(e => e.Product)
                      .WithMany(e => e.productDetails)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure Order
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Shipping).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Tax).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.AppUser)
                      .WithMany(e => e.Orders)
                      .HasForeignKey(e => e.AppUserId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.ShippingAddress)
                      .WithMany()
                      .HasForeignKey(e => e.ShippingAddressId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Coupon)
                      .WithMany(e => e.Orders)
                      .HasForeignKey(e => e.CouponId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.CouponDiscountAmount).HasColumnType("decimal(18,2)");
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure OrderItem
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Image).HasMaxLength(500);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Order)
                      .WithMany(e => e.Items)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure ShippingAddress
            modelBuilder.Entity<ShippingAddress>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Phone).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Street).IsRequired().HasMaxLength(200);
                entity.Property(e => e.City).IsRequired().HasMaxLength(100);
                entity.Property(e => e.State).HasMaxLength(100);
                entity.Property(e => e.PostalCode).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Country).IsRequired().HasMaxLength(100);
                entity.HasOne(e => e.AppUser)
                      .WithOne()
                      .HasForeignKey<ShippingAddress>(e => e.AppUserId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure Transaction
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Order)
                      .WithMany()
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.AppUser)
                      .WithMany(e => e.Transactions)
                      .HasForeignKey(e => e.AppUserId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure Rating
            modelBuilder.Entity<Rating>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.RatingNumber).HasColumnType("decimal(2,1)");
                entity.HasOne(e => e.Product)
                      .WithMany(e => e.Ratings)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure Coupon
            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Value).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinimumOrderAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MaximumDiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.EndDate).IsRequired();
                entity.HasOne(e => e.AppUser)
                      .WithMany()
                      .HasForeignKey(e => e.AppUserId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Add indexes for performance
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SubCategoryId);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.AppUserId);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            modelBuilder.Entity<Coupon>()
                .HasIndex(c => c.Code)
                .IsUnique();

            modelBuilder.Entity<Coupon>()
                .HasIndex(c => c.AppUserId);

            modelBuilder.Entity<Coupon>()
                .HasIndex(c => c.StartDate);

            modelBuilder.Entity<Coupon>()
                .HasIndex(c => c.EndDate);

            // Configure Visitor
            modelBuilder.Entity<Visitor>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IpAddress).HasMaxLength(100);
                entity.Property(e => e.UserAgent).HasMaxLength(1024);
                entity.Property(e => e.Path).HasMaxLength(512);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<Visitor>()
                .HasIndex(v => v.VisitedAtUtc);

            // Configure HealthPing
            modelBuilder.Entity<HealthPing>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasMaxLength(100);
                entity.Property(e => e.Error).HasMaxLength(1024);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<HealthPing>()
                .HasIndex(h => h.CreatedAt);
        }

        public override int SaveChanges()
        {
            UpdateAuditFields();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateAuditFields()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is Domain.comman.BaseEntity &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            var userId = GetCurrentUserId();

            foreach (var entry in entries)
            {
                var entity = (Domain.comman.BaseEntity)entry.Entity;

                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = DateTime.UtcNow;
                    entity.CreatedBy = userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entity.UpdatedAt = DateTime.UtcNow;
                    entity.UpdatedBy = userId;
                }
            }
        }

        private string GetCurrentUserId()
        {
            if (_serviceProvider == null)
            {
                return "System";
            }

            var httpContextAccessor = _serviceProvider.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            var httpContext = httpContextAccessor?.HttpContext;
            
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    return userIdClaim;
                }
            }
            return "System";
        }
    }
}

