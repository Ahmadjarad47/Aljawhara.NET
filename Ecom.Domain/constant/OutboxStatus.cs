namespace Ecom.Domain.constant;

public enum OutboxStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    DeadLetter = 3
}
