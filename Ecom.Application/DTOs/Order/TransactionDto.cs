using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Application.DTOs.Order
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
    }

    public class TransactionCreateDto
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public Domain.constant.PaymentMethod PaymentMethod { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
