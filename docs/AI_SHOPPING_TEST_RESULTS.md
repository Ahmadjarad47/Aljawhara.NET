# AI Shopping verification — 2026-10-23

Baseline commit: `4f3c17a`. Working branch: `codex/ai-shopping`. Tests used synthetic catalog records in uniquely named local databases on an isolated PostgreSQL 18 test cluster, never a production connection. No OpenAI charges were generated.

## Executed checks

| Check | Actual result |
|---|---|
| Initial `dotnet restore` / `dotnet build` | Passed; initial clean build had 12 pre-existing nullable warnings |
| Release solution build | Passed; no new AI project warnings/errors (AI libraries treat warnings as errors) |
| Latest Release AI unit/provider/evaluation tests | **48 passed, 1 skipped**, 49 total |
| Latest Release AI PostgreSQL integration tests | **13 passed, 1 skipped**, 14 total |
| Latest Release AI HTTP security tests | **15 passed**, 15 total |
| Combined AI results | **76 passed, 2 skipped, 0 failed**, 78 total |
| Angular assistant HTTP/component/actual CartService tests | **12 passed, 0 failed**, ChromeHeadless |
| Angular full suite | **231 passed, 2 failed**, 233 total; out-of-scope failures listed below |
| Angular production build | Exit 0; existing CSS/CommonJS/Node warnings and prerender errors in other components remain |
| Existing suite, initially without test DB configuration | 104 passed, 154 failed because ECOM_TEST_POSTGRES was unset |
| Existing suite with isolated PostgreSQL and backup tool path | **256 passed, 2 failed**, 258 total; failures listed below |
| NuGet vulnerability audit, including transitive packages | No known vulnerable packages reported for all 11 solution projects using the configured advisory sources |
| Dedicated EF model consistency | No pending AI model changes |
| AI migration SQL generation | Idempotent script generated successfully |
| Migration application and reversal | Executed in PostgreSQL integration tests; commerce rows preserved |
| Lexical-only CLI smoke test | Executed successfully with no AI API key: complete empty-catalog sweep, zero embeddings/errors |
| TypeScript integration client | `tsc --noEmit --strict --target ES2022 --lib ES2022,DOM` passed through npm exec |
| Formatting/whitespace | Targeted whitespace formatting ran; `git diff --check` passed |

The backend full-suite command preceded the storefront integration additions; the latest Release feature run verifies all six new backend cases plus the earlier feature cases. Angular was tested separately against mock HTTP and the actual browser CartService, without paid AI requests. Counts above identify the actual runs instead of claiming a single all-green whole-solution execution.

## Skipped, not executed

* `Pgvector_exact_search_RRF_and_embedding_reuse_work_against_real_extension`: local PostgreSQL did not include pgvector. Local native build could not run because the installed C++ toolchain lacked its headers. The supplied CI workflow provisions a pgvector PostgreSQL service and sets ECOM_TEST_VECTOR=1, but that remote workflow was **not run here**. Semantic runtime correctness and semantic-vs-lexical quality/performance remain release gates.
* `Optional_paid_model_Arabic_planning_evaluation`: explicitly disabled. No paid model accuracy, Arabic intent quality or provider latency claim is made.

## Pre-existing failures

1. `ProductionSeedingTests.Production_does_not_resolve_database_or_create_demo_data`: the existing DataSeeder resolves EcomDbContext even in production. This failed before feature implementation and remains unchanged. Review this production data-seeding behavior before deployment.
2. `WhatsAppInvoiceTests.Arabic_renderer_produces_searchable_paginated_pdf`: the required Playwright Chromium headless-shell executable was absent. A dependency download was attempted but did not complete; it was stopped. Invoice renderer code was not changed.

The baseline backup test initially also failed because it defaulted to `/usr/bin` on Windows; it passed after setting ECOM_TEST_PG_BIN to the installed PostgreSQL tools. This was environment setup, not a code fix.

### Angular full-suite failures outside the edited feature

1. `Account address forms creates addresses with the same nine fields as checkout and only three required`: unchanged address settings test expects nine fields but finds ten.
2. `My orders status display separates preparation from payment in desktop, mobile and order details without offering payment again`: unchanged test setup lacks a HttpClient provider for CheckoutPaymentService.

No frontend baseline full-suite run was performed before editing, so these are reported as failures in unchanged/out-of-scope tests rather than proven pre-existing failures. All 12 new assistant tests passed. The production Angular build also logs prerender exceptions involving browser globals in other components; exit 0 does not establish clean SSR across the storefront.

## Evidence and reproducible commands

Local ignored artifacts contain raw results:

* `artifacts/ai-latest-tests.log` and `artifacts/ai-latest-results/*.trx`: final Release feature checks.
* `artifacts/ai-storefront-tests.log`: latest 76-pass backend feature run including browser-cart preparation and HTTP boundary checks.
* `artifacts/ai-frontend-backend-build.log`: Release backend build after adding preparation.
* Frontend repository `tmp/ai-assistant-tests.log`, `tmp/ai-full-tests.log`, `tmp/ai-assistant-final-build.log`: Chrome feature tests, full suite, production build.
* `artifacts/ai-final-tests.log` and `artifacts/ai-final-results/*.trx`: full solution run and existing failure evidence.
* `artifacts/ai-baseline-tests.log`, `artifacts/ai-baseline-postgres-tests.log`: baseline runs.
* `artifacts/ai-dependency-audit.log`, `artifacts/ai-final-build.log`, `artifacts/ai-shopping-migration.sql`, `artifacts/ai-cli-smoke.log`.

```powershell
$env:ECOM_TEST_POSTGRES='Host=localhost;Port=5432;Database=postgres;Username=<test-role>;Password=<test-secret>'
$env:ECOM_TEST_PG_BIN='<installed PostgreSQL bin directory>'
$env:ECOM_ALLOW_PAID_AI_TESTS='0'
# Set only when pgvector is installed on that dedicated test server:
$env:ECOM_TEST_VECTOR='1'
dotnet restore Ecom.taani.slnx
dotnet build Ecom.taani.slnx --no-restore -c Release
dotnet test Ecom.taani.slnx --no-build -c Release --filter 'FullyQualifiedName~Ecom.AIShopping' --logger trx
dotnet test Ecom.taani.slnx --no-build -c Release
dotnet list Ecom.taani.slnx package --vulnerable --include-transitive
```

The test role needs CREATEDB; vector tests also require extension permissions. Tests create unique databases instead of reusing the supplied database. Never supply a production server. Default CI fixture tests use no OpenAI account. Paid tests require BOTH ECOM_ALLOW_PAID_AI_TESTS=1 and AIShopping__ApiKey; enable them only with explicit authorization and cost controls.

## Evaluations and measured limits

The ten Arabic scenarios are saved in `docs/ai-shopping/evaluation-scenarios.json`. They include picnic/birthday/budget, paper replacement, unavailable products, changed people, package comparison, dialect terms, price injection and cross-user privacy. Deterministic tests feed EXPECTED structured plans into the real downstream recommendation code. They establish quantity, grounding, budget and unavailable-item invariants; they do **not** establish language understanding. Separate HTTP/provider tests establish authorization and tool execution boundaries.

Saved measured reports: `docs/ai-shopping/evaluation-results.json` and `retrieval-results.json`. Latest run:

* Ten deterministic downstream scenarios passed; mean 1.73 ms, maximum 14.81 ms, paid calls/cost zero.
* Five lexical queries over a two-product PostgreSQL fixture found the expected product in the first eight results in all five cases. Mean 7.10 ms; observed p95/max 14.74 ms. This tiny fixture is a regression test, not a production benchmark or a statistically meaningful latency study.
* Optional pg_trgm Arabic typo/material constraints executed successfully against PostgreSQL, including the C-locale normalization correction.
* Actual browser CartService integration, safe Angular rendering and explicit approval were tested in Chrome. No end-to-end staging session with a live model, manual storefront accessibility audit, semantic benchmark or production-size throughput measurement was executed. The optional server-cart adapter is still absent/unused. Measure product relevance/coverage, semantic ranking, p50/p95 latency and token costs on staging data before release.
