using Ecom.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Product
{
    public class ProductDetailDto : BaseDto
    {
        public string Label { get; set; } = string.Empty;
        public string LabelAr { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ValueAr { get; set; } = string.Empty;
        public int ProductId { get; set; }
    }

    public class ProductDetailCreateDto
    {
        [Required(ErrorMessage = "Label is required")]
        [StringLength(100, ErrorMessage = "Label cannot exceed 100 characters")]
        public string Label { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic label cannot exceed 100 characters")]
        public string LabelAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Value is required")]
        [StringLength(500, ErrorMessage = "Value cannot exceed 500 characters")]
        public string Value { get; set; } = string.Empty;
        
        [StringLength(500, ErrorMessage = "Arabic value cannot exceed 500 characters")]
        public string ValueAr { get; set; } = string.Empty;
    }
}

