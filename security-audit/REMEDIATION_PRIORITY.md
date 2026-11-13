# Remediation priorities

The order below uses the exposed credential scope, customer and operator impact, and whether a defect can affect real financial records. The detailed source evidence and proposed regression checks are in the [assessment](SECURITY_ASSESSMENT.md).

## P0 · Immediate containment

| Finding | Action | Completion evidence |
| --- | --- | --- |
| AS-01 · Credential exposure | Revoke and replace the SMTP password and Telegram bot token. Remove the values from source and copy-safe examples. Confirm retirement of historical JWT signing material and historical API credentials. Coordinate with deployment operators to review relevant artifacts and access logs. | The operator records revocation and replacement without including credential values. Deployment injects replacements, and secret scanning covers the working tree, Git history and releasable artifacts. |
| AS-05 · Refund integrity | Disable the admin refund action or label and restrict it as an explicit manual reconciliation flow until real provider confirmation is implemented. Reconcile previously marked local refunds against independent provider evidence. | Finance resolves mismatches. Fake-provider tests enforce pending, failed, confirmed and duplicate refund handling. |

## P1 · Account and payment boundaries

| Findings | Action | Completion evidence |
| --- | --- | --- |
| AS-02 · Session invalidation | Implement hashed per-session refresh storage, atomic rotation, server-side revocation on account events, and consistent access-token expiration. | Tests verify logout, password change, password reset, block and concurrent rotation. Monitoring Admin access respects the chosen revocation policy. |
| AS-03 and AS-12 · Recovery | Bind challenges to account and purpose. Set attempt and issuance budgets. Replace the two-step password write with an atomic Identity reset. | Tests use synthetic accounts and an in-memory mail sink to verify purpose isolation, single consumption and password preservation on failure. |
| AS-04 · Coupon ownership | Filter public reads to public promotions and use principal-derived identity for private eligibility. | Public, owner and Admin fixtures expose only the coupon data each caller is entitled to see. |
| AS-09 · Payment triggers | Minimize callback responses, add per-invoice verification limits, and authenticate provider events under a verified provider contract when supported. | Public HTTP response tests cover uniform outcomes. The fake provider stays within the configured verification budget and paid transitions still require verified evidence. |

## P2 · Output and catalog integrity

| Findings | Action | Completion evidence |
| --- | --- | --- |
| AS-07 and AS-08 · Output encoding | Encode contact mail as HTML text and write transactions through a proper CSV or workbook encoder with formula-safe untrusted fields. | Tests inspect a captured MIME message and parse exported rows or cells without using external mail or a live spreadsheet client. |
| AS-10 · Review integrity | Establish review eligibility and enforce authorship and uniqueness on writes. | A defensive test suite verifies required identity, eligibility and the database uniqueness constraint. |
| AS-11 and AS-13 · Object storage | Validate image formats and limits, serve from a dedicated asset origin, and restrict deletions to assets attached to the product. | A fake bucket records validated writes and authorized deletes only. Verify asset-origin response headers in an isolated deployment. |
| AS-06 · Seeded accounts | Gate demo seeds behind explicit development setup and remove fixed passwords. Review shared non-production databases for demo accounts and retire their sessions and credentials if present. | Default startup in every environment creates zero demo accounts. An explicitly enabled development fixture uses generated credentials. |

## Release verification

- Run the existing defensive suites plus regression tests for each fixed invariant. Include PostgreSQL-backed tests for recovery, checkout idempotency and payment transitions in a disposable environment with stubbed outbound services.
- Obtain the storefront source and local build to verify rendering and browser authentication assumptions. Verify proxy settings, object-storage response headers, database grants and Telegram destination access under operator control.
- Run fresh NuGet advisory checks for direct and transitive dependencies in CI. Apply equivalent coverage in the frontend repository if it uses npm.
