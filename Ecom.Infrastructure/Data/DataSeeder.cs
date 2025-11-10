using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace Ecom.Infrastructure.Data
{
    public static class DataSeeder
    {
        private static Random _random = new Random();

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

            // Seed all entities in order
            await SeedCategoriesAsync(context);
            await SeedSubCategoriesAsync(context);
            await SeedProductsAsync(context);
            await SeedProductDetailsAsync(context);
            await SeedUsersAsync(userManager);
            await SeedShippingAddressesAsync(context);
            await SeedRatingsAsync(context);
            await SeedCouponsAsync(context);
            await SeedOrdersAsync(context);
            await SeedOrderItemsAsync(context);
            await SeedTransactionsAsync(context);
            await SeedVisitorsAsync(context);
            await SeedHealthPingsAsync(context);
        }

        /// <summary>
        /// Generates a random date within a specified year and month range
        /// </summary>
        private static DateTime GetRandomDate(int year, int month, bool allowPastYears = true)
        {
            var yearsOffset = allowPastYears ? _random.Next(-2, 1) : 0; // -2 to 0 years (past 2 years to current)
            var monthOffset = _random.Next(-11, 1); // -11 to 0 months
            var day = _random.Next(1, 29); // Avoid day 30/31 issues
            var hour = _random.Next(0, 24);
            var minute = _random.Next(0, 60);
            var second = _random.Next(0, 60);

            var targetYear = year + yearsOffset;
            var targetMonth = month + monthOffset;

            // Handle month overflow/underflow
            while (targetMonth < 1)
            {
                targetMonth += 12;
                targetYear--;
            }
            while (targetMonth > 12)
            {
                targetMonth -= 12;
                targetYear++;
            }

            return new DateTime(targetYear, targetMonth, day, hour, minute, second, DateTimeKind.Utc);
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
                var createdAt = GetRandomDate(DateTime.UtcNow.Year, DateTime.UtcNow.Month, false);
                createdAt = new DateTime(2022, 1, 15, 10, 0, 0, DateTimeKind.Utc); // Admin created in Jan 2022

                adminUser = new AppUsers
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    PhoneNumber = "+963123456789",
                    PhoneNumberConfirmed = true,
                    CreatedAt = createdAt
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    // Update CreatedAt after creation (Identity doesn't preserve it)
                    adminUser.CreatedAt = createdAt;
                    await userManager.UpdateAsync(adminUser);
                }
            }
        }

        private static async Task SeedCategoriesAsync(EcomDbContext context)
        {
            if (await context.Categories.AnyAsync())
            {
                return;
            }

            var categories = new List<Category>
            {
                new Category
                {
                    Name = "Electronics",
                    NameAr = "إلكترونيات",
                    Description = "Electronic devices and accessories",
                    DescriptionAr = "الأجهزة الإلكترونية والملحقات",
                    CreatedAt = GetRandomDate(2022, 3),
                    IsActive = true
                },
                new Category
                {
                    Name = "Clothing",
                    NameAr = "ملابس",
                    Description = "Men's and women's clothing",
                    DescriptionAr = "ملابس رجالية ونسائية",
                    CreatedAt = GetRandomDate(2022, 5),
                    IsActive = true
                },
                new Category
                {
                    Name = "Home & Kitchen",
                    NameAr = "المنزل والمطبخ",
                    Description = "Home decoration and kitchen appliances",
                    DescriptionAr = "ديكور المنزل وأجهزة المطبخ",
                    CreatedAt = GetRandomDate(2022, 7),
                    IsActive = true
                },
                new Category
                {
                    Name = "Books",
                    NameAr = "كتب",
                    Description = "Books and literature",
                    DescriptionAr = "الكتب والأدب",
                    CreatedAt = GetRandomDate(2023, 2),
                    IsActive = true
                },
                new Category
                {
                    Name = "Sports & Outdoors",
                    NameAr = "الرياضة والهواء الطلق",
                    Description = "Sports equipment and outdoor gear",
                    DescriptionAr = "معدات رياضية ومعدات في الهواء الطلق",
                    CreatedAt = GetRandomDate(2023, 9),
                    IsActive = true
                },
                new Category
                {
                    Name = "Beauty & Personal Care",
                    NameAr = "الجمال والعناية الشخصية",
                    Description = "Beauty products and personal care items",
                    DescriptionAr = "منتجات التجميل والعناية الشخصية",
                    CreatedAt = GetRandomDate(2024, 1),
                    IsActive = true
                }
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        private static async Task SeedSubCategoriesAsync(EcomDbContext context)
        {
            if (await context.SubCategories.AnyAsync())
            {
                return;
            }

            var categories = await context.Categories.ToListAsync();
            if (!categories.Any())
            {
                return;
            }

            var electronics = categories.FirstOrDefault(c => c.Name == "Electronics");
            var clothing = categories.FirstOrDefault(c => c.Name == "Clothing");
            var homeKitchen = categories.FirstOrDefault(c => c.Name == "Home & Kitchen");
            var books = categories.FirstOrDefault(c => c.Name == "Books");
            var sports = categories.FirstOrDefault(c => c.Name == "Sports & Outdoors");
            var beauty = categories.FirstOrDefault(c => c.Name == "Beauty & Personal Care");

            var subCategories = new List<SubCategory>();

            if (electronics != null)
            {
                subCategories.AddRange(new[]
                {
                    new SubCategory
                    {
                        Name = "Smartphones",
                        NameAr = "هواتف ذكية",
                        Description = "Latest smartphones",
                        DescriptionAr = "أحدث الهواتف الذكية",
                        CategoryId = electronics.Id,
                        CreatedAt = GetRandomDate(2022, 4),
                        IsActive = true
                    },
                    new SubCategory
                    {
                        Name = "Laptops",
                        NameAr = "أجهزة كمبيوتر محمولة",
                        Description = "Laptops and notebooks",
                        DescriptionAr = "أجهزة كمبيوتر محمولة ودفاتر",
                        CategoryId = electronics.Id,
                        CreatedAt = GetRandomDate(2022, 6),
                        IsActive = true
                    },
                    new SubCategory
                    {
                        Name = "Headphones",
                        NameAr = "سماعات الرأس",
                        Description = "Audio devices and headphones",
                        DescriptionAr = "الأجهزة الصوتية وسماعات الرأس",
                        CategoryId = electronics.Id,
                        CreatedAt = GetRandomDate(2023, 1),
                        IsActive = true
                    }
                });
            }

            if (clothing != null)
            {
                subCategories.AddRange(new[]
                {
                    new SubCategory
                    {
                        Name = "Men's T-Shirts",
                        NameAr = "قمصان رجالية",
                        Description = "Men's casual t-shirts",
                        DescriptionAr = "قمصان رجالية عادية",
                        CategoryId = clothing.Id,
                        CreatedAt = GetRandomDate(2022, 6),
                        IsActive = true
                    },
                    new SubCategory
                    {
                        Name = "Women's Dresses",
                        NameAr = "فساتين نسائية",
                        Description = "Women's dresses and gowns",
                        DescriptionAr = "فساتين نسائية وثياب",
                        CategoryId = clothing.Id,
                        CreatedAt = GetRandomDate(2022, 8),
                        IsActive = true
                    },
                    new SubCategory
                    {
                        Name = "Shoes",
                        NameAr = "أحذية",
                        Description = "Footwear for all occasions",
                        DescriptionAr = "أحذية لجميع المناسبات",
                        CategoryId = clothing.Id,
                        CreatedAt = GetRandomDate(2023, 3),
                        IsActive = true
                    }
                });
            }

            if (homeKitchen != null)
            {
                subCategories.AddRange(new[]
                {
                    new SubCategory
                    {
                        Name = "Cookware",
                        NameAr = "أدوات المطبخ",
                        Description = "Kitchen cookware and utensils",
                        DescriptionAr = "أدوات المطبخ والأدوات",
                        CategoryId = homeKitchen.Id,
                        CreatedAt = GetRandomDate(2022, 8),
                        IsActive = true
                    },
                    new SubCategory
                    {
                        Name = "Home Decor",
                        NameAr = "ديكور المنزل",
                        Description = "Home decoration items",
                        DescriptionAr = "أدوات ديكور المنزل",
                        CategoryId = homeKitchen.Id,
                        CreatedAt = GetRandomDate(2023, 5),
                        IsActive = true
                    }
                });
            }

            if (books != null)
            {
                subCategories.AddRange(new[]
                {
                    new SubCategory
                    {
                        Name = "Fiction",
                        NameAr = "روايات",
                        Description = "Fiction books and novels",
                        DescriptionAr = "كتب وروايات خيالية",
                        CategoryId = books.Id,
                        CreatedAt = GetRandomDate(2023, 3),
                        IsActive = true
                    },
                    new SubCategory
                    {
                        Name = "Non-Fiction",
                        NameAr = "غير خيالية",
                        Description = "Educational and non-fiction books",
                        DescriptionAr = "كتب تعليمية وغير خيالية",
                        CategoryId = books.Id,
                        CreatedAt = GetRandomDate(2023, 4),
                        IsActive = true
                    }
                });
            }

            if (sports != null)
            {
                subCategories.AddRange(new[]
                {
                    new SubCategory
                    {
                        Name = "Fitness Equipment",
                        NameAr = "معدات اللياقة البدنية",
                        Description = "Gym and fitness equipment",
                        DescriptionAr = "معدات الصالة الرياضية واللياقة البدنية",
                        CategoryId = sports.Id,
                        CreatedAt = GetRandomDate(2023, 10),
                        IsActive = true
                    },
                    new SubCategory
                    {
                        Name = "Outdoor Gear",
                        NameAr = "معدات في الهواء الطلق",
                        Description = "Camping and outdoor equipment",
                        DescriptionAr = "معدات التخييم والهواء الطلق",
                        CategoryId = sports.Id,
                        CreatedAt = GetRandomDate(2023, 11),
                        IsActive = true
                    }
                });
            }

            if (beauty != null)
            {
                subCategories.AddRange(new[]
                {
                    new SubCategory
                    {
                        Name = "Skincare",
                        NameAr = "العناية بالبشرة",
                        Description = "Face and body skincare products",
                        DescriptionAr = "منتجات العناية بالبشرة والجسم",
                        CategoryId = beauty.Id,
                        CreatedAt = GetRandomDate(2024, 2),
                        IsActive = true
                    },
                    new SubCategory
                    {
                        Name = "Makeup",
                        NameAr = "مستحضرات التجميل",
                        Description = "Cosmetics and makeup products",
                        DescriptionAr = "مستحضرات التجميل والماكياج",
                        CategoryId = beauty.Id,
                        CreatedAt = GetRandomDate(2024, 3),
                        IsActive = true
                    }
                });
            }

            await context.SubCategories.AddRangeAsync(subCategories);
            await context.SaveChangesAsync();
        }

        private static async Task SeedProductsAsync(EcomDbContext context)
        {
            if (await context.Products.AnyAsync())
            {
                return;
            }

            var subCategories = await context.SubCategories.ToListAsync();
            if (!subCategories.Any())
            {
                return;
            }

            var products = new List<Product>();
            var dates = new List<DateTime>();

            // Generate varied dates for products
            for (int i = 0; i < 30; i++)
            {
                dates.Add(GetRandomDate(2022, 3 + (i % 12)));
            }

            int dateIndex = 0;
            foreach (var subCat in subCategories)
            {
                var productCount = _random.Next(3, 6); // 3-5 products per subcategory
                for (int i = 0; i < productCount; i++)
                {
                    var oldPrice = (decimal)_random.Next(50, 500);
                    var newPrice = oldPrice * (decimal)(0.7 + _random.NextDouble() * 0.25); // 70-95% of old price
                    var createdAt = dates[dateIndex % dates.Count];
                    dateIndex++;

                    products.Add(new Product
                    {
                        Title = $"{subCat.Name} Product {i + 1}",
                        TitleAr = $"منتج {subCat.NameAr} {i + 1}",
                        Description = $"High quality {subCat.Name.ToLower()} product {i + 1}. Perfect for your needs.",
                        DescriptionAr = $"منتج {subCat.NameAr.ToLower()} عالي الجودة {i + 1}. مثالي لاحتياجاتك.",
                        oldPrice = oldPrice,
                        newPrice = newPrice,
                        IsInStock = _random.Next(100) > 10, // 90% in stock
                        TotalInStock = _random.Next(0, 100),
                        Images = new[] { $"image_{subCat.Id}_{i + 1}_1.jpg", $"image_{subCat.Id}_{i + 1}_2.jpg" },
                        SubCategoryId = subCat.Id,
                        CreatedAt = createdAt,
                        UpdatedAt = _random.Next(100) > 70 ? createdAt.AddDays(_random.Next(1, 90)) : null,
                        IsActive = true
                    });
                }
            }

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }

        private static async Task SeedProductDetailsAsync(EcomDbContext context)
        {
            if (await context.ProductDetails.AnyAsync())
            {
                return;
            }

            var products = await context.Products.ToListAsync();
            if (!products.Any())
            {
                return;
            }

            var productDetails = new List<ProductDetails>();

            foreach (var product in products)
            {
                var createdAt = product.CreatedAt.AddHours(_random.Next(1, 24));
                var detailCount = _random.Next(2, 5); // 2-4 details per product

                productDetails.AddRange(new[]
                {
                    new ProductDetails
                    {
                        Label = "Brand",
                        LabelAr = "العلامة التجارية",
                        Value = "Premium Brand",
                        ValueAr = "علامة تجارية متميزة",
                        ProductId = product.Id,
                        CreatedAt = createdAt,
                        IsActive = true
                    },
                    new ProductDetails
                    {
                        Label = "Material",
                        LabelAr = "المادة",
                        Value = "High Quality Material",
                        ValueAr = "مادة عالية الجودة",
                        ProductId = product.Id,
                        CreatedAt = createdAt,
                        IsActive = true
                    },
                    new ProductDetails
                    {
                        Label = "Warranty",
                        LabelAr = "الضمان",
                        Value = $"{_random.Next(1, 3)} Year(s)",
                        ValueAr = $"{_random.Next(1, 3)} سنة",
                        ProductId = product.Id,
                        CreatedAt = createdAt,
                        IsActive = true
                    }
                });

                if (detailCount > 3)
                {
                    productDetails.Add(new ProductDetails
                    {
                        Label = "Color",
                        LabelAr = "اللون",
                        Value = "Multiple Colors Available",
                        ValueAr = "ألوان متعددة متاحة",
                        ProductId = product.Id,
                        CreatedAt = createdAt,
                        IsActive = true
                    });
                }
            }

            await context.ProductDetails.AddRangeAsync(productDetails);
            await context.SaveChangesAsync();
        }

        private static async Task SeedUsersAsync(UserManager<AppUsers> userManager)
        {
            if (await userManager.Users.CountAsync() > 1) // More than just admin
            {
                return;
            }

            var users = new[]
            {
                new { Email = "user1@example.com", Password = "User@123", Name = "Ahmed", CreatedAt = GetRandomDate(2022, 4) },
                new { Email = "user2@example.com", Password = "User@123", Name = "Sara", CreatedAt = GetRandomDate(2022, 7) },
                new { Email = "user3@example.com", Password = "User@123", Name = "Mohammed", CreatedAt = GetRandomDate(2022, 9) },
                new { Email = "user4@example.com", Password = "User@123", Name = "Fatima", CreatedAt = GetRandomDate(2023, 1) },
                new { Email = "user5@example.com", Password = "User@123", Name = "Ali", CreatedAt = GetRandomDate(2023, 5) },
                new { Email = "user6@example.com", Password = "User@123", Name = "Layla", CreatedAt = GetRandomDate(2023, 8) },
                new { Email = "user7@example.com", Password = "User@123", Name = "Omar", CreatedAt = GetRandomDate(2023, 11) },
                new { Email = "user8@example.com", Password = "User@123", Name = "Aisha", CreatedAt = GetRandomDate(2024, 2) },
                new { Email = "user9@example.com", Password = "User@123", Name = "Hassan", CreatedAt = GetRandomDate(2024, 6) },
                new { Email = "user10@example.com", Password = "User@123", Name = "Noor", CreatedAt = GetRandomDate(2024, 9) }
            };

            foreach (var userData in users)
            {
                var existingUser = await userManager.FindByEmailAsync(userData.Email);
                if (existingUser == null)
                {
                    var user = new AppUsers
                    {
                        UserName = userData.Email,
                        Email = userData.Email,
                        EmailConfirmed = true,
                        PhoneNumber = $"+963{_random.Next(100000000, 999999999)}",
                        PhoneNumberConfirmed = _random.Next(100) > 20,
                        CreatedAt = userData.CreatedAt,
                        IsActive = _random.Next(100) > 10 // 90% active
                    };

                    var result = await userManager.CreateAsync(user, userData.Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "User");
                        // Update CreatedAt after creation
                        user.CreatedAt = userData.CreatedAt;
                        await userManager.UpdateAsync(user);
                    }
                }
            }
        }

        private static async Task SeedShippingAddressesAsync(EcomDbContext context)
        {
            if (await context.ShippingAddresses.AnyAsync())
            {
                return;
            }

            var users = await context.Set<AppUsers>().ToListAsync();
            if (!users.Any())
            {
                return;
            }

            var shippingAddresses = new List<ShippingAddress>();
            var cities = new[] { "Damascus", "Aleppo", "Homs", "Latakia", "Tartus" };
            var citiesAr = new[] { "دمشق", "حلب", "حمص", "اللاذقية", "طرطوس" };

            foreach (var user in users)
            {
                var addressCount = _random.Next(1, 3); // 1-2 addresses per user
                var createdAt = user.CreatedAt.AddDays(_random.Next(1, 30));

                for (int i = 0; i < addressCount; i++)
                {
                    var cityIndex = _random.Next(cities.Length);
                    shippingAddresses.Add(new ShippingAddress
                    {
                        FullName = user.UserName?.Split('@')[0] ?? "User",
                        PhoneNumber = user.PhoneNumber ?? "+963123456789",
                        AddressLine1 = $"Street {_random.Next(1, 100)}, Building {_random.Next(1, 50)}",
                        AddressLine2 = i == 0 ? null : $"Apartment {_random.Next(1, 20)}",
                        City = cities[cityIndex],
                        State = "Syria",
                        PostalCode = $"{_random.Next(10000, 99999)}",
                        Country = "Syria",
                        IsDefault = i == 0,
                        AppUserId = user.Id,
                        CreatedAt = createdAt.AddDays(i),
                        IsActive = true
                    });
                }
            }

            await context.ShippingAddresses.AddRangeAsync(shippingAddresses);
            await context.SaveChangesAsync();
        }

        private static async Task SeedRatingsAsync(EcomDbContext context)
        {
            if (await context.Ratings.AnyAsync())
            {
                return;
            }

            var products = await context.Products.ToListAsync();
            if (!products.Any())
            {
                return;
            }

            var ratings = new List<Rating>();

            foreach (var product in products)
            {
                var ratingCount = _random.Next(3, 8); // 3-7 ratings per product
                var baseDate = product.CreatedAt.AddDays(_random.Next(7, 60));

                for (int i = 0; i < ratingCount; i++)
                {
                    ratings.Add(new Rating
                    {
                        Content = $"Great product! {i + 1}",
                        RatingNumber = Math.Round((_random.NextDouble() * 2 + 3), 1), // 3.0 to 5.0
                        ProductId = product.Id,
                        CreatedAt = baseDate.AddDays(i * _random.Next(1, 10)),
                        IsActive = true
                    });
                }
            }

            await context.Ratings.AddRangeAsync(ratings);
            await context.SaveChangesAsync();
        }

        private static async Task SeedCouponsAsync(EcomDbContext context)
        {
            // Check if coupons already exist
            if (await context.Coupons.AnyAsync())
            {
                return; // Don't seed if coupons already exist
            }

            var baseDate = DateTime.UtcNow;
            var coupons = new List<Coupon>
            {
                // Percentage Discount Coupons with varied dates
                new Coupon
                {
                    Code = "WELCOME10",
                    Description = "Welcome discount - 10% off your first order",
                    Type = CouponType.Percentage,
                    Value = 10,
                    MinimumOrderAmount = 50,
                    MaximumDiscountAmount = 25,
                    StartDate = GetRandomDate(2023, 6),
                    EndDate = GetRandomDate(2023, 9),
                    UsageLimit = 1000,
                    CreatedAt = GetRandomDate(2023, 6),
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
                    StartDate = GetRandomDate(2023, 8),
                    EndDate = GetRandomDate(2024, 2),
                    UsageLimit = 500,
                    CreatedAt = GetRandomDate(2023, 8),
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
                    StartDate = GetRandomDate(2024, 5),
                    EndDate = GetRandomDate(2024, 6),
                    UsageLimit = 200,
                    CreatedAt = GetRandomDate(2024, 5),
                    IsActive = true,
                    IsSingleUse = false
                },
                new Coupon
                {
                    Code = "SPRING2024",
                    Description = "Spring sale - 15% off",
                    Type = CouponType.Percentage,
                    Value = 15,
                    MinimumOrderAmount = 40,
                    MaximumDiscountAmount = 30,
                    StartDate = GetRandomDate(2024, 3),
                    EndDate = GetRandomDate(2024, 5),
                    UsageLimit = 800,
                    CreatedAt = GetRandomDate(2024, 3),
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
                    StartDate = GetRandomDate(2022, 12),
                    EndDate = GetRandomDate(2024, 12),
                    UsageLimit = 2000,
                    CreatedAt = GetRandomDate(2022, 12),
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
                    StartDate = GetRandomDate(2023, 11),
                    EndDate = GetRandomDate(2024, 5),
                    UsageLimit = 300,
                    CreatedAt = GetRandomDate(2023, 11),
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
                    StartDate = GetRandomDate(2024, 1),
                    EndDate = GetRandomDate(2024, 4),
                    UsageLimit = 100,
                    CreatedAt = GetRandomDate(2024, 1),
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
                    StartDate = GetRandomDate(2023, 9),
                    EndDate = GetRandomDate(2024, 3),
                    UsageLimit = 1000,
                    CreatedAt = GetRandomDate(2023, 9),
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
                    StartDate = GetRandomDate(2023, 7),
                    EndDate = GetRandomDate(2024, 7),
                    UsageLimit = 5000,
                    CreatedAt = GetRandomDate(2023, 7),
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
                    StartDate = GetRandomDate(2024, 4),
                    EndDate = GetRandomDate(2024, 5),
                    UsageLimit = 1,
                    CreatedAt = GetRandomDate(2024, 4),
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
                    StartDate = GetRandomDate(2024, 2),
                    EndDate = GetRandomDate(2024, 4),
                    UsageLimit = 1,
                    CreatedAt = GetRandomDate(2024, 2),
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
                    StartDate = GetRandomDate(2022, 10),
                    EndDate = GetRandomDate(2023, 1),
                    UsageLimit = 100,
                    CreatedAt = GetRandomDate(2022, 10),
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
                    StartDate = baseDate.AddDays(7),
                    EndDate = baseDate.AddMonths(1),
                    UsageLimit = 200,
                    CreatedAt = baseDate,
                    IsActive = true,
                    IsSingleUse = false
                },

                // Additional coupons with varied dates
                new Coupon
                {
                    Code = "SUMMER2023",
                    Description = "Summer sale 2023 - 25% off",
                    Type = CouponType.Percentage,
                    Value = 25,
                    MinimumOrderAmount = 80,
                    MaximumDiscountAmount = 60,
                    StartDate = GetRandomDate(2023, 6),
                    EndDate = GetRandomDate(2023, 8),
                    UsageLimit = 500,
                    CreatedAt = GetRandomDate(2023, 6),
                    IsActive = false,
                    IsSingleUse = false
                },
                new Coupon
                {
                    Code = "WINTER2023",
                    Description = "Winter sale 2023 - 20% off",
                    Type = CouponType.Percentage,
                    Value = 20,
                    MinimumOrderAmount = 60,
                    MaximumDiscountAmount = 40,
                    StartDate = GetRandomDate(2023, 12),
                    EndDate = GetRandomDate(2024, 1),
                    UsageLimit = 600,
                    CreatedAt = GetRandomDate(2023, 12),
                    IsActive = false,
                    IsSingleUse = false
                }
            };

            await context.Coupons.AddRangeAsync(coupons);
            await context.SaveChangesAsync();
        }

        private static async Task SeedOrdersAsync(EcomDbContext context)
        {
            if (await context.Orders.AnyAsync())
            {
                return;
            }

            var users = await context.Set<AppUsers>().ToListAsync();
            var shippingAddresses = await context.ShippingAddresses.ToListAsync();
            var coupons = await context.Coupons.Where(c => c.IsActive).ToListAsync();

            if (!users.Any() || !shippingAddresses.Any())
            {
                return;
            }

            var orders = new List<Order>();
            var orderNumberCounter = 1000;

            // Generate orders spread across different months and years (minimum 15 orders with varied dates)
            var orderDates = new List<DateTime>();
            var months = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            var years = new[] { 2022, 2023, 2024 };

            // Create at least 15 unique date combinations
            for (int i = 0; i < 25; i++)
            {
                var year = years[_random.Next(years.Length)];
                var month = months[_random.Next(months.Length)];
                var day = _random.Next(1, 28);
                var hour = _random.Next(0, 24);
                var minute = _random.Next(0, 60);
                orderDates.Add(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc));
            }

            // Remove duplicates and ensure we have at least 15 unique dates
            orderDates = orderDates.Distinct().OrderBy(d => d).ToList();
            while (orderDates.Count < 15)
            {
                var year = years[_random.Next(years.Length)];
                var month = months[_random.Next(months.Length)];
                var day = _random.Next(1, 28);
                var hour = _random.Next(0, 24);
                var minute = _random.Next(0, 60);
                var newDate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
                if (!orderDates.Contains(newDate))
                {
                    orderDates.Add(newDate);
                }
            }

            int dateIndex = 0;
            var allUsers = users.ToList();
            
            // Ensure minimum of 15 orders across different dates
            for (int orderIdx = 0; orderIdx < 15; orderIdx++)
            {
                var user = allUsers[_random.Next(allUsers.Count)];
                var userAddresses = shippingAddresses.Where(a => a.AppUserId == user.Id).ToList();
                if (!userAddresses.Any()) 
                {
                    userAddresses = shippingAddresses.Take(1).ToList(); // Fallback to any address
                }

                var orderDate = orderDates[dateIndex % orderDates.Count];
                dateIndex++;

                var subtotal = (decimal)_random.Next(50, 500);
                var shipping = (decimal)_random.Next(5, 25);
                var tax = subtotal * 0.10m; // 10% tax
                var coupon = _random.Next(100) > 60 ? coupons.OrderBy(x => _random.Next()).FirstOrDefault() : null; // 40% chance
                var couponDiscount = coupon != null ? (coupon.Type == CouponType.Percentage ? subtotal * (coupon.Value / 100m) : coupon.Value) : 0;
                if (couponDiscount > coupon?.MaximumDiscountAmount) couponDiscount = coupon.MaximumDiscountAmount ?? couponDiscount;
                var total = subtotal + shipping + tax - couponDiscount;

                var statuses = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>().ToArray();
                var status = statuses[_random.Next(statuses.Length)];

                orders.Add(new Order
                {
                    OrderNumber = $"ORD{orderNumberCounter++}",
                    Status = status,
                    Subtotal = subtotal,
                    Shipping = shipping,
                    Tax = tax,
                    Total = total,
                    CouponId = coupon?.Id,
                    CouponDiscountAmount = couponDiscount > 0 ? couponDiscount : null,
                    ShippingAddressId = userAddresses[_random.Next(userAddresses.Count)].Id,
                    AppUserId = user.Id,
                    CreatedAt = orderDate,
                    UpdatedAt = status != OrderStatus.Pending ? orderDate.AddDays(_random.Next(1, 30)) : null,
                    IsActive = true
                });
            }

            // Add additional orders for users who have addresses (optional extra orders)
            foreach (var user in allUsers.Take(8))
            {
                var userAddresses = shippingAddresses.Where(a => a.AppUserId == user.Id).ToList();
                if (!userAddresses.Any()) continue;

                var orderCount = _random.Next(1, 3); // 1-2 extra orders per user
                for (int i = 0; i < orderCount; i++)
                {
                    var orderDate = GetRandomDate(years[_random.Next(years.Length)], months[_random.Next(months.Length)]);

                    var subtotal = (decimal)_random.Next(50, 500);
                    var shipping = (decimal)_random.Next(5, 25);
                    var tax = subtotal * 0.10m;
                    var coupon = _random.Next(100) > 60 ? coupons.OrderBy(x => _random.Next()).FirstOrDefault() : null;
                    var couponDiscount = coupon != null ? (coupon.Type == CouponType.Percentage ? subtotal * (coupon.Value / 100m) : coupon.Value) : 0;
                    if (couponDiscount > coupon?.MaximumDiscountAmount) couponDiscount = coupon.MaximumDiscountAmount ?? couponDiscount;
                    var total = subtotal + shipping + tax - couponDiscount;

                    var statuses = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>().ToArray();
                    var status = statuses[_random.Next(statuses.Length)];

                    orders.Add(new Order
                    {
                        OrderNumber = $"ORD{orderNumberCounter++}",
                        Status = status,
                        Subtotal = subtotal,
                        Shipping = shipping,
                        Tax = tax,
                        Total = total,
                        CouponId = coupon?.Id,
                        CouponDiscountAmount = couponDiscount > 0 ? couponDiscount : null,
                        ShippingAddressId = userAddresses[_random.Next(userAddresses.Count)].Id,
                        AppUserId = user.Id,
                        CreatedAt = orderDate,
                        UpdatedAt = status != OrderStatus.Pending ? orderDate.AddDays(_random.Next(1, 30)) : null,
                        IsActive = true
                    });
                }
            }

            await context.Orders.AddRangeAsync(orders);
            await context.SaveChangesAsync();
        }

        private static async Task SeedOrderItemsAsync(EcomDbContext context)
        {
            if (await context.OrderItems.AnyAsync())
            {
                return;
            }

            var orders = await context.Orders.ToListAsync();
            var products = await context.Products.ToListAsync();

            if (!orders.Any() || !products.Any())
            {
                return;
            }

            var orderItems = new List<OrderItem>();

            foreach (var order in orders)
            {
                var itemCount = _random.Next(1, 5); // 1-4 items per order
                var selectedProducts = products.OrderBy(x => _random.Next()).Take(itemCount).ToList();

                foreach (var product in selectedProducts)
                {
                    var quantity = _random.Next(1, 4);
                    var createdAt = order.CreatedAt.AddMinutes(_random.Next(1, 60));

                    orderItems.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = product.Id,
                        Name = product.Title,
                        Image = product.Images.FirstOrDefault() ?? "default.jpg",
                        Price = product.newPrice,
                        Quantity = quantity,
                        CreatedAt = createdAt,
                        IsActive = true
                    });
                }
            }

            await context.OrderItems.AddRangeAsync(orderItems);
            await context.SaveChangesAsync();
        }

        private static async Task SeedTransactionsAsync(EcomDbContext context)
        {
            if (await context.Transactions.AnyAsync())
            {
                return;
            }

            var orders = await context.Orders.Include(o => o.AppUser).ToListAsync();
            if (!orders.Any())
            {
                return;
            }

            var transactions = new List<Transaction>();
            var paymentMethods = Enum.GetValues(typeof(PaymentMethod)).Cast<PaymentMethod>().ToArray();
            var statuses = new[] { "Completed", "Pending", "Failed", "Refunded" };

            // Generate varied transaction dates across different months and years
            var transactionDates = new List<DateTime>();
            var months = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            var years = new[] { 2022, 2023, 2024 };

            // Create at least 15 unique transaction date combinations
            for (int i = 0; i < 20; i++)
            {
                var year = years[_random.Next(years.Length)];
                var month = months[_random.Next(months.Length)];
                var day = _random.Next(1, 28);
                var hour = _random.Next(0, 24);
                var minute = _random.Next(0, 60);
                var second = _random.Next(0, 60);
                transactionDates.Add(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
            }

            // Remove duplicates and ensure we have at least 15 unique dates
            transactionDates = transactionDates.Distinct().OrderBy(d => d).ToList();
            while (transactionDates.Count < 15)
            {
                var year = years[_random.Next(years.Length)];
                var month = months[_random.Next(months.Length)];
                var day = _random.Next(1, 28);
                var hour = _random.Next(0, 24);
                var minute = _random.Next(0, 60);
                var second = _random.Next(0, 60);
                var newDate = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
                if (!transactionDates.Contains(newDate))
                {
                    transactionDates.Add(newDate);
                }
            }

            int dateIndex = 0;

            // Ensure minimum of 15 transactions with varied dates
            // Some orders will have multiple transactions (e.g., failed attempts, then success)
            var ordersForTransactions = orders.OrderBy(x => _random.Next()).Take(Math.Max(15, orders.Count)).ToList();
            int transactionCount = 0;

            foreach (var order in ordersForTransactions)
            {
                // Most orders have 1 transaction, but some have 2 (e.g., failed then successful)
                var transactionCountForOrder = transactionCount < 10 ? 1 : (_random.Next(100) > 85 ? 2 : 1);
                
                for (int txnIdx = 0; txnIdx < transactionCountForOrder; txnIdx++)
                {
                    var paymentMethod = paymentMethods[_random.Next(paymentMethods.Length)];
                    
                    // First transaction might fail, second one should succeed
                    var status = txnIdx == 0 && transactionCountForOrder > 1 && _random.Next(100) > 50
                        ? "Failed" 
                        : statuses[_random.Next(statuses.Length)];
                    
                    // Use varied dates for transactions
                    DateTime transactionDate;
                    if (dateIndex < transactionDates.Count)
                    {
                        transactionDate = transactionDates[dateIndex % transactionDates.Count];
                        dateIndex++;
                    }
                    else
                    {
                        // For additional transactions, create dates relative to order date
                        transactionDate = order.CreatedAt.AddMinutes(_random.Next(5, 120));
                    }

                    var transaction = new Transaction
                    {
                        OrderId = order.Id,
                        AppUserId = order.AppUserId,
                        Amount = order.Total,
                        PaymentMethod = paymentMethod,
                        Status = status,
                        TransactionDate = transactionDate,
                        ProcessedDate = status == "Completed" ? transactionDate.AddMinutes(_random.Next(1, 60)) : null,
                        TransactionReference = $"TXN{_random.Next(100000, 999999)}",
                        PaymentGatewayResponse = status == "Completed" ? "Success" : status == "Failed" ? "Payment failed" : "Pending",
                        Notes = status == "Refunded" ? "Customer requested refund" : (status == "Failed" && txnIdx == 0) ? "Initial payment attempt failed" : null,
                        IsRefunded = status == "Refunded",
                        RefundAmount = status == "Refunded" ? order.Total : null,
                        RefundDate = status == "Refunded" ? transactionDate.AddDays(_random.Next(1, 7)) : null,
                        RefundReason = status == "Refunded" ? "Customer request" : null,
                        CreatedAt = transactionDate,
                        UpdatedAt = status != "Pending" ? transactionDate.AddMinutes(_random.Next(1, 30)) : null,
                        IsActive = true
                    };

                    transactions.Add(transaction);
                    transactionCount++;
                }

                // Ensure we have at least 15 transactions
                if (transactionCount >= 15 && dateIndex >= transactionDates.Count)
                {
                    break;
                }
            }

            // If we still don't have enough transactions, add more for remaining orders
            while (transactions.Count < 15 && transactionCount < orders.Count)
            {
                var order = orders[transactionCount % orders.Count];
                var paymentMethod = paymentMethods[_random.Next(paymentMethods.Length)];
                var status = statuses[_random.Next(statuses.Length)];
                var transactionDate = GetRandomDate(years[_random.Next(years.Length)], months[_random.Next(months.Length)]);

                var transaction = new Transaction
                {
                    OrderId = order.Id,
                    AppUserId = order.AppUserId,
                    Amount = order.Total,
                    PaymentMethod = paymentMethod,
                    Status = status,
                    TransactionDate = transactionDate,
                    ProcessedDate = status == "Completed" ? transactionDate.AddMinutes(_random.Next(1, 60)) : null,
                    TransactionReference = $"TXN{_random.Next(100000, 999999)}",
                    PaymentGatewayResponse = status == "Completed" ? "Success" : status == "Failed" ? "Payment failed" : "Pending",
                    Notes = status == "Refunded" ? "Customer requested refund" : null,
                    IsRefunded = status == "Refunded",
                    RefundAmount = status == "Refunded" ? order.Total : null,
                    RefundDate = status == "Refunded" ? transactionDate.AddDays(_random.Next(1, 7)) : null,
                    RefundReason = status == "Refunded" ? "Customer request" : null,
                    CreatedAt = transactionDate,
                    UpdatedAt = status != "Pending" ? transactionDate.AddMinutes(_random.Next(1, 30)) : null,
                    IsActive = true
                };

                transactions.Add(transaction);
                transactionCount++;
            }

            await context.Transactions.AddRangeAsync(transactions);
            await context.SaveChangesAsync();
        }

        private static async Task SeedVisitorsAsync(EcomDbContext context)
        {
            if (await context.Visitors.AnyAsync())
            {
                return;
            }

            var visitors = new List<Visitor>();
            var start = DateTime.UtcNow.Date.AddDays(-60);
            var paths = new[] { "/", "/home", "/products", "/products/1", "/cart", "/checkout", "/categories/1", "/login" };
            var ipBase = new[] { "192.168.1.", "10.0.0.", "172.16.0." };

            for (int d = 0; d <= 60; d++)
            {
                var day = start.AddDays(d);
                var count = _random.Next(5, 40);
                for (int i = 0; i < count; i++)
                {
                    var ts = day.AddMinutes(_random.Next(0, 24 * 60));
                    visitors.Add(new Visitor
                    {
                        IpAddress = ipBase[_random.Next(ipBase.Length)] + _random.Next(2, 254),
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                        Path = paths[_random.Next(paths.Length)],
                        VisitedAtUtc = ts,
                        CreatedAt = ts,
                        IsActive = true
                    });
                }
            }

            await context.Visitors.AddRangeAsync(visitors);
            await context.SaveChangesAsync();
        }

        private static async Task SeedHealthPingsAsync(EcomDbContext context)
        {
            if (await context.HealthPings.AnyAsync())
            {
                return;
            }

            var pings = new List<HealthPing>();
            var start = DateTime.UtcNow.AddHours(-24);
            for (int m = 0; m <= 24 * 60; m++)
            {
                var t = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0, DateTimeKind.Utc).AddMinutes(m);
                var healthy = _random.Next(100) > 3; // ~97% healthy
                pings.Add(new HealthPing
                {
                    IsHealthy = healthy,
                    Status = healthy ? "Healthy" : "Unhealthy",
                    Error = healthy ? null : "Simulated transient error",
                    CreatedAt = t,
                    IsActive = true
                });
            }

            await context.HealthPings.AddRangeAsync(pings);
            await context.SaveChangesAsync();
        }
    }
}
