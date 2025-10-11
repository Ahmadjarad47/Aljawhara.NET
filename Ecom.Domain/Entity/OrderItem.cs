using Ecom.Domain.comman;

namespace Ecom.Domain.Entity
{
    public class OrderItem : BaseEntity
    {
        // Foreign key to Product
        public int ProductId { get; set; }           // Assuming Product.Id is int
        public Product Product { get; set; } = null!;         // Navigation property

        public int OrderId { get; set; }             // Foreign key to Order
        public Order Order { get; set; } = null!;             // Navigation property

        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }

    }
}