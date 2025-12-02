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

        // Arabic address fields
        public string? AlQataa { get; set; } // القطعة (District/Block)
        public string? AlSharee { get; set; } // الشارع (Street)
        public string? AlJada { get; set; } // الجادة (Avenue)
        public string? AlManzil { get; set; } // المنزل (House)
        public string? AlDor { get; set; } // الدور (Floor)
        public string? AlShakka { get; set; } // الشقة (Apartment)

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