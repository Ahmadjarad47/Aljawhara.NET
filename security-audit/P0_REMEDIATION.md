# Aljawhara.NET P0 remediation and operator handoff

## Application behavior

- The API uses its existing ASP.NET Core environment configuration provider for SMTP and Telegram. The local `Ecom.API/Properties/launchSettings.json` supplies process environment variables when a launch profile starts. Git ignores the local file. The [shared launch profile template](../Ecom.API/Properties/launchSettings.example.json) contains no integration credentials. The database connection, R2 settings and bootstrap admin settings continue to come from the process environment.
- SMTP requires `SmtpSettings__Host`, `SmtpSettings__Username`, `SmtpSettings__Password` and `SmtpSettings__FromEmail`. Set `SmtpSettings__Port` when needed. The shared setting remains 465 with TLS on connect. Missing or invalid settings skip delivery and emit a fixed diagnostic. Operators who relied on the removed defaults must inject SMTP settings before relying on account and order emails.
- Telegram defaults to disabled in the shared settings. A launch profile can enable it with `Telegram__Enabled=true`, `Telegram__BotToken` and `Telegram__ChatId`. The existing optional topic, timezone, retry and content-protection settings still apply. A disabled worker leaves pending outbox entries queued. Telegram diagnostics omit provider response text and transport exceptions. Keep request URLs out of any independently configured HTTP tracing because the provider's request path contains the bot token.
- The admin refund endpoint returns `410 Gone` with Problem Details for a valid authorized request. The existing eligibility response has `canRefund: false`. The service rejects direct refund calls, and ordinary admin order and transaction updates reject a requested `Refunded` status. Client integrations should hide or disable refund actions based on eligibility and treat 410 as a terminal response.
- The change removes generated demo refund entries from future seeding. Historical statuses, refund fields, reports and audit data remain readable. There is no schema migration or historical data update.

## Local verification

The selected `Ecom.CheckoutAudit.Tests` suites passed with **88 passed, 0 failed and 0 skipped**. Refund tests exercised the actual admin routes through an in-process HTTP server with synthetic records and repositories. They verified the 410 response, false eligibility, rejection of all three direct status writes, and zero repository updates, saves or gateway calls for both new and previously refunded records. Credential tests covered configuration defaults, environment injection, SMTP rejection of invalid settings, sanitized Telegram failures and disabled outbox processing. Existing checkout, payment, authentication, monitoring and production-seeding guard tests also passed.

Before restoring the values in the Git-ignored local launch profile, a redacted scan of 63 configuration, example and documentation files found no matches for the checked historical credential literals or provider-shaped bot tokens. Provider validity and operator-side logs remain unverified. Testing used synthetic data and isolated test services.

## Files changed for P0

| Area | Files |
| --- | --- |
| Defaults and launch | `Ecom.API/appsettings.json`, `Ecom.API/Properties/launchSettings.json`, `deploy/ubuntu/aljawhara.env.example` |
| SMTP and Telegram | `Ecom.Application/Services/EmailService.cs`, `Ecom.API/Program.cs`, `Ecom.API/HostedServices/TelegramOrderNotificationWorker.cs`, `Ecom.Application/Telegram/TelegramOrderNotificationProcessor.cs`, `Ecom.Application/Telegram/TelegramOrderNotificationService.cs` |
| Refund integrity | `Ecom.API/Controllers/AdminTransactionController.cs`, `Ecom.Application/Services/TransactionService.cs`, `Ecom.Application/Services/OrderService.cs`, `Ecom.Infrastructure/Data/DataSeeder.cs` |
| Test access and regressions | `Ecom.Application/Ecom.Application.csproj`, `tests/Ecom.CheckoutAudit.Tests/CredentialConfigurationTests.cs`, `tests/Ecom.CheckoutAudit.Tests/RefundIntegrityTests.cs` |
| Operator handoff | `security-audit/P0_REMEDIATION.md` |

## Credentials for operator review

Presence in Git or the checkout establishes exposure. Provider validity, prior rotation and environment reuse require operator verification.

| Credential | Redacted evidence | Operator action |
| --- | --- | --- |
| SMTP credentials | Shared settings at `70ee24d` and earlier shared and development snapshots `5f8912a` and `4047f65`. | Revoke or rotate all exposed account credentials that are still valid and check for reuse. |
| Telegram bot token | Pre-remediation working copy of `Ecom.API/appsettings.json`. | Revoke the token and issue a replacement if the integration is still required. Review recent bot and chat activity and the approved chat destination. |
| Sadad credentials | HTTPS launch profile at `70ee24d` and history from `616129e`. | Identify which merchant and environment each exposed pair belongs to, revoke or rotate active pairs, and review provider access and settlement history. |
| Cloudflare R2 credentials | HTTPS launch profile at `70ee24d` and history from `e55272b`. | Revoke or rotate exposed access keys. Issue replacements scoped to the required bucket and operations. |
| Bootstrap administrator password | HTTPS launch profile at `70ee24d` and history from `c97a516`. | Identify any account created with the exposed password, then disable it or reset it and revoke its sessions using the approved identity procedure. Retire the bootstrap setting after provisioning. |
| JWT signing key | Shared settings history at `7f88b26`. | Replace any signing key still in use and coordinate invalidation of JWTs signed with exposed material. |
| Gupshup API key and webhook secret | Sandbox settings history at `5f8912a`. | Revoke exposed keys, rotate any still-used callback secret and review legacy provider registrations. |

The HTTPS launch profile at `70ee24d` also contains a PostgreSQL connection string with a loopback host and a password. Review reuse and rotate that database credential if it is still valid.

## Manual rotation checklist

1. Have the authorized operator identify each affected account and environment using the secret store and provider console. Record account identifiers, evidence references and ownership in the incident record. Keep credential values in the secret store.
2. Revoke exposed credentials at the provider or issuer. When the provider requires a coordinated handoff, issue a least-privilege replacement, install it in the private environment or secret store, verify the handoff and retire the exposed credential promptly. Coordinate the JWT key change across all API instances and document the expected effect on active sessions.
3. For systemd, inject values through the existing root-owned `/etc/aljawhara/aljawhara.env` with mode `0600`. Use the blank fields and placeholders in [the environment example](../deploy/ubuntu/aljawhara.env.example) as a key reference. Use `Sadad__ClientKey` and `Sadad__ClientSecret` as a pair, `R2_KEY` and `R2_SECRET` for object storage, and `JwtSettings__SecretKey` for signing. Supply the database connection through `ConnectionStrings__DefaultConnection`. Enter values through the approved secret-management workflow to avoid storing them in shell history, command-line arguments, tickets or chat.
4. Before any later authorized rollout, verify credential loading with synthetic or sandbox data in an approved environment. Review application and infrastructure logs, traces and diagnostic exports for secret values and credential-bearing URLs. Confirm old credentials are retired and review prior build artifacts, copies and repository history for exposure. Handle any history cleanup as a separate operator decision.
5. Have finance compare historical local refund entries with independent provider settlement evidence. Preserve the original transaction and audit records and record discrepancies through the approved accounting process. Re-enable refunds only after a verified provider workflow or a separately approved, auditable reconciliation flow exists.
