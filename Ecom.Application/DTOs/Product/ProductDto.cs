using Ecom.Application.DTOs.Common;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Product
{
    public class ProductDto : BaseDto
    {
        public string Title { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public bool IsInStock { get; set; }
        public int TotalInStock { get; set; }
        public string[] Images { get; set; } = Array.Empty<string>();
        public string MainImage { get; set; } = string.Empty;
        public int SubCategoryId { get; set; }
        public string SubCategoryName { get; set; } = string.Empty;
        public string SubCategoryNameAr { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryNameAr { get; set; } = string.Empty;
        public List<ProductDetailDto> ProductDetails { get; set; } = new();
        public List<RatingDto> Ratings { get; set; } = new();
        public List<ProductVariantDto> Variants { get; set; } = new();
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }

    public class ProductCreateDto
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;
        
        [StringLength(200, ErrorMessage = "Arabic title cannot exceed 200 characters")]
        public string TitleAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(2000, ErrorMessage = "Arabic description cannot exceed 2000 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Old price must be 0 or greater")]
        public decimal OldPrice { get; set; }
        
        [Range(0.01, double.MaxValue, ErrorMessage = "New price must be greater than 0")]
        public decimal NewPrice { get; set; }
        
        public bool IsInStock { get; set; } = true;
        
        [Range(0, int.MaxValue, ErrorMessage = "Total in stock must be 0 or greater")]
        public int TotalInStock { get; set; } = 0;
        
        [Required(ErrorMessage = "SubCategory ID is required")]
        public int SubCategoryId { get; set; }
        
        public List<ProductDetailCreateDto> ProductDetails { get; set; } = new();
        public List<ProductVariantCreateDto> Variants { get; set; } = new();
    }

    public class ProductUpdateDto
    {
        [Required(ErrorMessage = "ID is required")]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;
        
        [StringLength(200, ErrorMessage = "Arabic title cannot exceed 200 characters")]
        public string TitleAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(2000, ErrorMessage = "Arabic description cannot exceed 2000 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Old price must be 0 or greater")]
        public decimal OldPrice { get; set; }
        
        [Range(0.01, double.MaxValue, ErrorMessage = "New price must be greater than 0")]
        public decimal NewPrice { get; set; }
        
        public bool IsInStock { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Total in stock must be 0 or greater")]
        public int TotalInStock { get; set; }
        
        public string[] Images { get; set; } = Array.Empty<string>();
        
        [Required(ErrorMessage = "SubCategory ID is required")]
        public int SubCategoryId { get; set; }
        
        public List<ProductDetailCreateDto> ProductDetails { get; set; } = new();
        public List<ProductVariantUpdateDto> Variants { get; set; } = new();
    }

    public class ProductSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public bool IsInStock { get; set; }
        public int TotalInStock { get; set; }
        public string MainImage { get; set; } = string.Empty;
        public string SubCategoryName { get; set; } = string.Empty;
        public string SubCategoryNameAr { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public bool IsActive { get; set; }
    }

    public class ProductCreateWithFilesDto
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;
        
        [StringLength(200, ErrorMessage = "Arabic title cannot exceed 200 characters")]
        public string TitleAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(2000, ErrorMessage = "Arabic description cannot exceed 2000 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Old price must be 0 or greater")]
        public decimal OldPrice { get; set; }
        
        [Range(0.01, double.MaxValue, ErrorMessage = "New price must be greater than 0")]
        public decimal NewPrice { get; set; }
        
        public bool IsInStock { get; set; } = true;
        
        [Range(0, int.MaxValue, ErrorMessage = "Total in stock must be 0 or greater")]
        public int TotalInStock { get; set; } = 0;
        
        [Required(ErrorMessage = "SubCategory ID is required")]
        public int SubCategoryId { get; set; }
        
        public List<ProductDetailCreateDto> ProductDetails { get; set; } = new();
        public List<ProductVariantCreateDto> Variants { get; set; } = new();
        public IFormFileCollection? Images { get; set; }
    }

    public class ProductUpdateWithFilesDto
    {
        [Required(ErrorMessage = "ID is required")]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;
        
        [StringLength(200, ErrorMessage = "Arabic title cannot exceed 200 characters")]
        public string TitleAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(2000, ErrorMessage = "Arabic description cannot exceed 2000 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Old price must be 0 or greater")]
        public decimal OldPrice { get; set; }
        
        [Range(0.01, double.MaxValue, ErrorMessage = "New price must be greater than 0")]
        public decimal NewPrice { get; set; }
        
        public bool IsInStock { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Total in stock must be 0 or greater")]
        public int TotalInStock { get; set; }
        
        [Required(ErrorMessage = "SubCategory ID is required")]
        public int SubCategoryId { get; set; }
        
        public List<ProductDetailCreateDto> ProductDetails { get; set; } = new();
        public List<ProductVariantUpdateDto> Variants { get; set; } = new();
        public IFormFileCollection? Images { get; set; }
        public List<string> ImagesToDelete { get; set; } = new();
    }
}

