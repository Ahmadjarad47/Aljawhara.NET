using Ecom.Domain.constant;
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
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<ProductVariantValue> ProductVariantValues { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ShippingAddress> ShippingAddresses { get; set; }
        public DbSet<ShippingSettings> ShippingSettings { get; set; }
        public DbSet<ShippingAreaRate> ShippingAreaRates { get; set; }
        public DbSet<ShippingGovernorate> ShippingGovernorates { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<Visitor> Visitors { get; set; }
        public DbSet<HealthPing> HealthPings { get; set; }
        public DbSet<Carousel> Carousels { get; set; }
        public DbSet<PaymentAttempt> PaymentAttempts { get; set; }
        public DbSet<InventoryReservation> InventoryReservations { get; set; }
        public DbSet<CouponReservation> CouponReservations { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<PaymentTransitionAudit> PaymentTransitionAudits { get; set; }
        public DbSet<OrderNumberCounter> OrderNumberCounters { get; set; }
        public DbSet<WhatsAppInvoice> WhatsAppInvoices { get; set; }
        public DbSet<WhatsAppDeliveryEvent> WhatsAppDeliveryEvents { get; set; }
        public DbSet<WhatsAppConsentAudit> WhatsAppConsentAudits { get; set; }
        public DbSet<WhatsAppPhoneVerification> WhatsAppPhoneVerifications { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ShippingSettings>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.DefaultFee).HasColumnType("decimal(18,3)");
                entity.Property(e => e.FreeShippingThreshold).HasColumnType("decimal(18,3)");
                entity.Property(e => e.Version).IsConcurrencyToken();
                entity.HasMany(e => e.Governorates).WithOne().HasForeignKey(e => e.ShippingSettingsId).OnDelete(DeleteBehavior.Cascade);
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_ShippingSettings_Singleton", "\"Id\" = 1");
                    table.HasCheckConstraint("CK_ShippingSettings_DefaultFee", "\"DefaultFee\" BETWEEN 0 AND 1000000");
                    table.HasCheckConstraint("CK_ShippingSettings_Threshold", "\"FreeShippingThreshold\" IS NULL OR \"FreeShippingThreshold\" BETWEEN 0 AND 1000000");
                    table.HasCheckConstraint("CK_ShippingSettings_Version", "\"Version\" > 0");
                });
                entity.HasData(new ShippingSettings { Id = 1, DefaultFee = 2m, FreeShippingThreshold = 20m, Version = 1 });
            });
            modelBuilder.Entity<ShippingGovernorate>(entity =>
            {
                entity.HasKey(e => new { e.ShippingSettingsId, e.StateKey });
                entity.Property(e => e.StateKey).HasMaxLength(100);
                entity.Property(e => e.State).IsRequired().HasMaxLength(100);
                entity.HasMany(e => e.Areas).WithOne().HasForeignKey(e => new { e.ShippingSettingsId, e.StateKey }).OnDelete(DeleteBehavior.Cascade);
                entity.HasData(
                    new ShippingGovernorate { ShippingSettingsId = 1, StateKey = "العاصمة", State = "محافظة العاصمة" },
                    new ShippingGovernorate { ShippingSettingsId = 1, StateKey = "حولي", State = "محافظة حولي" },
                    new ShippingGovernorate { ShippingSettingsId = 1, StateKey = "الفروانية", State = "محافظة الفروانية" },
                    new ShippingGovernorate { ShippingSettingsId = 1, StateKey = "الجهراء", State = "محافظة الجهراء" },
                    new ShippingGovernorate { ShippingSettingsId = 1, StateKey = "الأحمدي", State = "محافظة الأحمدي" },
                    new ShippingGovernorate { ShippingSettingsId = 1, StateKey = "مبارك الكبير", State = "محافظة مبارك الكبير" });
            });
            modelBuilder.Entity<ShippingAreaRate>(entity =>
            {
                entity.HasKey(e => new { e.ShippingSettingsId, e.StateKey, e.CityKey });
                entity.Property(e => e.StateKey).HasMaxLength(100);
                entity.Property(e => e.CityKey).HasMaxLength(100);
                entity.Property(e => e.City).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Fee).HasColumnType("decimal(18,3)");
                entity.ToTable(table => table.HasCheckConstraint("CK_ShippingAreaRates_Fee", "\"Fee\" BETWEEN 0 AND 1000000"));
                entity.HasData(
                    new ShippingAreaRate { ShippingSettingsId = 1, StateKey = "الأحمدي", CityKey = "مدينة صباح الأحمد", City = "مدينة صباح الأحمد", Fee = 6m },
                    new ShippingAreaRate { ShippingSettingsId = 1, StateKey = "الأحمدي", CityKey = "الخيران", City = "الخيران", Fee = 6m },
                    new ShippingAreaRate { ShippingSettingsId = 1, StateKey = "الجهراء", CityKey = "العبدلي", City = "العبدلي", Fee = 6m },
                    new ShippingAreaRate { ShippingSettingsId = 1, StateKey = "الأحمدي", CityKey = "الوفرة", City = "الوفرة", Fee = 6m },
                    new ShippingAreaRate { ShippingSettingsId = 1, StateKey = "مبارك الكبير", CityKey = "صباح السالم", City = "صباح السالم", Fee = 3m },
                    new ShippingAreaRate { ShippingSettingsId = 1, StateKey = "الأحمدي", CityKey = "الأحمدي", City = "الأحمدي", Fee = 3m },
                    new ShippingAreaRate { ShippingSettingsId = 1, StateKey = "الجهراء", CityKey = "المطلاع", City = "المطلاع", Fee = 4m });
            });

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
                entity.Property(e => e.oldPrice).HasColumnType("decimal(18,3)");
                entity.Property(e => e.newPrice).HasColumnType("decimal(18,3)");
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

            // Configure ProductVariant
            modelBuilder.Entity<ProductVariant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NameAr).HasMaxLength(100);
                entity.HasOne(e => e.Product)
                      .WithMany(e => e.Variants)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure ProductVariantValue
            modelBuilder.Entity<ProductVariantValue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Value).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ValueAr).HasMaxLength(100);
                entity.Property(e => e.Price).HasColumnType("decimal(18,3)");
                entity.HasOne(e => e.ProductVariant)
                      .WithMany(e => e.Values)
                      .HasForeignKey(e => e.ProductVariantId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure Order
            modelBuilder.Entity<WhatsAppInvoice>(entity =>
            {
                entity.HasIndex(e => e.PaymentAttemptId).IsUnique();
                entity.HasIndex(e => e.ProviderMessageId).IsUnique().HasFilter("\"ProviderMessageId\" IS NOT NULL");
                entity.HasIndex(e => new { e.Status, e.NextAttemptAtUtc });
                entity.HasOne(e => e.PaymentAttempt).WithMany().HasForeignKey(e => e.PaymentAttemptId).OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.InvoiceNumber).HasMaxLength(100);
                entity.Property(e => e.Destination).HasMaxLength(15);
                entity.Property(e => e.AppName).HasMaxLength(100);
                entity.Property(e => e.ProviderMessageId).HasMaxLength(200);
                entity.Property(e => e.WhatsAppMessageId).HasMaxLength(200);
                entity.Property(e => e.MediaTokenHash).HasMaxLength(64);
                entity.Property(e => e.LeaseOwner).HasMaxLength(100);
                entity.Property(e => e.LastErrorCode).HasMaxLength(100);
                entity.Property(e => e.ReconciliationEvidence).HasMaxLength(1000);
                entity.Property(e => e.RowVersion).IsRowVersion();
            });
            modelBuilder.Entity<WhatsAppDeliveryEvent>(entity =>
            {
                entity.HasIndex(e => e.EventKey).IsUnique();
                entity.HasIndex(e => e.ProviderMessageId);
                entity.Property(e => e.EventKey).HasMaxLength(64);
                entity.Property(e => e.AppName).HasMaxLength(100);
                entity.Property(e => e.ProviderMessageId).HasMaxLength(200);
                entity.Property(e => e.Destination).HasMaxLength(15);
                entity.Property(e => e.WhatsAppMessageId).HasMaxLength(200);
                entity.Property(e => e.Type).HasMaxLength(20);
            });
            modelBuilder.Entity<WhatsAppConsentAudit>(entity =>
            {
                entity.HasIndex(e => new { e.Phone, e.OccurredAtUtc });
                entity.Property(e => e.Phone).HasMaxLength(15);
                entity.Property(e => e.Source).HasMaxLength(50);
                entity.Property(e => e.ProviderSyncStatus).HasMaxLength(30);
                entity.Property(e => e.ProviderSyncError).HasMaxLength(200);
                entity.HasOne(e => e.AppUser).WithMany().HasForeignKey(e => e.AppUserId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Order).WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.SetNull);
            });
            modelBuilder.Entity<WhatsAppPhoneVerification>(entity =>
            {
                entity.HasIndex(e => new { e.AppUserId, e.Phone, e.ExpiresAtUtc });
                entity.Property(e => e.Phone).HasMaxLength(15);
                entity.Property(e => e.CodeHash).HasMaxLength(32);
                entity.Property(e => e.CodeSalt).HasMaxLength(16);
                entity.Property(e => e.ProviderMessageId).HasMaxLength(200);
                entity.HasOne(e => e.AppUser).WithMany().HasForeignKey(e => e.AppUserId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,3)");
                entity.Property(e => e.Shipping).HasColumnType("decimal(18,3)");
                entity.Property(e => e.Tax).HasColumnType("decimal(18,3)");
                entity.Property(e => e.Total).HasColumnType("decimal(18,3)");
                entity.Property(e => e.WhatsAppConsentPhone).HasMaxLength(40);
                entity.Property(e => e.WhatsAppConsentSource).HasMaxLength(50);
                entity.Property(e => e.GuestAccessTokenHash).HasMaxLength(64);
                entity.Property(e => e.CheckoutIdempotencyKeyHash).HasMaxLength(64);
                entity.Property(e => e.CheckoutRequestHash).HasMaxLength(64);
                entity.Property(e => e.GuestTokenDerivationNonce).HasMaxLength(64);
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
                entity.Property(e => e.CouponDiscountAmount).HasColumnType("decimal(18,3)");
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<AppUsers>(entity =>
            {
                entity.Property(e => e.WhatsAppConsentPhone).HasMaxLength(15);
            });

            // Configure OrderItem
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Image).HasMaxLength(500);
                entity.Property(e => e.Price).HasColumnType("decimal(18,3)");
                entity.ToTable(table => table.HasCheckConstraint("CK_OrderItems_Quantity_Positive", "\"Quantity\" > 0"));
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
                entity.Property(e => e.City).IsRequired(false).HasMaxLength(100);
                entity.Property(e => e.State).IsRequired(false).HasMaxLength(100);
                entity.Property(e => e.PostalCode).IsRequired(false).HasMaxLength(20);
                entity.Property(e => e.Country).IsRequired(false).HasMaxLength(100);
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
                entity.Property(e => e.Amount).HasColumnType("decimal(18,3)");
                entity.Property(e => e.RefundAmount).HasColumnType("decimal(18,3)");
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.GatewayInvoiceId).HasMaxLength(100);
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
                entity.Property(e => e.Value).HasColumnType("decimal(18,3)");
                entity.Property(e => e.MinimumOrderAmount).HasColumnType("decimal(18,3)");
                entity.Property(e => e.MaximumDiscountAmount).HasColumnType("decimal(18,3)");
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.EndDate).IsRequired();
                entity.HasOne(e => e.AppUser)
                      .WithMany()
                      .HasForeignKey(e => e.AppUserId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_Coupons_UsedCount_NonNegative", "\"UsedCount\" >= 0");
                    table.HasCheckConstraint("CK_Coupons_UsageLimit", "\"UsageLimit\" IS NULL OR (\"UsageLimit\" >= 0 AND \"UsedCount\" <= \"UsageLimit\")");
                });
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Add indexes for performance
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SubCategoryId);

            modelBuilder.Entity<Product>()
                .ToTable(table => table.HasCheckConstraint("CK_Products_TotalInStock_NonNegative", "\"TotalInStock\" >= 0"));

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.AppUserId);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.CheckoutIdempotencyKeyHash)
                .IsUnique()
                .HasFilter("\"CheckoutIdempotencyKeyHash\" IS NOT NULL");

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

            // Configure Carousel
            modelBuilder.Entity<Carousel>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Price).HasColumnType("decimal(18,3)");
                entity.Property(e => e.Image).HasMaxLength(500);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<PaymentAttempt>(entity =>
            {
                entity.Property(e => e.Amount).HasColumnType("decimal(18,3)");
                entity.Property(e => e.Currency).HasMaxLength(3);
                entity.Property(e => e.IdempotencyKeyHash).HasMaxLength(64);
                entity.Property(e => e.RequestHash).HasMaxLength(64);
                entity.Property(e => e.MerchantReference).HasMaxLength(80);
                entity.Property(e => e.GatewayInvoiceId).HasMaxLength(100);
                entity.Property(e => e.VerificationEvidenceId).HasMaxLength(64);
                entity.Property(e => e.GatewayTransactionId).HasMaxLength(200);
                entity.Property(e => e.VerifiedAmount).HasColumnType("decimal(18,3)");
                entity.Property(e => e.VerifiedTotal).HasColumnType("decimal(18,3)");
                entity.Property(e => e.VerifiedSubtotal).HasColumnType("decimal(18,3)");
                entity.Property(e => e.VerifiedRefundedAmount).HasColumnType("decimal(18,3)");
                entity.Property(e => e.PaymentUrl).HasMaxLength(2048);
                entity.Property(e => e.LeaseOwner).HasMaxLength(100);
                entity.Property(e => e.LastErrorCode).HasMaxLength(100);
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.HasIndex(e => e.IdempotencyKeyHash).IsUnique();
                entity.HasIndex(e => e.MerchantReference).IsUnique();
                entity.HasIndex(e => e.OrderId).IsUnique().HasFilter("\"Status\" IN (0, 1, 2, 3)");
                entity.HasIndex(e => e.GatewayInvoiceId).IsUnique().HasFilter("\"GatewayInvoiceId\" IS NOT NULL");
                entity.HasOne(e => e.Order).WithMany(e => e.PaymentAttempts).HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Transaction).WithOne().HasForeignKey<PaymentAttempt>(e => e.TransactionId).OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_PaymentAttempts_Amount_Positive", "\"Amount\" > 0");
                    table.HasCheckConstraint("CK_PaymentAttempts_Currency_KWD", "\"Currency\" = 'KWD'");
                });
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<InventoryReservation>(entity =>
            {
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.HasIndex(e => new { e.OrderId, e.ProductId }).IsUnique();
                entity.ToTable(table => table.HasCheckConstraint("CK_InventoryReservations_Quantity_Positive", "\"Quantity\" > 0"));
                entity.HasOne(e => e.Order).WithMany(e => e.InventoryReservations).HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<CouponReservation>(entity =>
            {
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.HasIndex(e => e.OrderId).IsUnique();
                entity.HasOne(e => e.Order).WithOne(e => e.CouponReservation).HasForeignKey<CouponReservation>(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Coupon).WithMany().HasForeignKey(e => e.CouponId).OnDelete(DeleteBehavior.Restrict);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.Property(e => e.Type).HasMaxLength(100);
                entity.Property(e => e.DeduplicationKey).HasMaxLength(200);
                entity.Property(e => e.LeaseOwner).HasMaxLength(100);
                entity.Property(e => e.LastErrorCode).HasMaxLength(100);
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.HasIndex(e => e.DeduplicationKey).IsUnique();
                entity.HasIndex(e => new { e.Status, e.NextAttemptAtUtc });
                entity.ToTable(table => table.HasCheckConstraint("CK_OutboxMessages_AttemptCount_NonNegative", "\"AttemptCount\" >= 0"));
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<PaymentTransitionAudit>(entity =>
            {
                entity.Property(e => e.EventKey).HasMaxLength(200);
                entity.Property(e => e.ReasonCode).HasMaxLength(100);
                entity.HasIndex(e => e.EventKey).IsUnique();
                entity.HasOne(e => e.PaymentAttempt).WithMany().HasForeignKey(e => e.PaymentAttemptId).OnDelete(DeleteBehavior.Restrict);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<OrderNumberCounter>(entity =>
            {
                entity.HasKey(e => e.DateKey);
                entity.Property(e => e.DateKey).ValueGeneratedNever();
                entity.ToTable(table => table.HasCheckConstraint("CK_OrderNumberCounters_NextValue_Positive", "\"NextValue\" > 0"));
            });
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

