# Checkout shipping and payment

Use the existing `POST /api/Orders` endpoint. JSON fields use camelCase.

```json
{
  "items": [{ "productId": 123, "quantity": 1 }],
  "shippingAddress": {
    "fullName": "بدر",
    "phone": "213123213",
    "street": "محافظة الفروانية - عبدالله المبارك",
    "city": "عبدالله المبارك",
    "state": "الفروانية",
    "alQataa": "8",
    "alSharee": "809",
    "alJada": "16",
    "alManzil": "16",
    "alDor": "2",
    "alShakka": "5"
  },
  "paymentMethod": 2
}
```

- `paymentMethod: 2` (`Cod`): cash on delivery. The transaction is saved as `Pending` (`status: 0`), with `paymentUrl: null` and no gateway invoice. Show order confirmation; do not request a payment link or redirect to the gateway.
- `paymentMethod: 4` (`Bank`): the existing electronic payment flow through Sadad. Use the returned `paymentUrl` to redirect to payment when present. Creating the invoice does not mark the transaction paid.
- Omitting `paymentMethod` retains the existing electronic payment behavior (`4`) for older clients. Undefined numeric enum values are rejected.
- The existing response includes `orderId`, `orderNumber`, `paymentMethod`, `status`, `gatewayInvoiceId`, and `paymentUrl`.
- `POST /api/Orders/{id}/payment-link` rejects cash-on-delivery orders instead of creating an electronic transaction for them.

Only `fullName`, `phone`, and `street` are required for checkout shipping. `street` contains the combined governorate/area label; `alSharee` remains the separate street field. `city` is the area and `state` is the governorate; both are optional, as are postal code, country, and the detailed address fields.

For account address endpoints, use `addressLine1` and `phoneNumber` instead of checkout's `street` and `phone`. No new address endpoint is needed.

Apply `20261017161057_MakeShippingLocationFieldsOptional` to each deployed database. The existing API startup applies pending migrations. Payment selection uses the existing transaction column and requires no additional migration.

Run payment regression checks with `dotnet run --project tests/Checkout.PaymentChecks/PaymentChecks.csproj --configuration Release`. These checks use repository doubles and a mocked HTTP gateway; they do not create real orders or contact Sadad.
# Phase 2 retry and asynchronous payment initialization

Clients should attach a high-entropy `Idempotency-Key` to `POST /api/orders`. Retrying the same body with the same key returns the same logical checkout; changing the body returns HTTP 409. Existing clients without the header still work but do not receive retry deduplication.

Electronic checkout is asynchronous. The response retains existing transaction fields and adds `paymentAttemptId`, `merchantReference`, `paymentInitializationStatus`, and `reservationExpiresAtUtc`. Poll `GET /api/orders/{id}/payment-status`; authenticated owners use their JWT and guests use `X-Guest-Order-Token`. A URL is returned only after the durable worker has safely stored it. `RequiresReconciliation` must be shown as pending/manual review, never as paid.

After a terminal attempt failure, retry with `POST /api/orders/{id}/payment-attempts`, a new `Idempotency-Key`, and the same owner/guest authorization. Do not retry while the status is pending, dispatching, awaiting payment, or reconciliation.

# Phase 3 verified payment completion

`paymentInitializationStatus` now includes `Paid = 5`. The complete numeric mapping is `NotApplicable=0`, `Pending=1`, `Ready=2`, `RequiresReconciliation=3`, `Failed=4`, `Paid=5`. Never infer paid state from a browser query parameter or webhook payload; the API confirms it only after authenticated Sadad GetById verification.

After returning from Sadad, call `GET /api/orders/payment-callback?invoice_id={id}&payment={value}` only as a verification trigger, then poll the protected order payment-status endpoint. HTTP 200 means verified/already Paid, 202 means verification is pending or requires reconciliation, 400 means rejected, and 404 means unknown invoice. Frontend details are in `docs/checkout-audit/PHASE3_FRONTEND_HANDOFF.md`.
