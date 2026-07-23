using Ecom.Domain.comman;

namespace Ecom.Domain.Entity
{
    public class ProductVariantValue : BaseEntity
    {
        public string Value { get; set; } = string.Empty;     // مثال: Large
        public string ValueAr { get; set; } = string.Empty;   // كبير

        public decimal Price { get; set; }                    // السعر البديل

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; } = null!;
    }
}
