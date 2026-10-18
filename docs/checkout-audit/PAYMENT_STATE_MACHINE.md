# Payment state machine

## Persistent attempt states

`PendingDispatch -> Dispatching -> AwaitingPayment -> Paid`

- `PendingDispatch`: the local checkout transaction committed; no Sadad invoice request has started.
- `Dispatching`: a leased worker persisted that it is about to send the request. This boundary is intentionally durable.
- `AwaitingPayment`: Sadad returned a recognized invoice id. The invoice id and payment URL are stored locally.
- `Paid`: entered only by `IPaymentFinalizationService` after an authoritative verification adapter supplies matching invoice id, merchant identity/reference, amount, currency, and terminal paid evidence.
- `Failed`: a definitive provider rejection, such as a recognized non-success 4xx response.
- `RequiresReconciliation`: the remote outcome may be successful but is not known locally. No automatic resend or fulfilment is allowed.
- `Cancelled` / `Expired`: the local operation ended before verified payment.

## Critical transitions

| From | Event | To | Rule |
|---|---|---|---|
| PendingDispatch | worker claim | Dispatching | conditional SQL update; one worker wins |
| Dispatching | recognized invoice response | AwaitingPayment | invoice id is unique; transaction and audit update atomically |
| Dispatching | timeout/network/5xx/unrecognized success | RequiresReconciliation | never resend blindly |
| Dispatching | worker restarts with expired lease | RequiresReconciliation | the request may already have reached Sadad |
| AwaitingPayment | authoritative paid evidence + active reservations | Paid | confirms inventory/coupon and moves order to Processing in one SQL transaction |
| AwaitingPayment | reservation expired/cancelled | RequiresReconciliation | late payment does not fulfil unavailable stock |
| any unpaid state | cancellation | Cancelled or RequiresReconciliation | Dispatching becomes reconciliation; safe pre/post states cancel |

`TransactionStatus.Paid` and `OrderStatus.Processing` cannot be produced by the public callback body, redirect query, or administrative status edit. Sadad confirmation remains fail-closed.

## API projection

Checkout adds `paymentAttemptId`, `merchantReference`, `paymentInitializationStatus`, and `reservationExpiresAtUtc`. Existing fields remain. `GET /api/orders/{id}/payment-status` returns the durable state and an existing URL when available, subject to the same user/guest capability authorization as the order.

`POST /api/orders/{id}/payment-attempts` starts a later legitimate attempt with a new required idempotency key only after the prior attempt is terminal and the reservation is still valid. It returns HTTP 202 and the same status projection.
