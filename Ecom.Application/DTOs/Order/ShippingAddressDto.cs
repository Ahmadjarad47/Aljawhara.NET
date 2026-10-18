using Ecom.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Order
{
        public class ShippingAddressDto : BaseDto
        {
            public string FullName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Street { get; set; } = string.Empty;
            public string? City { get; set; }
            public string? State { get; set; }
            public string? PostalCode { get; set; }
            public string? Country { get; set; }
            public string? AppUserId { get; set; }
            
            // Arabic address fields
            public string? AlQataa { get; set; } // القطعة (District/Block)
            public string? AlSharee { get; set; } // الشارع (Street)
            public string? AlJada { get; set; } // الجادة (Avenue)
            public string? AlManzil { get; set; } // المنزل (House)
            public string? AlDor { get; set; } // الدور (Floor)
            public string? AlShakka { get; set; } // الشقة (Apartment)
        }

    public class ShippingAddressCreateDto
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Street { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        
        // Arabic address fields
        public string? AlQataa { get; set; } // القطعة (District/Block)
        public string? AlSharee { get; set; } // الشارع (Street)
        public string? AlJada { get; set; } // الجادة (Avenue)
        public string? AlManzil { get; set; } // المنزل (House)
        public string? AlDor { get; set; } // الدور (Floor)
        public string? AlShakka { get; set; } // الشقة (Apartment)
    }

    public class ShippingAddressUpdateDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Street { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        
        // Arabic address fields
        public string? AlQataa { get; set; } // القطعة (District/Block)
        public string? AlSharee { get; set; } // الشارع (Street)
        public string? AlJada { get; set; } // الجادة (Avenue)
        public string? AlManzil { get; set; } // المنزل (House)
        public string? AlDor { get; set; } // الدور (Floor)
        public string? AlShakka { get; set; } // الشقة (Apartment)
    }
}





