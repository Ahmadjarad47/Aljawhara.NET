using Ecom.Domain.comman;

namespace Ecom.Domain.Entity
{
    public class ShippingAddress : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? State { get; set; }        // Optional
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        // Optional relation back to AppUser
        public string? AppUserId { get; set; }
        public AppUsers? AppUser { get; set; }
    }
}