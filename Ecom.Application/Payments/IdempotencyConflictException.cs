namespace Ecom.Application.Payments;

public sealed class IdempotencyConflictException : ArgumentException
{
    public IdempotencyConflictException() : base("The idempotency key was already used with a different checkout payload.") { }
}
