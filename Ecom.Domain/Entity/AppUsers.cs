using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Domain.Entity
{
    public class AppUsers : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public List<Order> Orders { get; set; } = new List<Order>();
        public ShippingAddress? ShippingAddresses { get; set; } 
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiresAtUtc { get; set; }
    }
}
