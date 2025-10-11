using Ecom.Application.DTOs.Common;

namespace Ecom.Application.DTOs.Order
{
    public class OrderItemDto : BaseDto
    {
        public int ProductId { get; set; }
        public int OrderId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Total { get; set; }
    }

    public class OrderItemCreateDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}





