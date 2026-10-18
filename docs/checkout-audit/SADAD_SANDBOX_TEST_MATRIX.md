# Sadad sandbox and deterministic test matrix

Execution date: 2026-10-18

## Automated isolated SQL Server suite

The Phase 3 tests use isolated SQL Server LocalDB databases and mocked authenticated Sadad responses. They validate application behavior, not provider behavior.

| Scenario | Result | Enforced outcome |
|---|---|---|
| Unpaid invoice | PASS | Remains AwaitingPayment; never Paid. |
| Strict verified Paid invoice | PASS | Complete evidence parsed using decimal and three-digit KWD amount. |
| Invoice ID mismatch | PASS | Rejected/fail closed. |
| Merchant/vendor mismatch | PASS | Rejected/fail closed. |
| Merchant reference mismatch | PASS | Rejected/fail closed. |
| Amount mismatch | PASS | Rejected/fail closed. |
| Currency mismatch | PASS | Rejected/fail closed. |
| Unknown status | PASS | Rejected/fail closed. |
| Missing transaction evidence | PASS | Rejected/fail closed. |
| Missing required fields | PASS | Rejected/fail closed. |
| Refund/partial-refund signal | PASS | Requires reconciliation. |
| More than three KWD decimals | PASS | Rejected/fail closed. |
| Coupon/discount invoice without certified semantics | PASS | No Sadad call; remains unsupported/reconciliation. |
| Duplicate webhook delivery and replay | PASS | One gateway verification and one Paid transition. |
| Concurrent verifiers | PASS | One lease owner, one GetById, one Paid transition. |
| Paid after reservation expiry | PASS | Requires reconciliation; transaction remains Pending. |
| Timeout/unknown create recovered through one exact GetAll candidate | PASS | Candidate is reverified with GetById before Paid. |
| Ambiguous GetAll response | PASS | Preserves reconciliation and never creates another invoice. |

Phase 3 cases: **18 passed, 0 failed, 0 skipped**.  
Complete regression suite: **86 passed, 0 failed, 0 skipped** (the prior 68 plus 18 Phase 3 cases).

Additional checks:

- Solution build: PASS, 0 errors.
- Checkout smoke checks: PASS for COD and mocked electronic invoice initialization.
- EF model/migration parity: PASS, no pending model changes.
- NuGet vulnerability scan: PASS, no vulnerable packages reported by configured sources.

## Controlled Sadad sandbox certification

Status: **BLOCKED / NOT EXECUTED**.

`ClientKey`, `ClientSecret` and a sandbox vendor ID were not available in the process environment. No production credential was requested or accessed, and no real card/payment was charged.

The following must be executed with synthetic data and documented sandbox payment methods before any activation flag is changed:

| Sandbox case | Required evidence |
|---|---|
| Create non-discounted `12.345 KWD` invoice | Request/response proves exact scale and stored GetById values. |
| Unpaid then Paid lifecycle | Full status sequence and proof that Paid is terminal. |
| Merchant identity | Confirm `vendor_Id` is always the authenticated merchant ID. |
| Monetary semantics | Provider-backed equations for `amount`, `total`, `subtotal` and captured transaction value. |
| Discounted fixed and percentage invoices | Exact equations and rounding at three decimals. |
| Paid transaction evidence | Stable successful transaction identifier and `transactions` item success fields. |
| Partial/full refund | Status and `refunded_Amount` behavior over time. |
| Webhook retry/replay | Retry cadence, duplicate payload behavior and acknowledgement expectations. |
| GetAll exact reference | Response schema, pagination, exact matching, duplicates and visibility delay. |
| Timeout after create | Confirm recovery behavior without duplicate creation. |

