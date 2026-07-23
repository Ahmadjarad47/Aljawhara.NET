using Ecom.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Product
{
    public class ProductVariantDto : BaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public List<ProductVariantValueDto> Values { get; set; } = new();
    }

    public class ProductVariantValueDto : BaseDto
    {
        public string Value { get; set; } = string.Empty;
        public string ValueAr { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int ProductVariantId { get; set; }
    }

    public class ProductVariantCreateDto
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic name cannot exceed 100 characters")]
        public string NameAr { get; set; } = string.Empty;
        
        public List<ProductVariantValueCreateDto> Values { get; set; } = new();
    }

    public class ProductVariantValueCreateDto
    {
        [Required(ErrorMessage = "Value is required")]
        [StringLength(100, ErrorMessage = "Value cannot exceed 100 characters")]
        public string Value { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic value cannot exceed 100 characters")]
        public string ValueAr { get; set; } = string.Empty;
        
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }
    }

    public class ProductVariantUpdateDto
    {
        public int? Id { get; set; } // Null for new variants
        
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic name cannot exceed 100 characters")]
        public string NameAr { get; set; } = string.Empty;
        
        public List<ProductVariantValueUpdateDto> Values { get; set; } = new();
    }

    public class ProductVariantValueUpdateDto
    {
        public int? Id { get; set; } // Null for new values
        
        [Required(ErrorMessage = "Value is required")]
        [StringLength(100, ErrorMessage = "Value cannot exceed 100 characters")]
        public string Value { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic value cannot exceed 100 characters")]
        public string ValueAr { get; set; } = string.Empty;
        
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }
    }
}

