using Ecom.Domain.comman;

namespace Ecom.Domain.Entity
{
    public class ProductVariant : BaseEntity
    {
        public string Name { get; set; } = string.Empty;      // مثال: Size
        public string NameAr { get; set; } = string.Empty;    // حجم

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public List<ProductVariantValue> Values { get; set; } = new();
    }
}
