using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Domain.Entity
{
    public class AppUsers : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<Order> Orders { get; set; } = new List<Order>();
        public List<ShippingAddress> ShippingAddresses { get; set; } = new List<ShippingAddress>();
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
