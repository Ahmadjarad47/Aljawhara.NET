using Ecom.Domain.constant;
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
        public PaymentMethod PaymentMethod { get; set; }
        public TransactionStatus Status { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? GatewayInvoiceId { get; set; }
        public bool IsActive { get; set; }
    }

    public class TransactionCreateDto
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        public string? GatewayInvoiceId { get; set; }
    }
}
