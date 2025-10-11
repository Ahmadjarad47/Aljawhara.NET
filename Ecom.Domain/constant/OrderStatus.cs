using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Domain.constant
{
    public enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled,
        Refunded
    }
}
