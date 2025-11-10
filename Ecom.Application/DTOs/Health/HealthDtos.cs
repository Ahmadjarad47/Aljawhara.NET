namespace Ecom.Application.DTOs.Health
{
    public class HealthSummaryDto
    {
        public bool Healthy { get; set; }
        public HealthChecksDto Checks { get; set; } = new HealthChecksDto();
        public DateTime Timestamp { get; set; }
    }

    public class HealthChecksDto
    {
        public bool Database { get; set; }
    }

    public class HealthChartPointDto
    {
        public DateTime Date { get; set; }
        public int Healthy { get; set; }
        public int Unhealthy { get; set; }
    }
}


