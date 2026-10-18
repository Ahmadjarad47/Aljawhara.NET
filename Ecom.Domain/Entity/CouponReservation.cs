using Ecom.Domain.comman;
using Ecom.Domain.constant;

namespace Ecom.Domain.Entity;

public class CouponReservation : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int CouponId { get; set; }
    public Coupon Coupon { get; set; } = null!;
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
