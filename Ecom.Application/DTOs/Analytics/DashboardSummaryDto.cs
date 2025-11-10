namespace Ecom.Application.DTOs.Analytics
{
    public class CountSummaryDto
    {
        public int Total { get; set; }
        public int LastMonth { get; set; }
    }

    public class SalesSummaryDto
    {
        public decimal Total { get; set; }
        public decimal LastMonth { get; set; }
    }

    public class DashboardSummaryDto
    {
        public CountSummaryDto Users { get; set; } = new CountSummaryDto();
        public CountSummaryDto Orders { get; set; } = new CountSummaryDto();
        public CountSummaryDto Transactions { get; set; } = new CountSummaryDto();
        public CountSummaryDto Visitors { get; set; } = new CountSummaryDto();
        public SalesSummaryDto Sales { get; set; } = new SalesSummaryDto();
    }
}


