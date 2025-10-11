using Ecom.Application.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace Ecom.Application.DTOs.Product
{
    public class ProductDto : BaseDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string[] Images { get; set; } = Array.Empty<string>();
        public int SubCategoryId { get; set; }
        public string SubCategoryName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public List<ProductDetailDto> ProductDetails { get; set; } = new();
        public List<RatingDto> Ratings { get; set; } = new();
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }

    public class ProductCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public int SubCategoryId { get; set; }
        public List<ProductDetailCreateDto> ProductDetails { get; set; } = new();
    }

    public class ProductUpdateDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string[] Images { get; set; } = Array.Empty<string>();
        public int SubCategoryId { get; set; }
        public List<ProductDetailCreateDto> ProductDetails { get; set; } = new();
    }

    public class ProductSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string MainImage { get; set; } = string.Empty;
        public string SubCategoryName { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }

    public class ProductCreateWithFilesDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public int SubCategoryId { get; set; }
        public List<ProductDetailCreateDto> ProductDetails { get; set; } = new();
        public IFormFileCollection? Images { get; set; }
    }

    public class ProductUpdateWithFilesDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public int SubCategoryId { get; set; }
        public List<ProductDetailCreateDto> ProductDetails { get; set; } = new();
        public IFormFileCollection? Images { get; set; }
        public List<string> ImagesToDelete { get; set; } = new();
    }
}

