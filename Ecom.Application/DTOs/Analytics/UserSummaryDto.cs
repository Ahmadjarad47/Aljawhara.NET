namespace Ecom.Application.DTOs.Analytics
{
    public class UserSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool EmailConfirmed { get; set; }
    }
}


