# Sadad verification implementation

## Server-to-server flow

Browser callback, webhook and periodic polling all enter `ISadadPaymentVerificationService`. The supplied callback/webhook status, amount and transaction ID never create financial truth. The service locates the existing `PaymentAttempt`, obtains a database lease, and calls authenticated Sadad `GetById` through `ISadadInvoiceGateway`.

Only `Paid` and `Unpaid` are accepted. A Paid candidate must pass every check below before centralized finalization:

1. `isValid` is a JSON boolean equal to true and `response` is an object.
2. `id`, `vendor_Id`, `status`, `amount`, `total`, `subtotal`, `refunded_Amount`, `ref_Number` and `currency_Code` are present with strict JSON types.
3. Invoice ID equals the stored `PaymentAttempt.GatewayInvoiceId` exactly.
4. `vendor_Id` equals configured `Sadad:MerchantVendorId`.
5. `ref_Number` equals the attempt's generated `MerchantReference` exactly.
6. Currency equals both the stored currency and uppercase `KWD`.
7. Monetary values parse as .NET `decimal`. `amount` and `total` equal the stored expected amount after KWD three-decimal normalization. This accepts binary serialization noise such as `354.66900000000004`, but a complete `0.001` KWD difference still fails. `subtotal` is retained as audit evidence and is not treated as the charged total.
8. `refunded_Amount` is zero at KWD precision and there is no successful Refund/PartialRefund transaction. Any effective refund is reconciled, not fulfilled.
9. Paid includes a non-empty `transactionID` and a non-empty `transactions` array containing a `Capture` transaction whose status is `Success`, ID matches `transactionID`, amount equals the expected KWD amount, currency is `KWD`, and any supplied transaction invoice ID matches the invoice.
10. Order is still Pending and every inventory reservation is active and unexpired.

The SHA-256 digest of the authoritative GetById body is persisted as `VerificationEvidenceId`; the gateway transaction ID and all verified monetary fields are also persisted. No raw customer response body is stored.

## State and concurrency behavior

- A database lease permits one verifier per attempt. Concurrent webhook, callback and poll operations return Busy/AlreadyPaid or converge on one Paid transition.
- The `PaymentAttempt` and `Order` are claimed with conditional SQL updates inside a transaction.
- Duplicate/replayed events are idempotent after Paid and cannot downgrade the payment.
- Invalid, incomplete, unknown, refunded or unavailable results move/stay in `RequiresReconciliation` with a bounded reason code.
- Paid after reservation release moves the order and attempt to reconciliation; it never starts fulfilment.
- Callback query `payment` and webhook fields are informational triggers only.

## Reconciliation after uncertain create

No invoice is recreated after an unknown remote result. `GetAll?Ref_Number=...&vendor_Id=...` is disabled unless `Sadad:Verification:GetAllContractCertified=true`.

When enabled after certification, the adapter requires exact merchant reference and vendor matches:

- zero matches: keep reconciliation;
- multiple matches: keep reconciliation;
- one exact match: store its unique invoice ID, then require the complete GetById verification above;
- malformed/unavailable results: keep reconciliation.

GetAll alone never marks Paid.

Administrators can safely recheck known historical invoice IDs with `POST /api/admin/transaction/sadad/reconcile`. The endpoint is Admin-only, accepts at most 100 IDs, never creates an invoice or payment, and sends every ID through the same leased GetById verification/finalization path. Example body: `{ "invoiceIds": [70934, 70935] }`.

## Fail-closed activation

All settings default to false/empty:

```json
{
  "Sadad": {
    "MerchantVendorId": "",
    "Verification": {
      "ContractProfileCertified": false,
      "EnablePaidTransitions": false,
      "DiscountSemanticsCertified": false,
      "GetAllContractCertified": false
    }
  }
}
```

Even a structurally valid Paid response stays in reconciliation until both `ContractProfileCertified` and `EnablePaidTransitions` are explicitly enabled. Discounted invoices and GetAll recovery have separate gates.

## Persistence and code locations

- Adapter contract/parser: `Ecom.Application/Payments/ISadadInvoiceGateway.cs`, `SadadInvoiceGateway.cs`.
- Lease, reconciliation and finalization: `PaymentOrchestrationService.cs`.
- Trigger endpoints: `Ecom.API/Controllers/OrdersController.cs`.
- Poller: `Ecom.API/HostedServices/SadadPaymentStatusCheckerService.cs`.
- Evidence fields: `Ecom.Domain/Entity/PaymentAttempt.cs`.
- Migration: `20261018063147_Phase3SadadVerificationEvidence`.

