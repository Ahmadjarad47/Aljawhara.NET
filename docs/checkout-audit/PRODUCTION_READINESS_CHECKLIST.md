# Production readiness checklist

## Implemented and verified

- [x] SQL-backed checkout idempotency with payload conflict detection.
- [x] Persistent payment attempts, merchant references, transition audit, and unique constraints.
- [x] Transactional Outbox and multi-instance conditional lease claim.
- [x] No Sadad HTTP call inside checkout SQL transaction.
- [x] Unknown remote outcome stops in reconciliation without blind resend.
- [x] Secure payment-status polling under user/guest order authorization.
- [x] KWD money columns use `decimal(18,3)` and half-away-from-zero rounding.
- [x] Atomic inventory/coupon reservation, confirmation, expiry, and release.
- [x] Concurrency-safe daily order-number counter.
- [x] Durable email notification with retry/backoff and deduplication key.
- [x] Direct public and administrative payment creation endpoints disabled.
- [x] SQL Server integration and HTTP tests run against isolated LocalDB databases.

## Required before deployment

- [x] Implement strict GetById verification, centralized finalization, duplicate/replay protection, and fail-closed activation gates.
- [x] Treat callback/webhook payloads as triggers only and require authenticated server-to-server verification.
- [ ] Complete the provider answers in `SADAD_PROVIDER_OPEN_QUESTIONS.md` and approve the exact certified profile.
- [ ] Execute and archive the controlled provider sandbox suite in `SADAD_SANDBOX_TEST_MATRIX.md`.
- [ ] Configure the certified sandbox/production `Sadad__MerchantVendorId`; only then review enabling `ContractProfileCertified` and `EnablePaidTransitions`.
- [ ] Keep `DiscountSemanticsCertified` and `GetAllContractCertified` false until their separate sandbox cases pass.
- [ ] Configure stable, identical secrets on every instance: JWT signing key and `CheckoutSecurity__GuestTokenDerivationKey` (32+ bytes).
- [ ] Configure `ConnectionStrings__DefaultConnection`, Sadad credentials/official merchant identity, `Notifications__AdminEmail`, and SMTP values in the deployment secret store.
- [ ] Persist/rotate secrets according to `SECURE_CONFIGURATION_AND_ROTATION.md`; do not rotate the guest derivation key without a compatibility window.
- [ ] Run migration preflight on a production snapshot, back up the database, and confirm no invalid coupon data.
- [ ] Validate worker concurrency, clocks, alerts, dead-letter/reconciliation dashboards, and SMTP in staging.
- [ ] Confirm frontend sends `Idempotency-Key`, handles `paymentInitializationStatus`, and polls the secure status endpoint.
- [ ] Define operational ownership for `RequiresReconciliation` and `DeadLetter` rows.

Migration procedure: back up and restore-test first; run `dotnet ef database update` in a single controlled deployment job, then start workers. The `Phase2FinancialInvariants` preflight aborts rather than normalizing invalid legacy coupon/payment data. Rollback requires stopping workers first; downgrading money columns to `decimal(18,2)` is lossy and must not be run without an approved data export/rounding plan.

Final status: **NO-GO for production electronic payments** until Sadad sandbox certification and deployment prerequisites are complete. The adapter is implemented and tested against deterministic mocks, but activation remains fail-closed.
