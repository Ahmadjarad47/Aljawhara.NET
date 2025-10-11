using Ecom.Domain.comman;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Domain.Entity
{
    public class Rating : BaseEntity
    {
        public string Content { get; set; } = string.Empty;
        public double RatingNumber { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
    }
}
