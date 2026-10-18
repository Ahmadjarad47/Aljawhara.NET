# Sadad contract requirements

Electronic confirmation remains disabled until Sadad supplies an authoritative, merchant-specific contract covering all of the following:

1. Final payment status field, complete enum, and which values are irreversible.
2. Authoritative paid amount and ISO currency fields, including decimal representation for KWD.
3. Merchant reference/transaction id echo and invoice id semantics.
4. Merchant/account identity in the verification response and how it is bound to credentials.
5. A server-to-server invoice-status endpoint suitable as financial evidence, including consistency guarantees.
6. Webhook authentication: signing algorithm, signed bytes/canonicalization, key distribution/rotation, timestamp, replay window, and event id.
7. Provider-enforced idempotency for invoice creation, or a reliable lookup by the exact merchant reference.
8. Timeout semantics: whether a timed-out request may still commit, and the supported reconciliation procedure.
9. Duplicate invoice/reference behavior and retention period.
10. Refund API, refund evidence, partial-refund rules, settlement identifiers, and reconciliation exports.
11. Sandbox/production differences, endpoint versions, rate limits, retry guidance, and SLA/support escalation.

The current adapter trusts only the documented invoice-creation envelope already used by the application. Timeout, network failure, HTTP 5xx, or an unrecognized success body becomes `RequiresReconciliation`; it is not retried as a new invoice. Redirect query parameters and the existing webhook DTO never create `Paid` state.
