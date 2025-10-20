using Ecom.Domain.comman;

namespace Ecom.Domain.Entity
{
    public class ShippingAddress : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;

        // Optional relation back to AppUser
        public string? AppUserId { get; set; }
        public AppUsers? AppUser { get; set; }
        
        // Legacy properties for backward compatibility
        public string Phone 
        { 
            get => PhoneNumber; 
            set => PhoneNumber = value; 
        }
        
        public string Street 
        { 
            get => AddressLine1; 
            set => AddressLine1 = value; 
        }
    }
}