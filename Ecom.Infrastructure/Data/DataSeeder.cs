using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecom.Infrastructure.Data
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUsers>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Seed Roles
            await SeedRolesAsync(roleManager);

            // Seed Admin User
            await SeedAdminUserAsync(userManager);

            // Seed Coupons
            await SeedCouponsAsync(context);
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roles = { "Admin", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private static async Task SeedAdminUserAsync(UserManager<AppUsers> userManager)
        {
            const string adminEmail = "admin@taani.com";
            const string adminPassword = "Admin@123";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new AppUsers
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    PhoneNumber = "+963123456789",
                    PhoneNumberConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }

        private static async Task SeedCouponsAsync(EcomDbContext context)
        {
            // Check if coupons already exist
            if (await context.Coupons.AnyAsync())
            {
                return; // Don't seed if coupons already exist
            }

            var coupons = new List<Coupon>
            {
                // Percentage Discount Coupons
                new Coupon
                {
                    Code = "WELCOME10",
                    Description = "Welcome discount - 10% off your first order",
                    Type = CouponType.Percentage,
                    Value = 10,
                    MinimumOrderAmount = 50,
                    MaximumDiscountAmount = 25,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(3),
                    UsageLimit = 1000,
                    IsActive = true,
                    IsSingleUse = false
                },
                new Coupon
                {
                    Code = "SAVE20",
                    Description = "Save 20% on orders over $100",
                    Type = CouponType.Percentage,
                    Value = 20,
                    MinimumOrderAmount = 100,
                    MaximumDiscountAmount = 50,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(6),
                    UsageLimit = 500,
                    IsActive = true,
                    IsSingleUse = false
                },
                new Coupon
                {
                    Code = "FLASH30",
                    Description = "Flash sale - 30% off for limited time",
                    Type = CouponType.Percentage,
                    Value = 30,
                    MinimumOrderAmount = 75,
                    MaximumDiscountAmount = 100,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(7),
                    UsageLimit = 200,
                    IsActive = true,
                    IsSingleUse = false
                },

                // Fixed Amount Discount Coupons
                new Coupon
                {
                    Code = "SAVE5",
                    Description = "Save $5 on any order",
                    Type = CouponType.FixedAmount,
                    Value = 5,
                    MinimumOrderAmount = 25,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(12),
                    UsageLimit = 2000,
                    IsActive = true,
                    IsSingleUse = false
                },
                new Coupon
                {
                    Code = "BIGSAVE25",
                    Description = "Save $25 on orders over $150",
                    Type = CouponType.FixedAmount,
                    Value = 25,
                    MinimumOrderAmount = 150,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(6),
                    UsageLimit = 300,
                    IsActive = true,
                    IsSingleUse = false
                },
                new Coupon
                {
                    Code = "MEGA50",
                    Description = "Mega discount - $50 off orders over $300",
                    Type = CouponType.FixedAmount,
                    Value = 50,
                    MinimumOrderAmount = 300,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(3),
                    UsageLimit = 100,
                    IsActive = true,
                    IsSingleUse = false
                },

                // Free Shipping Coupons
                new Coupon
                {
                    Code = "FREESHIP",
                    Description = "Free shipping on any order",
                    Type = CouponType.FreeShipping,
                    Value = 0,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(6),
                    UsageLimit = 1000,
                    IsActive = true,
                    IsSingleUse = false
                },
                new Coupon
                {
                    Code = "FREESHIP50",
                    Description = "Free shipping on orders over $50",
                    Type = CouponType.FreeShipping,
                    Value = 0,
                    MinimumOrderAmount = 50,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(12),
                    UsageLimit = 5000,
                    IsActive = true,
                    IsSingleUse = false
                },

                // Single Use Coupons
                new Coupon
                {
                    Code = "FIRSTORDER",
                    Description = "First order special - 15% off",
                    Type = CouponType.Percentage,
                    Value = 15,
                    MinimumOrderAmount = 30,
                    MaximumDiscountAmount = 30,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(1),
                    UsageLimit = 1,
                    IsActive = true,
                    IsSingleUse = true
                },
                new Coupon
                {
                    Code = "VIP100",
                    Description = "VIP customer - $100 off",
                    Type = CouponType.FixedAmount,
                    Value = 100,
                    MinimumOrderAmount = 500,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(2),
                    UsageLimit = 1,
                    IsActive = true,
                    IsSingleUse = true
                },

                // Expired Coupon (for testing)
                new Coupon
                {
                    Code = "EXPIRED20",
                    Description = "Expired coupon - 20% off",
                    Type = CouponType.Percentage,
                    Value = 20,
                    StartDate = DateTime.UtcNow.AddMonths(-2),
                    EndDate = DateTime.UtcNow.AddDays(-1),
                    UsageLimit = 100,
                    IsActive = false,
                    IsSingleUse = false
                },

                // Future Coupon
                new Coupon
                {
                    Code = "FUTURE15",
                    Description = "Future coupon - 15% off",
                    Type = CouponType.Percentage,
                    Value = 15,
                    StartDate = DateTime.UtcNow.AddDays(7),
                    EndDate = DateTime.UtcNow.AddMonths(1),
                    UsageLimit = 200,
                    IsActive = true,
                    IsSingleUse = false
                }
            };

            await context.Coupons.AddRangeAsync(coupons);
            await context.SaveChangesAsync();
        }
    }
}
