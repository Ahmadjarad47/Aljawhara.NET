# Security test execution log

**Date** 12 November 2026, Asia/Damascus.  
**Scope** Local working tree at base revision `70ee24d`, including uncommitted application changes. No changes were made to the application source or configuration.

## Executed tests

| Run | Fixture and coverage | Observed result |
| --- | --- | --- |
| T-01 | Existing checkout unit and controller tests. Synthetic order and user fixtures, stub repositories, and fake gateway HTTP handlers. Covers order-owner and guest-capability checks, expired and revoked capabilities, payment verification access checks, disabled legacy payment, server-derived transaction amounts, stock validation, and administrative paid-transition checks. [Guest fixtures](../tests/Ecom.CheckoutAudit.Tests/GuestAccessTests.cs) · [Controller fixtures](../tests/Ecom.CheckoutAudit.Tests/AuthorizationAndControllerTests.cs) · [Payment fixtures](../tests/Ecom.CheckoutAudit.Tests/BusinessAndPaymentTests.cs) | **42 passed**, 0 failed, 0 skipped. Exit 0. The application and tests compiled with nullable reference warnings. |
| T-02 | Existing monitoring suite. `WebApplicationFactory` hosts ASP.NET Core in process using `Testing`. Health checks are replaced with a stub, authentication uses the test scheme, and log storage uses per-fixture temporary paths. Checks the public dashboard shell, Admin authorization on both monitoring endpoints, error handling, redaction, bounded queries and log retention. [Fixture](../tests/Ecom.CheckoutAudit.Tests/MonitoringTests.cs#L176) | **13 passed**, 0 failed, 0 skipped. Exit 0. The role tests asserted 401 for anonymous, 403 for a normal test user and 200 for an Admin on both monitoring data routes. The fixture substitutes authentication and therefore provides no evidence about JWT issuance or revocation. |
| T-03 | Existing reverse-proxy middleware tests using in-memory `DefaultHttpContext` instances with loopback and synthetic IP values. Checks trusted proxy processing and rejection of forwarded metadata from untrusted addresses. [Fixture](../tests/Ecom.AIShopping.SecurityTests/ReverseProxyTests.cs) | **5 passed**, 0 failed, 0 skipped. Exit 0. No proxy server or network socket was needed. |
| T-04 | Existing production-seeding guard test. Supplies a `Production` environment and no database registration. [Fixture](../tests/Ecom.CheckoutAudit.Tests/ProductionSeedingTests.cs#L10) | **1 passed**, 0 failed, 0 skipped. Exit 0. Production seeding returned without resolving the database. |

The runs used .NET SDK `10.0.401` and .NET runtime `10.0.12` on Windows. The package cache and existing restore assets supported local builds with `--no-restore`. The monitoring run explicitly set `AIShopping__Enabled`, `AIShopping__SemanticEnabled`, `Telegram__Enabled`, and `DatabaseBackup__Enabled` to `false` in its process. Startup disables Gupshup delivery and skips migration, demo seeding and payment background workers in `Testing`. The exercised actions use the in-process server or direct service calls. No external provider or network target was contacted. [Test startup conditions](../Ecom.API/Program.cs#L204) · [Migration condition](../Ecom.API/Program.cs#L547)

```powershell
dotnet test tests/Ecom.CheckoutAudit.Tests/Ecom.CheckoutAudit.Tests.csproj --no-restore --nologo --filter 'FullyQualifiedName~GuestAccessTests|FullyQualifiedName~AuthorizationAndControllerTests|FullyQualifiedName~BusinessAndPaymentTests' -v quiet

$env:AIShopping__Enabled = 'false'
$env:AIShopping__SemanticEnabled = 'false'
$env:Telegram__Enabled = 'false'
$env:DatabaseBackup__Enabled = 'false'
dotnet test tests/Ecom.CheckoutAudit.Tests/Ecom.CheckoutAudit.Tests.csproj --no-restore --no-build --nologo --filter 'FullyQualifiedName~MonitoringTests' -v quiet

dotnet test tests/Ecom.AIShopping.SecurityTests/Ecom.AIShopping.SecurityTests.csproj --no-restore --nologo --filter 'FullyQualifiedName~ReverseProxyTests' -v quiet

dotnet test tests/Ecom.CheckoutAudit.Tests/Ecom.CheckoutAudit.Tests.csproj --no-restore --no-build --nologo --filter 'FullyQualifiedName~ProductionSeedingTests' -v quiet
```

## Source review coverage

| Check | Method and outcome |
| --- | --- |
| Route and role inventory | Enumerated 213 HTTP action attributes in 24 controllers, resolved route templates and class/action access metadata, and reconciled the count with an independent source search. Reviewed host mappings and framework-served files separately. [Inventory](ENDPOINT_INVENTORY.md) |
| Authentication and session handling | Traced login, account confirmation, logout, password reset and password change, lockout, JWT signing and validation, refresh lookup and rotation. Reviewed response and log flows. AS-01, AS-02, AS-03, AS-06 and AS-12 contain the resulting source evidence. |
| Object authorization and overposting | Traced customer address operations, transaction filtering, order ownership, guest capabilities, coupon ownership, admin routes and upload/delete DTOs. Order and address ownership checks use server-side identifiers on the reviewed paths. Public coupons and product image deletion produced AS-04 and AS-13. [Address checks](../Ecom.Application/Services/AddressService.cs#L25) · [Transaction filter](../Ecom.API/Controllers/TransactionsController.cs#L25) · [Guest order access](../Ecom.Application/Services/OrderService.cs#L791) |
| Payment and checkout | Traced pricing, quantity validation, stock and coupon reservations, checkout replay, state transitions and provider verification. The observed source computes payment values from database data. AS-05 and AS-09 cover refund integrity and public verification triggers. PostgreSQL concurrency, COD abuse controls and provider contracts were outside executed coverage. [Checkout pricing](../Ecom.Application/Services/OrderService.cs#L167) · [Database invariants](../Ecom.Infrastructure/Data/EcomDbContext.cs#L388) |
| SQL and deserialization | Reviewed EF and Npgsql call sites in runtime projects. Interpolated SQL uses parameterizing EF APIs, explicit Npgsql search commands use parameters, and reviewed raw SQL was fixed text. Webhook input is parsed as structured JSON. No confirmed SQL injection or type-directed deserialization in the paths inspected. [AI retrieval](../Ecom.AIShopping.Infrastructure/Retrieval/PostgresProductRetrieval.cs#L29) · [Indexer SQL](../Ecom.AIShopping.Infrastructure/BackgroundJobs/ShoppingSearchIndexService.cs#L68) · [WhatsApp JSON handling](../Ecom.API/Controllers/WhatsAppController.cs#L41) |
| Output, uploads and outbound boundaries | Traced HTML mail, CSV, public reviews, monitoring DOM writes, catalog uploads, signed PDF access, browser PDF generation, and provider requests. Observed HTML and spreadsheet injection sinks and missing content validation. Reviewed outbound destinations are selected by service configuration or fixed in code. The PDF renderer blocks browser network routes. No confirmed user-selected server-side fetch or filesystem traversal in the traced paths. [Assessment](SECURITY_ASSESSMENT.md#1-confirmed-vulnerabilities-and-security-defects) |
| Host and integration configuration | Reviewed forwarded headers, CORS, API cache control, HSTS, CSP, health, exception handling, log redaction, startup DB privileges, backup options, integration feature switches and local deployment files. No deployed proxy configuration, database grants, or R2 bucket policy was available for validation. No Redis registration or Angular source was found. |
| Dependencies | Read runtime project references and locally resolved NuGet assets for all six runtime projects. Checked cached restore diagnostics for `NU19xx` advisory entries and found none in those assets. Current advisory status remains unknown because the review did not fetch a fresh advisory database. No npm lockfile or package manifest was present. |

### Credential review

The current configuration and historical Git snapshots were inspected locally. The checks reported field names, revision and line references, credential-shape indicators, and equality with `HEAD`. Credential values, connection strings and tokens are omitted.

| Evidence | Observed result |
| --- | --- |
| Current `Ecom.API/appsettings.json` line 46 | A non-placeholder SMTP password is configured alongside nonempty host and username fields. The password matches the value in `HEAD`. |
| Current `Ecom.API/appsettings.json` lines 57–58 | Telegram is enabled and the bot token matches the provider token shape. The token is absent from `HEAD`. |
| `7f88b26`, `Ecom.API/appsettings.json` line 6 | Historical nonempty JWT signing material in source. |
| `5f8912a`, `Ecom.API/appsettings.json` lines 12 and 46 | Historical nonempty API key and password fields. |
| `4047f65`, `Ecom.API/appsettings.Development.json` line 18 | Historical nonempty password field. |

Historical checks covered the two main API settings files and are not an exhaustive scan of every Git object. No credential validity, provider access or live rotation status was tested. The configured workflow removes appsettings files from published output before transfer. Git history, developer checkouts and any separately copied configurations remain relevant exposure paths. [Publish workflow](../.github/workflows/deploy.yml#L28)

## Unexecuted validation

| Activity | Reason and evidence boundary |
| --- | --- |
| Active DAST, vulnerability reproduction and unauthorized behavior trials | Outside the executed scope. Source findings are traceable to reachable actions and branches. The test results above are evidence for existing defensive controls. |
| Stand-alone PostgreSQL API, real login and token refresh/revocation, checkout concurrency, coupon and inventory contention | A disposable PostgreSQL deployment with isolated integrations was unverified. Normal startup outside `Testing` migrates and seeds the database. Database-backed tests and the repository's end-to-end HTTP security suite were excluded. |
| Sadad, SMTP, OpenAI, Cloudflare R2, Telegram and Gupshup requests | These are external integrations. Provider-dependent claims are limited to code behavior and conditional impact. |
| Angular XSS, browser cookie/CSRF, Redis, live reverse proxy and object headers | The corresponding frontend, Redis integration, and deployed configuration were unavailable locally. In-memory proxy tests exercised the application trust settings. |
| Current NuGet/npm vulnerability check | Fresh NuGet advisory data was unavailable in the offline audit. There was no npm project to audit. |
