using Ecom.Domain.comman;
using Ecom.Domain.constant;

namespace Ecom.Domain.Entity;

public class InventoryReservation : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
