# Observed legacy-to-current column mapping

Generated from the actual VPS test transfer catalog on 2026-10-18. Names are preserved exactly. See the main report for new nullable fields, empty tables, timestamp precision archive and provider conversions.

| SQL Server dbo column | Source definition | PostgreSQL public column | Target type |
|---|---|---|---|
| AspNetRoleClaims.Id | int length=4 precision=10 scale=0 nullable=False identity=True | AspNetRoleClaims.Id | integer |
| AspNetRoleClaims.RoleId | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetRoleClaims.RoleId | text |
| AspNetRoleClaims.ClaimType | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetRoleClaims.ClaimType | text |
| AspNetRoleClaims.ClaimValue | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetRoleClaims.ClaimValue | text |
| AspNetRoles.Id | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetRoles.Id | text |
| AspNetRoles.Name | nvarchar length=512 precision=0 scale=0 nullable=True identity=False | AspNetRoles.Name | character varying(256) |
| AspNetRoles.NormalizedName | nvarchar length=512 precision=0 scale=0 nullable=True identity=False | AspNetRoles.NormalizedName | character varying(256) |
| AspNetRoles.ConcurrencyStamp | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetRoles.ConcurrencyStamp | text |
| AspNetUserClaims.Id | int length=4 precision=10 scale=0 nullable=False identity=True | AspNetUserClaims.Id | integer |
| AspNetUserClaims.UserId | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserClaims.UserId | text |
| AspNetUserClaims.ClaimType | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUserClaims.ClaimType | text |
| AspNetUserClaims.ClaimValue | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUserClaims.ClaimValue | text |
| AspNetUserLogins.LoginProvider | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserLogins.LoginProvider | text |
| AspNetUserLogins.ProviderKey | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserLogins.ProviderKey | text |
| AspNetUserLogins.ProviderDisplayName | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUserLogins.ProviderDisplayName | text |
| AspNetUserLogins.UserId | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserLogins.UserId | text |
| AspNetUserRoles.UserId | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserRoles.UserId | text |
| AspNetUserRoles.RoleId | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserRoles.RoleId | text |
| AspNetUsers.Id | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUsers.Id | text |
| AspNetUsers.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | AspNetUsers.CreatedAt | timestamp with time zone |
| AspNetUsers.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | AspNetUsers.IsActive | boolean |
| AspNetUsers.ShippingAddressesId | int length=4 precision=10 scale=0 nullable=True identity=False | AspNetUsers.ShippingAddressesId | integer |
| AspNetUsers.RefreshToken | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUsers.RefreshToken | text |
| AspNetUsers.RefreshTokenExpiresAtUtc | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | AspNetUsers.RefreshTokenExpiresAtUtc | timestamp with time zone |
| AspNetUsers.UserName | nvarchar length=512 precision=0 scale=0 nullable=True identity=False | AspNetUsers.UserName | character varying(256) |
| AspNetUsers.NormalizedUserName | nvarchar length=512 precision=0 scale=0 nullable=True identity=False | AspNetUsers.NormalizedUserName | character varying(256) |
| AspNetUsers.Email | nvarchar length=512 precision=0 scale=0 nullable=True identity=False | AspNetUsers.Email | character varying(256) |
| AspNetUsers.NormalizedEmail | nvarchar length=512 precision=0 scale=0 nullable=True identity=False | AspNetUsers.NormalizedEmail | character varying(256) |
| AspNetUsers.EmailConfirmed | bit length=1 precision=1 scale=0 nullable=False identity=False | AspNetUsers.EmailConfirmed | boolean |
| AspNetUsers.PasswordHash | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUsers.PasswordHash | text |
| AspNetUsers.SecurityStamp | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUsers.SecurityStamp | text |
| AspNetUsers.ConcurrencyStamp | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUsers.ConcurrencyStamp | text |
| AspNetUsers.PhoneNumber | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUsers.PhoneNumber | text |
| AspNetUsers.PhoneNumberConfirmed | bit length=1 precision=1 scale=0 nullable=False identity=False | AspNetUsers.PhoneNumberConfirmed | boolean |
| AspNetUsers.TwoFactorEnabled | bit length=1 precision=1 scale=0 nullable=False identity=False | AspNetUsers.TwoFactorEnabled | boolean |
| AspNetUsers.LockoutEnd | datetimeoffset length=10 precision=34 scale=7 nullable=True identity=False | AspNetUsers.LockoutEnd | timestamp with time zone |
| AspNetUsers.LockoutEnabled | bit length=1 precision=1 scale=0 nullable=False identity=False | AspNetUsers.LockoutEnabled | boolean |
| AspNetUsers.AccessFailedCount | int length=4 precision=10 scale=0 nullable=False identity=False | AspNetUsers.AccessFailedCount | integer |
| AspNetUserTokens.UserId | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserTokens.UserId | text |
| AspNetUserTokens.LoginProvider | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserTokens.LoginProvider | text |
| AspNetUserTokens.Name | nvarchar length=900 precision=0 scale=0 nullable=False identity=False | AspNetUserTokens.Name | text |
| AspNetUserTokens.Value | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | AspNetUserTokens.Value | text |
| Carousels.Id | int length=4 precision=10 scale=0 nullable=False identity=True | Carousels.Id | integer |
| Carousels.Title | nvarchar length=400 precision=0 scale=0 nullable=False identity=False | Carousels.Title | character varying(200) |
| Carousels.Description | nvarchar length=2000 precision=0 scale=0 nullable=False identity=False | Carousels.Description | character varying(1000) |
| Carousels.Price | decimal length=9 precision=18 scale=2 nullable=False identity=False | Carousels.Price | numeric(18,3) |
| Carousels.Image | nvarchar length=1000 precision=0 scale=0 nullable=False identity=False | Carousels.Image | character varying(500) |
| Carousels.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Carousels.CreatedAt | timestamp with time zone |
| Carousels.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Carousels.UpdatedAt | timestamp with time zone |
| Carousels.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Carousels.CreatedBy | text |
| Carousels.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Carousels.UpdatedBy | text |
| Carousels.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | Carousels.IsDeleted | boolean |
| Carousels.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | Carousels.IsActive | boolean |
| Carousels.DescriptionAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Carousels.DescriptionAr | text |
| Carousels.TitleAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Carousels.TitleAr | text |
| Carousels.ProductUrl | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Carousels.ProductUrl | text |
| Categories.Id | int length=4 precision=10 scale=0 nullable=False identity=True | Categories.Id | integer |
| Categories.Name | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | Categories.Name | character varying(100) |
| Categories.NameAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Categories.NameAr | text |
| Categories.Description | nvarchar length=1000 precision=0 scale=0 nullable=False identity=False | Categories.Description | character varying(500) |
| Categories.DescriptionAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Categories.DescriptionAr | text |
| Categories.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Categories.CreatedAt | timestamp with time zone |
| Categories.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Categories.UpdatedAt | timestamp with time zone |
| Categories.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Categories.CreatedBy | text |
| Categories.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Categories.UpdatedBy | text |
| Categories.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | Categories.IsDeleted | boolean |
| Categories.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | Categories.IsActive | boolean |
| Categories.Image | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Categories.Image | text |
| Coupons.Id | int length=4 precision=10 scale=0 nullable=False identity=True | Coupons.Id | integer |
| Coupons.Code | nvarchar length=100 precision=0 scale=0 nullable=False identity=False | Coupons.Code | character varying(50) |
| Coupons.Description | nvarchar length=1000 precision=0 scale=0 nullable=False identity=False | Coupons.Description | character varying(500) |
| Coupons.Type | int length=4 precision=10 scale=0 nullable=False identity=False | Coupons.Type | integer |
| Coupons.Value | decimal length=9 precision=18 scale=2 nullable=False identity=False | Coupons.Value | numeric(18,3) |
| Coupons.MinimumOrderAmount | decimal length=9 precision=18 scale=2 nullable=True identity=False | Coupons.MinimumOrderAmount | numeric(18,3) |
| Coupons.MaximumDiscountAmount | decimal length=9 precision=18 scale=2 nullable=True identity=False | Coupons.MaximumDiscountAmount | numeric(18,3) |
| Coupons.StartDate | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Coupons.StartDate | timestamp with time zone |
| Coupons.EndDate | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Coupons.EndDate | timestamp with time zone |
| Coupons.UsageLimit | int length=4 precision=10 scale=0 nullable=True identity=False | Coupons.UsageLimit | integer |
| Coupons.UsedCount | int length=4 precision=10 scale=0 nullable=False identity=False | Coupons.UsedCount | integer |
| Coupons.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | Coupons.IsActive | boolean |
| Coupons.IsSingleUse | bit length=1 precision=1 scale=0 nullable=False identity=False | Coupons.IsSingleUse | boolean |
| Coupons.AppUserId | nvarchar length=900 precision=0 scale=0 nullable=True identity=False | Coupons.AppUserId | text |
| Coupons.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Coupons.CreatedAt | timestamp with time zone |
| Coupons.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Coupons.UpdatedAt | timestamp with time zone |
| Coupons.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Coupons.CreatedBy | text |
| Coupons.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Coupons.UpdatedBy | text |
| Coupons.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | Coupons.IsDeleted | boolean |
| HealthPings.Id | int length=4 precision=10 scale=0 nullable=False identity=True | HealthPings.Id | integer |
| HealthPings.IsHealthy | bit length=1 precision=1 scale=0 nullable=False identity=False | HealthPings.IsHealthy | boolean |
| HealthPings.Status | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | HealthPings.Status | character varying(100) |
| HealthPings.Error | nvarchar length=2048 precision=0 scale=0 nullable=True identity=False | HealthPings.Error | character varying(1024) |
| HealthPings.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | HealthPings.CreatedAt | timestamp with time zone |
| HealthPings.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | HealthPings.UpdatedAt | timestamp with time zone |
| HealthPings.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | HealthPings.CreatedBy | text |
| HealthPings.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | HealthPings.UpdatedBy | text |
| HealthPings.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | HealthPings.IsDeleted | boolean |
| HealthPings.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | HealthPings.IsActive | boolean |
| OrderItems.Id | int length=4 precision=10 scale=0 nullable=False identity=True | OrderItems.Id | integer |
| OrderItems.ProductId | int length=4 precision=10 scale=0 nullable=False identity=False | OrderItems.ProductId | integer |
| OrderItems.OrderId | int length=4 precision=10 scale=0 nullable=False identity=False | OrderItems.OrderId | integer |
| OrderItems.Name | nvarchar length=400 precision=0 scale=0 nullable=False identity=False | OrderItems.Name | character varying(200) |
| OrderItems.Image | nvarchar length=1000 precision=0 scale=0 nullable=False identity=False | OrderItems.Image | character varying(500) |
| OrderItems.Price | decimal length=9 precision=18 scale=2 nullable=False identity=False | OrderItems.Price | numeric(18,3) |
| OrderItems.Quantity | int length=4 precision=10 scale=0 nullable=False identity=False | OrderItems.Quantity | integer |
| OrderItems.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | OrderItems.CreatedAt | timestamp with time zone |
| OrderItems.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | OrderItems.UpdatedAt | timestamp with time zone |
| OrderItems.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | OrderItems.CreatedBy | text |
| OrderItems.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | OrderItems.UpdatedBy | text |
| OrderItems.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | OrderItems.IsDeleted | boolean |
| OrderItems.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | OrderItems.IsActive | boolean |
| Orders.Id | int length=4 precision=10 scale=0 nullable=False identity=True | Orders.Id | integer |
| Orders.OrderNumber | nvarchar length=100 precision=0 scale=0 nullable=False identity=False | Orders.OrderNumber | character varying(50) |
| Orders.Status | int length=4 precision=10 scale=0 nullable=False identity=False | Orders.Status | integer |
| Orders.Subtotal | decimal length=9 precision=18 scale=2 nullable=False identity=False | Orders.Subtotal | numeric(18,3) |
| Orders.Shipping | decimal length=9 precision=18 scale=2 nullable=False identity=False | Orders.Shipping | numeric(18,3) |
| Orders.Tax | decimal length=9 precision=18 scale=2 nullable=False identity=False | Orders.Tax | numeric(18,3) |
| Orders.Total | decimal length=9 precision=18 scale=2 nullable=False identity=False | Orders.Total | numeric(18,3) |
| Orders.CouponId | int length=4 precision=10 scale=0 nullable=True identity=False | Orders.CouponId | integer |
| Orders.CouponDiscountAmount | decimal length=9 precision=18 scale=2 nullable=True identity=False | Orders.CouponDiscountAmount | numeric(18,3) |
| Orders.ShippingAddressId | int length=4 precision=10 scale=0 nullable=False identity=False | Orders.ShippingAddressId | integer |
| Orders.AppUserId | nvarchar length=900 precision=0 scale=0 nullable=True identity=False | Orders.AppUserId | text |
| Orders.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Orders.CreatedAt | timestamp with time zone |
| Orders.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Orders.UpdatedAt | timestamp with time zone |
| Orders.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Orders.CreatedBy | text |
| Orders.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Orders.UpdatedBy | text |
| Orders.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | Orders.IsDeleted | boolean |
| Orders.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | Orders.IsActive | boolean |
| ProductDetails.Id | int length=4 precision=10 scale=0 nullable=False identity=True | ProductDetails.Id | integer |
| ProductDetails.Label | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ProductDetails.Label | character varying(100) |
| ProductDetails.LabelAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | ProductDetails.LabelAr | text |
| ProductDetails.Value | nvarchar length=1000 precision=0 scale=0 nullable=False identity=False | ProductDetails.Value | character varying(500) |
| ProductDetails.ValueAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | ProductDetails.ValueAr | text |
| ProductDetails.ProductId | int length=4 precision=10 scale=0 nullable=False identity=False | ProductDetails.ProductId | integer |
| ProductDetails.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | ProductDetails.CreatedAt | timestamp with time zone |
| ProductDetails.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | ProductDetails.UpdatedAt | timestamp with time zone |
| ProductDetails.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | ProductDetails.CreatedBy | text |
| ProductDetails.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ProductDetails.UpdatedBy | text |
| ProductDetails.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | ProductDetails.IsDeleted | boolean |
| ProductDetails.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | ProductDetails.IsActive | boolean |
| Products.Id | int length=4 precision=10 scale=0 nullable=False identity=True | Products.Id | integer |
| Products.Title | nvarchar length=400 precision=0 scale=0 nullable=False identity=False | Products.Title | character varying(200) |
| Products.TitleAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Products.TitleAr | text |
| Products.Description | nvarchar length=4000 precision=0 scale=0 nullable=False identity=False | Products.Description | character varying(2000) |
| Products.DescriptionAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Products.DescriptionAr | text |
| Products.oldPrice | decimal length=9 precision=18 scale=2 nullable=False identity=False | Products.oldPrice | numeric(18,3) |
| Products.newPrice | decimal length=9 precision=18 scale=2 nullable=False identity=False | Products.newPrice | numeric(18,3) |
| Products.IsInStock | bit length=1 precision=1 scale=0 nullable=False identity=False | Products.IsInStock | boolean |
| Products.TotalInStock | int length=4 precision=10 scale=0 nullable=False identity=False | Products.TotalInStock | integer |
| Products.Images | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Products.Images | text[] |
| Products.SubCategoryId | int length=4 precision=10 scale=0 nullable=False identity=False | Products.SubCategoryId | integer |
| Products.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Products.CreatedAt | timestamp with time zone |
| Products.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Products.UpdatedAt | timestamp with time zone |
| Products.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Products.CreatedBy | text |
| Products.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Products.UpdatedBy | text |
| Products.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | Products.IsDeleted | boolean |
| Products.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | Products.IsActive | boolean |
| ProductVariants.Id | int length=4 precision=10 scale=0 nullable=False identity=True | ProductVariants.Id | integer |
| ProductVariants.Name | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ProductVariants.Name | character varying(100) |
| ProductVariants.NameAr | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ProductVariants.NameAr | character varying(100) |
| ProductVariants.ProductId | int length=4 precision=10 scale=0 nullable=False identity=False | ProductVariants.ProductId | integer |
| ProductVariants.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | ProductVariants.CreatedAt | timestamp with time zone |
| ProductVariants.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | ProductVariants.UpdatedAt | timestamp with time zone |
| ProductVariants.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | ProductVariants.CreatedBy | text |
| ProductVariants.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ProductVariants.UpdatedBy | text |
| ProductVariants.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | ProductVariants.IsDeleted | boolean |
| ProductVariants.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | ProductVariants.IsActive | boolean |
| ProductVariantValues.Id | int length=4 precision=10 scale=0 nullable=False identity=True | ProductVariantValues.Id | integer |
| ProductVariantValues.Value | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ProductVariantValues.Value | character varying(100) |
| ProductVariantValues.ValueAr | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ProductVariantValues.ValueAr | character varying(100) |
| ProductVariantValues.Price | decimal length=9 precision=18 scale=2 nullable=False identity=False | ProductVariantValues.Price | numeric(18,3) |
| ProductVariantValues.ProductVariantId | int length=4 precision=10 scale=0 nullable=False identity=False | ProductVariantValues.ProductVariantId | integer |
| ProductVariantValues.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | ProductVariantValues.CreatedAt | timestamp with time zone |
| ProductVariantValues.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | ProductVariantValues.UpdatedAt | timestamp with time zone |
| ProductVariantValues.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | ProductVariantValues.CreatedBy | text |
| ProductVariantValues.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ProductVariantValues.UpdatedBy | text |
| ProductVariantValues.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | ProductVariantValues.IsDeleted | boolean |
| ProductVariantValues.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | ProductVariantValues.IsActive | boolean |
| Ratings.Id | int length=4 precision=10 scale=0 nullable=False identity=True | Ratings.Id | integer |
| Ratings.Content | nvarchar length=2000 precision=0 scale=0 nullable=False identity=False | Ratings.Content | character varying(1000) |
| Ratings.RatingNumber | decimal length=5 precision=2 scale=1 nullable=False identity=False | Ratings.RatingNumber | numeric(2,1) |
| Ratings.ProductId | int length=4 precision=10 scale=0 nullable=False identity=False | Ratings.ProductId | integer |
| Ratings.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Ratings.CreatedAt | timestamp with time zone |
| Ratings.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Ratings.UpdatedAt | timestamp with time zone |
| Ratings.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Ratings.CreatedBy | text |
| Ratings.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Ratings.UpdatedBy | text |
| Ratings.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | Ratings.IsDeleted | boolean |
| Ratings.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | Ratings.IsActive | boolean |
| ShippingAddresses.Id | int length=4 precision=10 scale=0 nullable=False identity=True | ShippingAddresses.Id | integer |
| ShippingAddresses.FullName | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.FullName | character varying(100) |
| ShippingAddresses.PhoneNumber | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.PhoneNumber | text |
| ShippingAddresses.AddressLine1 | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.AddressLine1 | text |
| ShippingAddresses.AddressLine2 | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.AddressLine2 | text |
| ShippingAddresses.City | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.City | character varying(100) |
| ShippingAddresses.State | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.State | character varying(100) |
| ShippingAddresses.PostalCode | nvarchar length=40 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.PostalCode | character varying(20) |
| ShippingAddresses.Country | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.Country | character varying(100) |
| ShippingAddresses.IsDefault | bit length=1 precision=1 scale=0 nullable=False identity=False | ShippingAddresses.IsDefault | boolean |
| ShippingAddresses.AppUserId | nvarchar length=900 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.AppUserId | text |
| ShippingAddresses.Phone | nvarchar length=40 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.Phone | character varying(20) |
| ShippingAddresses.Street | nvarchar length=400 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.Street | character varying(200) |
| ShippingAddresses.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | ShippingAddresses.CreatedAt | timestamp with time zone |
| ShippingAddresses.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | ShippingAddresses.UpdatedAt | timestamp with time zone |
| ShippingAddresses.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | ShippingAddresses.CreatedBy | text |
| ShippingAddresses.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.UpdatedBy | text |
| ShippingAddresses.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | ShippingAddresses.IsDeleted | boolean |
| ShippingAddresses.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | ShippingAddresses.IsActive | boolean |
| ShippingAddresses.AlDor | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.AlDor | text |
| ShippingAddresses.AlJada | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.AlJada | text |
| ShippingAddresses.AlManzil | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.AlManzil | text |
| ShippingAddresses.AlQataa | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.AlQataa | text |
| ShippingAddresses.AlShakka | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.AlShakka | text |
| ShippingAddresses.AlSharee | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | ShippingAddresses.AlSharee | text |
| SubCategories.Id | int length=4 precision=10 scale=0 nullable=False identity=True | SubCategories.Id | integer |
| SubCategories.Name | nvarchar length=200 precision=0 scale=0 nullable=False identity=False | SubCategories.Name | character varying(100) |
| SubCategories.NameAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | SubCategories.NameAr | text |
| SubCategories.Description | nvarchar length=1000 precision=0 scale=0 nullable=False identity=False | SubCategories.Description | character varying(500) |
| SubCategories.DescriptionAr | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | SubCategories.DescriptionAr | text |
| SubCategories.CategoryId | int length=4 precision=10 scale=0 nullable=False identity=False | SubCategories.CategoryId | integer |
| SubCategories.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | SubCategories.CreatedAt | timestamp with time zone |
| SubCategories.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | SubCategories.UpdatedAt | timestamp with time zone |
| SubCategories.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | SubCategories.CreatedBy | text |
| SubCategories.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | SubCategories.UpdatedBy | text |
| SubCategories.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | SubCategories.IsDeleted | boolean |
| SubCategories.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | SubCategories.IsActive | boolean |
| Transactions.Id | int length=4 precision=10 scale=0 nullable=False identity=True | Transactions.Id | integer |
| Transactions.OrderId | int length=4 precision=10 scale=0 nullable=False identity=False | Transactions.OrderId | integer |
| Transactions.AppUserId | nvarchar length=900 precision=0 scale=0 nullable=True identity=False | Transactions.AppUserId | text |
| Transactions.Amount | decimal length=9 precision=18 scale=2 nullable=False identity=False | Transactions.Amount | numeric(18,3) |
| Transactions.PaymentMethod | int length=4 precision=10 scale=0 nullable=False identity=False | Transactions.PaymentMethod | integer |
| Transactions.TransactionDate | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Transactions.TransactionDate | timestamp with time zone |
| Transactions.ProcessedDate | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Transactions.ProcessedDate | timestamp with time zone |
| Transactions.TransactionReference | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Transactions.TransactionReference | text |
| Transactions.PaymentGatewayResponse | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Transactions.PaymentGatewayResponse | text |
| Transactions.Notes | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Transactions.Notes | text |
| Transactions.IsRefunded | bit length=1 precision=1 scale=0 nullable=False identity=False | Transactions.IsRefunded | boolean |
| Transactions.RefundAmount | decimal length=9 precision=18 scale=2 nullable=True identity=False | Transactions.RefundAmount | numeric(18,3) |
| Transactions.RefundDate | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Transactions.RefundDate | timestamp with time zone |
| Transactions.RefundReason | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Transactions.RefundReason | text |
| Transactions.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Transactions.CreatedAt | timestamp with time zone |
| Transactions.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Transactions.UpdatedAt | timestamp with time zone |
| Transactions.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Transactions.CreatedBy | text |
| Transactions.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Transactions.UpdatedBy | text |
| Transactions.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | Transactions.IsDeleted | boolean |
| Transactions.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | Transactions.IsActive | boolean |
| Transactions.GatewayInvoiceId | nvarchar length=200 precision=0 scale=0 nullable=True identity=False | Transactions.GatewayInvoiceId | character varying(100) |
| Transactions.Status | int length=4 precision=10 scale=0 nullable=False identity=False | Transactions.Status | integer |
| Visitors.Id | int length=4 precision=10 scale=0 nullable=False identity=True | Visitors.Id | integer |
| Visitors.IpAddress | nvarchar length=200 precision=0 scale=0 nullable=True identity=False | Visitors.IpAddress | character varying(100) |
| Visitors.UserAgent | nvarchar length=2048 precision=0 scale=0 nullable=True identity=False | Visitors.UserAgent | character varying(1024) |
| Visitors.Path | nvarchar length=1024 precision=0 scale=0 nullable=True identity=False | Visitors.Path | character varying(512) |
| Visitors.VisitedAtUtc | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Visitors.VisitedAtUtc | timestamp with time zone |
| Visitors.CreatedAt | datetime2 length=8 precision=27 scale=7 nullable=False identity=False | Visitors.CreatedAt | timestamp with time zone |
| Visitors.UpdatedAt | datetime2 length=8 precision=27 scale=7 nullable=True identity=False | Visitors.UpdatedAt | timestamp with time zone |
| Visitors.CreatedBy | nvarchar length=-1 precision=0 scale=0 nullable=False identity=False | Visitors.CreatedBy | text |
| Visitors.UpdatedBy | nvarchar length=-1 precision=0 scale=0 nullable=True identity=False | Visitors.UpdatedBy | text |
| Visitors.IsDeleted | bit length=1 precision=1 scale=0 nullable=False identity=False | Visitors.IsDeleted | boolean |
| Visitors.IsActive | bit length=1 precision=1 scale=0 nullable=False identity=False | Visitors.IsActive | boolean |
