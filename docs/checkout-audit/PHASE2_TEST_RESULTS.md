# Phase 2 test results

Date: 2026-10-18  
Environment: .NET 10, EF Core 10, SQL Server LocalDB, mocked Sadad; no live transaction and no production database.

## Automated result

- `dotnet test Ecom.taani.slnx`: **68 passed, 0 failed, 0 skipped**.
- Migration model check: **no pending model changes** after the Phase 2 migrations.
- Build: successful; remaining compiler warnings are pre-existing nullable/naming warnings and are not checkout test failures.

## Covered evidence

- 50 concurrent identical checkout requests split across two independent API hosts: one order/capability/logical operation.
- Same key with changed payload: HTTP 409.
- A later payment attempt uses its own key, returns the same retry operation, and cannot coexist with another active/unreconciled attempt.
- Distinct concurrent checkouts: unique formatted order numbers.
- Two independent worker contexts: one invoice dispatch.
- Expired worker lease after `Dispatching`: no resend; reconciliation.
- Remote timeout/unknown result: reconciliation and no duplicate delivery.
- Active reservation confirmation, duplicate finalization, expiry, exactly-once stock/coupon release, and late-payment reconciliation.
- Stock=1 concurrency and coupon final-use concurrency.
- COD checkout and cancellation release policy.
- Durable email failure followed by persisted retry success.
- Guest authorization, rotation/revocation, legacy fake-paid rejection, and fail-closed Sadad callback.
- `12.345 KWD` SQL persistence without loss.

## Not claimable without provider contract

Live Sadad verification, authenticated webhook replay tests, provider-side idempotency, settlement/refund reconciliation, and a real remote-success/local-persistence-failure certification cannot be proven against a mocked undocumented contract. The durable `Dispatching -> RequiresReconciliation` recovery path is tested, but production certification remains blocked by Sadad.
