# Aljawhara local security assessment — 14 November 2026

This was a defensive assessment of two working trees, a loopback ASP.NET Core server, the Angular client, and a dedicated PostgreSQL 18 cluster. It included live HTTP, Playwright, selected automated tests, and database assertions. No public service was used. Exploit development and vulnerability reproduction were outside the work performed; the results do not establish that untested attack classes are safe.

## Confirmed findings

| Finding | Affected surface | Observed behavior and impact |
| --- | --- | --- |
| R1 — Password recovery cannot be completed from the standard user flow | `POST /api/Auth/forgot-password`, `POST /api/Auth/reset-password`, Angular `/auth/forgot-password` and `/auth/reset-password` | A verified customer received a six-digit recovery code in the local mailbox. The success page had no reset link; direct navigation to the reset form exposed a read-only code input. The browser completed the reset and logged in only after the harness supplied the mailed code to the route as expected by the component. A customer following the normal flow can remain locked out. |
| R2 — Initial snapshot could not start with PostgreSQL migrations | All API operations | EF Core aborted before Kestrel listened because the model contained catalog ownership and reviewer columns and indexes without a corresponding migration. An audit-only migration unblocked the initial run. The later workspace revision added `20261114083000_P2CatalogIntegrity`; its isolated copy applied the real migration to a new database and returned 200 from `/health`. This initial blocker was cleared during the assessment. |

R1 root cause: the backend reset email contains a code but no link to a usable reset form. Angular marks the code input read-only, prepopulates it only from a query parameter, and provides no reset navigation from the request success screen. Fix the normal flow by offering a reset link and allowing the customer to enter the emailed code in the form. Use the [Playwright regression](password-recovery-regression.cjs) against a local frontend and test mailbox; it fails at the missing reset link on the observed build. The positive reset recorded in [browser evidence](browser-evidence.json) establishes that the backend can complete the workflow when it receives the code.

R2 regression: start the API in Development against a fresh disposable PostgreSQL database with integrations mocked. Require startup to apply all migrations and `GET /health` to return 200; retain a model-snapshot drift assertion in the suite. The initial run raised `PendingModelChangesWarning`; [host evidence](host-route-evidence.json) records the later success.

## Boundaries checked

49 explicit HTTP contract assertions passed across 21 operations. The suite observed 401 for missing credentials, 403 for a signed-in customer on nine sampled admin operations, 200 for an admin on those operations, 400 for missing or invalid input, and 401 when a customer tried to use their session after logout. Selected API responses returned `Cache-Control: no-store`. See [HTTP evidence](api-contract-evidence.json).

The request pipeline builds a global limiter key from the principal or client IP but calls the limiter before authentication. In a run where browser and HTTP requests overlapped, the shared loopback bucket returned 429 to otherwise valid requests. A separated run passed all 49 assertions. Review the production partitioning intent and forwarding configuration; this run does not establish whether clients in production share an IP bucket.

No exploitable vulnerability was confirmed. This is a coverage statement. Exploit payloads for injection, XSS, overposting, replay or privilege bypass were not executed. External provider behavior, production configuration, SSR, and the untested routes are outside the result.
