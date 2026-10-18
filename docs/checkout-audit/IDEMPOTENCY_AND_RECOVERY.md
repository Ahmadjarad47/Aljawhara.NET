# Idempotency and recovery

## Checkout identity

Clients should send a high-entropy `Idempotency-Key` header on `POST /api/orders`. The header remains optional only for backward compatibility; an omitted key cannot make a later client retry idempotent.

The database stores SHA-256 of `checkout:{key}` and a canonical request hash. A unique filtered SQL Server index makes the key global across API instances. Equivalent retries return the existing order/transaction/payment attempt. Reusing the key with another payload returns HTTP 409. The canonical hash includes caller scope, payment method, normalized item totals, coupon code, and shipping payload.

Guest recovery does not persist the raw order capability. For keyed guest checkout the capability is deterministically derived with HMAC-SHA256 from the idempotency key, a stored non-secret nonce, and `CheckoutSecurity:GuestTokenDerivationKey` (falling back to the deployment JWT secret for compatibility). Only its SHA-256 hash is stored.

## Other identities

- Payment attempts have a unique idempotency hash and a separate stable `PAY-...` merchant reference. Order id alone is not the attempt identity.
- `POST /api/orders/{id}/payment-attempts` requires its own `Idempotency-Key`. A filtered unique index permits only one active/unreconciled attempt per order; a retry is allowed only after a terminal failure/expiry and while the inventory reservation is still active.
- Sadad invoice id is unique when present.
- Outbox messages have unique business deduplication keys.
- Payment evidence/audit events have unique event keys.
- Inventory and coupon releases use conditional state transitions; repeated delivery has no second stock/coupon effect.
- Cancellation first claims the order transition and then releases only reservations still in an active/confirmed state.

## Commit and crash recovery

Checkout uses one local SQL transaction for order, items, atomic stock/coupon claims, transaction, payment attempt, reservations, and Outbox messages. Sadad HTTP is never called inside it. EF's execution strategy wraps the full transaction. If commit acknowledgement is lost, the handler queries the unique checkout key and returns the committed logical operation instead of creating another.

The worker claims an Outbox row with a conditional update and expiring lease. A crash before dispatch leaves `PendingDispatch` and is safe to retry. A crash after the attempt becomes `Dispatching` is not safe to resend without provider deduplication, so restart moves it to `RequiresReconciliation`.

This design does **not** claim exactly-once invoice creation. Exactly-once requires Sadad-enforced idempotency or authoritative lookup by merchant reference. Until that is documented, uncertain results require operator/provider reconciliation.
