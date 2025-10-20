using AutoMapper;
using Ecom.Application.DTOs.Category;
using Ecom.Application.DTOs.Coupon;
using Ecom.Application.DTOs.Order;
using Ecom.Application.DTOs.Product;
using Ecom.Domain.Entity;

namespace Ecom.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateProductMappings();
            CreateCategoryMappings();
            CreateOrderMappings();
            CreateCouponMappings();
        }

        private void CreateProductMappings()
        {
            CreateMap<Product, ProductDto>()
                .ForMember(dest => dest.OldPrice, opt => opt.MapFrom(src => src.oldPrice))
                .ForMember(dest => dest.NewPrice, opt => opt.MapFrom(src => src.newPrice))
                .ForMember(dest => dest.IsInStock, opt => opt.MapFrom(src => src.IsInStock))
                .ForMember(dest => dest.TotalInStock, opt => opt.MapFrom(src => src.TotalInStock))
                .ForMember(dest => dest.SubCategoryName, opt => opt.MapFrom(src => src.subCategory.Name))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.subCategory.Category.Name))
                .ForMember(dest => dest.ProductDetails, opt => opt.MapFrom(src => src.productDetails))
                .ForMember(dest => dest.AverageRating, opt => opt.MapFrom(src => src.Ratings.Any() ? src.Ratings.Average(r => r.RatingNumber) : 0))
                .ForMember(dest => dest.TotalReviews, opt => opt.MapFrom(src => src.Ratings.Count));

            CreateMap<Product, ProductSummaryDto>()
                .ForMember(dest => dest.OldPrice, opt => opt.MapFrom(src => src.oldPrice))
                .ForMember(dest => dest.NewPrice, opt => opt.MapFrom(src => src.newPrice))
                .ForMember(dest => dest.IsInStock, opt => opt.MapFrom(src => src.IsInStock))
                .ForMember(dest => dest.TotalInStock, opt => opt.MapFrom(src => src.TotalInStock))
                .ForMember(dest => dest.MainImage, opt => opt.MapFrom(src => src.Images.FirstOrDefault() ?? string.Empty))
                .ForMember(dest => dest.SubCategoryName, opt => opt.MapFrom(src => src.subCategory.Name))
                .ForMember(dest => dest.AverageRating, opt => opt.MapFrom(src => src.Ratings.Any() ? src.Ratings.Average(r => r.RatingNumber) : 0))
                .ForMember(dest => dest.TotalReviews, opt => opt.MapFrom(src => src.Ratings.Count));

            CreateMap<ProductCreateDto, Product>()
                .ForMember(dest => dest.oldPrice, opt => opt.MapFrom(src => src.OldPrice))
                .ForMember(dest => dest.newPrice, opt => opt.MapFrom(src => src.NewPrice))
                .ForMember(dest => dest.IsInStock, opt => opt.MapFrom(src => src.IsInStock))
                .ForMember(dest => dest.TotalInStock, opt => opt.MapFrom(src => src.TotalInStock))
                .ForMember(dest => dest.productDetails, opt => opt.MapFrom(src => src.ProductDetails));
            CreateMap<ProductCreateWithFilesDto, Product>()
           .ForMember(dest => dest.oldPrice, opt => opt.MapFrom(src => src.OldPrice))
           .ForMember(dest => dest.newPrice, opt => opt.MapFrom(src => src.NewPrice))
           .ForMember(dest => dest.IsInStock, opt => opt.MapFrom(src => src.IsInStock))
           .ForMember(dest => dest.TotalInStock, opt => opt.MapFrom(src => src.TotalInStock))
           .ForMember(dest => dest.productDetails, opt => opt.MapFrom(src => src.ProductDetails));

            CreateMap<ProductUpdateDto, Product>()
                .ForMember(dest => dest.oldPrice, opt => opt.MapFrom(src => src.OldPrice))
                .ForMember(dest => dest.newPrice, opt => opt.MapFrom(src => src.NewPrice))
                .ForMember(dest => dest.IsInStock, opt => opt.MapFrom(src => src.IsInStock))
                .ForMember(dest => dest.TotalInStock, opt => opt.MapFrom(src => src.TotalInStock))
                .ForMember(dest => dest.productDetails, opt => opt.MapFrom(src => src.ProductDetails));

            CreateMap<ProductDetails, ProductDetailDto>();
            CreateMap<ProductDetailCreateDto, ProductDetails>();

            CreateMap<Rating, RatingDto>()
                .ForMember(dest => dest.ProductTitle, opt => opt.MapFrom(src => src.Product.Title));
            CreateMap<RatingCreateDto, Rating>();
            CreateMap<RatingUpdateDto, Rating>();
        }

        private void CreateCategoryMappings()
        {
            CreateMap<Category, CategoryDto>()
                .ForMember(dest => dest.ProductCount, opt => opt.MapFrom(src => src.SubCategories.SelectMany(sc => sc.Products).Count()));

            CreateMap<CategoryCreateDto, Category>();
            CreateMap<CategoryUpdateDto, Category>();

            CreateMap<SubCategory, SubCategoryDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
                .ForMember(dest => dest.ProductCount, opt => opt.MapFrom(src => src.Products.Count));

            CreateMap<SubCategoryCreateDto, SubCategory>();
            CreateMap<SubCategoryUpdateDto, SubCategory>();
        }

        private void CreateOrderMappings()
        {
            CreateMap<Order, OrderDto>()
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.AppUser != null ? src.AppUser.UserName : "Guest"))
                .ForMember(dest => dest.CouponCode, opt => opt.MapFrom(src => src.Coupon != null ? src.Coupon.Code : string.Empty));

            CreateMap<Order, OrderSummaryDto>()
                .ForMember(dest => dest.ItemCount, opt => opt.MapFrom(src => src.Items.Count))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.AppUser != null ? src.AppUser.UserName : "Guest"));

            CreateMap<OrderCreateDto, Order>()
                .ForMember(dest => dest.OrderNumber, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => Domain.constant.OrderStatus.Pending))
                .ForMember(dest => dest.Subtotal, opt => opt.Ignore())
                .ForMember(dest => dest.Shipping, opt => opt.Ignore())
                .ForMember(dest => dest.Tax, opt => opt.Ignore())
                .ForMember(dest => dest.Total, opt => opt.Ignore());

            CreateMap<OrderItem, OrderItemDto>()
                .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.Price * src.Quantity));

            CreateMap<OrderItemCreateDto, OrderItem>();

            CreateMap<ShippingAddress, ShippingAddressDto>();
            CreateMap<ShippingAddressCreateDto, ShippingAddress>();
            CreateMap<ShippingAddressUpdateDto, ShippingAddress>();

            CreateMap<Transaction, TransactionDto>();
            CreateMap<TransactionCreateDto, Transaction>();
            
            // Advanced Transaction Mappings
            CreateMap<Transaction, TransactionAdvancedDto>()
                .ForMember(dest => dest.OrderNumber, opt => opt.MapFrom(src => src.Order.OrderNumber))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => 
                    src.Order != null && src.Order.ShippingAddress != null ? src.Order.ShippingAddress.FullName : 
                    (src.AppUser != null ? src.AppUser.UserName : "Guest")))
                .ForMember(dest => dest.CustomerEmail, opt => opt.MapFrom(src => src.AppUser != null ? src.AppUser.Email : string.Empty))
                .ForMember(dest => dest.PaymentMethodName, opt => opt.MapFrom(src => src.PaymentMethod.ToString()));

            CreateMap<TransactionCreateAdvancedDto, Transaction>();
            CreateMap<TransactionUpdateDto, Transaction>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.OrderId, opt => opt.Ignore())
                .ForMember(dest => dest.AppUserId, opt => opt.Ignore())
                .ForMember(dest => dest.Amount, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentMethod, opt => opt.Ignore())
                .ForMember(dest => dest.TransactionDate, opt => opt.Ignore())
                .ForMember(dest => dest.ProcessedDate, opt => opt.Ignore())
                .ForMember(dest => dest.TransactionReference, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentGatewayResponse, opt => opt.Ignore())
                .ForMember(dest => dest.IsRefunded, opt => opt.Ignore())
                .ForMember(dest => dest.RefundAmount, opt => opt.Ignore())
                .ForMember(dest => dest.RefundDate, opt => opt.Ignore())
                .ForMember(dest => dest.RefundReason, opt => opt.Ignore());

            CreateMap<Transaction, TransactionSummaryDto>()
                .ForMember(dest => dest.OrderNumber, opt => opt.MapFrom(src => src.Order.OrderNumber))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => 
                    src.Order != null && src.Order.ShippingAddress != null ? src.Order.ShippingAddress.FullName : 
                    (src.AppUser != null ? src.AppUser.UserName : "Guest")))
                .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod.ToString()));
        }

        private void CreateCouponMappings()
        {
            CreateMap<Coupon, CouponDto>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.AppUser != null ? src.AppUser.UserName : string.Empty))
                .ForMember(dest => dest.RemainingUses, opt => opt.MapFrom(src => src.UsageLimit.HasValue ? src.UsageLimit.Value - src.UsedCount : int.MaxValue))
                .ForMember(dest => dest.IsExpired, opt => opt.MapFrom(src => DateTime.UtcNow > src.EndDate))
                .ForMember(dest => dest.IsFullyUsed, opt => opt.MapFrom(src => src.UsageLimit.HasValue && src.UsedCount >= src.UsageLimit.Value));

            CreateMap<Coupon, CouponSummaryDto>()
                .ForMember(dest => dest.IsExpired, opt => opt.MapFrom(src => DateTime.UtcNow > src.EndDate))
                .ForMember(dest => dest.IsFullyUsed, opt => opt.MapFrom(src => src.UsageLimit.HasValue && src.UsedCount >= src.UsageLimit.Value));

            CreateMap<CouponCreateDto, Coupon>();
            CreateMap<CouponUpdateDto, Coupon>();
        }
    }


}
