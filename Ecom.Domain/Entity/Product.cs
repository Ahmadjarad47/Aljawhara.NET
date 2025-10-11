using Ecom.Domain.comman;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Domain.Entity
{
    public class Product : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal oldPrice { get; set; }
        public decimal newPrice { get; set; }

        public string[] Images { get; set; } = Array.Empty<string>();
        public List<ProductDetails> productDetails { get; set; } = new List<ProductDetails>();

        public List<Rating> Ratings { get; set; } = new List<Rating>();

        public int SubCategoryId { get; set; }
        public SubCategory subCategory { get; set; } = null!;
    }
}
