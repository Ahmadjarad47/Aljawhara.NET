using Ecom.Domain.comman;

namespace Ecom.Domain.Entity
{
    public class Visitor : BaseEntity
    {
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Path { get; set; }
        public DateTime VisitedAtUtc { get; set; } = DateTime.UtcNow;
    }
}


