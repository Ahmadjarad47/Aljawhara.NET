using Ecom.Application.DTOs.Common;

namespace Ecom.Application.DTOs.Product
{
    public class ProductDetailDto : BaseDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int ProductId { get; set; }
    }

    public class ProductDetailCreateDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}

