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
7. Monetary values parse as .NET `decimal` with at most three decimal places. `amount` and `total` must both equal the full stored expected charge. `subtotal` may be net merchant proceeds only when a numeric, nonnegative `fees` field reconciles it exactly: `subtotal = total - fees`, with `0 <= fees <= total`. Missing `fees` is treated as zero and requires `subtotal = total`. There is no rounding tolerance or acceptance of a reduced customer charge.
8. `refunded_Amount` is zero. Any refund/partial refund is reconciled, not fulfilled.
9. Paid includes a non-empty `transactionID` and a non-empty `transactions` array.
10. Order is still Pending and every inventory reservation is active and unexpired.

The SHA-256 digest of the authoritative GetById body is persisted as `VerificationEvidenceId`; the gateway transaction ID, verified gross amounts and verified net subtotal are also persisted. Verified fees equal `VerifiedTotal - VerifiedSubtotal`, so this change needs no database migration. Finalization independently checks the same amount/fee equation before claiming Paid. No raw customer response body is stored.

### Gateway fee observation (2026-10-20)

For the user-provided sandbox hosted invoice, a read-only request to the payment page's public `Invoice/getforpaymentbykey` endpoint returned `status=Paid`, `amount=203.689`, `total=203.689`, `subtotal=195.541` and `fees=8.148 KWD`. The displayed customer charge and merchant order/PDF were both 203.689 KWD. This exposed the invalid assumption that subtotal always equals the customer's total.

This public hosted-invoice response is diagnostic evidence only; it never authorizes a Paid transition. The application continues to require authenticated `Invoice/getbyid` with the invoice, merchant, reference, currency, refund and transaction checks above. If its fee field is absent, malformed or fails to explain the entire subtotal difference, verification rejects the response. The sandbox observation is not certification of all merchant/discount contracts.

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

