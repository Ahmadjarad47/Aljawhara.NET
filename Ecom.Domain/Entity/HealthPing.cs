using Ecom.Domain.comman;

namespace Ecom.Domain.Entity
{
    public class HealthPing : BaseEntity
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}


