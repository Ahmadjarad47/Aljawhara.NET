# Sadad official contract review

Review date: 2026-10-18  
Scope: Sadad Pay v1.0 public documentation and sandbox OpenAPI only. No merchant portal material or private provider contract was available.

## Official evidence reviewed

- [Payment Status](https://sadadpay.readme.io/reference/payment-status): instructs the merchant to call `GET /api/Invoice/getbyid?id={invoiceId}` before acting and publishes a sample containing `id`, `vendor_Id`, `status`, `amount`, `total`, `subtotal`, `refunded_Amount`, `ref_Number`, `currency_Code`, `transactions`, and `transactionID`.
- [Paid Webhook Event](https://sadadpay.readme.io/reference/invoice-paid-webhook): publishes the trigger payload, requires GetById verification, and documents retry every three minutes up to five attempts when HTTP 200 is not returned.
- [Webhook setup](https://sadadpay.readme.io/reference/webhook): documents merchant-profile URL configuration. It does not publish a signature or source-authentication scheme.
- [Create Invoice](https://sadadpay.readme.io/reference/create-invoice): publishes `ref_Number`, `amount`, `currency_Code`, items and `invoiceId`; it also states that the invoice amount is sent in KWD regardless of invoice currency.
- [Payment integration](https://sadadpay.readme.io/reference/payment-integration) and [Postman guide](https://sadadpay.readme.io/reference/postman-collection): require another GetById call after browser return and say a Paid response contains transaction details.
- [GetAll](https://sadadpay.readme.io/reference/get_api-invoice-getall): documents a `Ref_Number` query filter, but no response schema, uniqueness, consistency or completeness guarantee.
- Sandbox OpenAPI: `GET /api/Invoice/getbyid` accepts an `int64 id`; `GET /api/Invoice/getall` accepts `Ref_Number` and `vendor_Id`. Both declare only HTTP 200 and publish no response schema.

## Verification requirement classification

`VERIFIED IN SANDBOX` means observed with authorized merchant sandbox credentials. Mocked responses do not qualify.

| Requirement | Classification | Evidence and limitation |
|---|---|---|
| Invoice identity (`id`) | DOCUMENTED | Official GetById sample and endpoint parameter. Not yet certified against this merchant account. |
| Merchant identity (`vendor_Id`) | DOCUMENTED | Present in the official sample. The docs do not explicitly guarantee it is the authenticated merchant identity in every response. |
| Final payment status (`Paid`) | DOCUMENTED | Paid is named in the webhook and Postman flow. The exhaustive status vocabulary and terminality rules are not published. |
| Expected invoice amount (`amount`) | DOCUMENTED | Field and create request are documented. Relationship to discounts, items and the other totals is not guaranteed. |
| Final charged amount | REQUIRES PROVIDER CONFIRMATION | `amount`, `total` and `subtotal` appear together, but no field is formally identified as the final captured amount. |
| Currency (`currency_Code`) | DOCUMENTED | Field is published; its semantics conflict with the note that invoice amount is always sent in KWD regardless of invoice currency. |
| Merchant reference (`ref_Number`) | DOCUMENTED | Published for create, GetById and GetAll filtering. No uniqueness guarantee is published. |
| Refund state (`refunded_Amount`) | REQUIRES PROVIDER CONFIRMATION | Field is published, but full/partial refund timing and status interaction are not specified. |
| Successful transaction identity | REQUIRES PROVIDER CONFIRMATION | `transactions` and `transactionID` are published and Paid is said to expose details; item schema, uniqueness and success criteria are absent. |
| Webhook authentication/signature | UNSUPPORTED | No signature, secret, certificate or IP-authentication contract is published. |
| Provider idempotency for invoice create | UNSUPPORTED | No idempotency key or replay guarantee is documented. |
| GetAll recovery by exact reference | REQUIRES PROVIDER CONFIRMATION | Filter exists; result schema, exact-match behavior, pagination, consistency and completeness are undocumented. |
| KWD three-decimal preservation | REQUIRES PROVIDER CONFIRMATION | KWD is documented, but scale/rounding guarantees and examples at 0.001 precision are absent. |
| Discount semantics | REQUIRES PROVIDER CONFIRMATION | Sample fields exist; no normative equation links `amount`, `total`, `subtotal`, `discount_Amount` and `discount_Amount_Total`. |

No item is classified `VERIFIED IN SANDBOX`: the required sandbox credentials and merchant vendor ID were not present, and no production credentials were accessed.

## Safe interpretation adopted

The public material is sufficient to implement a strict candidate adapter and to establish that callbacks/webhooks are triggers only. It is not sufficient to activate production Paid transitions. The original assumption that all three monetary fields equal the local expected KWD amount was corrected after the sandbox hosted-invoice observation below. Discounted invoices, refunds, partial payments and uncertain GetAll results remain in reconciliation until their semantics are certified.

## 2026-10-20 hosted-invoice observation

The user's sandbox invoice exposed `amount=total=203.689`, `subtotal=195.541`, and `fees=8.148` through the same public read-only endpoint used by the hosted payment page. Net subtotal plus fees exactly equals the customer charge. The parser now supports this relationship only when authenticated GetById returns explicit numeric fees and every other payment verification check passes. Missing fees still require all three amounts to match. This corrects the amount comparison; the public response itself is never trusted for finalization and does not certify the merchant's full GetById or discount contract.

