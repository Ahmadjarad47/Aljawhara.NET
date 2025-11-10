using Ecom.Domain.comman;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Domain.Entity
{
    public class ProductDetails : BaseEntity
    {
        public string Label { get; set; } = string.Empty;
        public string LabelAr { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ValueAr { get; set; } = string.Empty;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
    }
}
