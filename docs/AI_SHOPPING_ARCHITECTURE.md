# AI Shopping architecture — repository audit, 2026-10-23

## Verified baseline

The working checkout is the `Aljawhara.NET` backend. It targets .NET 10 and uses controllers, JWT bearer authentication, ASP.NET Identity, EF Core 10.0.13 and Npgsql EF 10.0.3. `Application` already references `Infrastructure`; this feature does not expand that existing inversion. Domain contains no AI dependencies. Initial restore/build succeeded, with 12 existing nullable warnings. Initial tests: 104 passed, 154 failed because ECOM_TEST_POSTGRES was absent (258 total). No production connection or website testing was used.

## Commerce facts established from source

* Product: localized title/description, newPrice/oldPrice (decimal(18,3)), active/deleted flags, IsInStock and TotalInStock. Categories/subcategories also have flags. Details are label/value records, not a typed package schema.
* Variants: groups with values and alternative prices, but no variant stock. OrderItemCreateDto has no variant identifier. OrderService.CreateOrderAsync groups by ProductId and uses product.newPrice. Therefore AI must not promise variant checkout; products requiring active variants are excluded until commerce supports it.
* Existing search is an unbounded LIKE query, without Arabic normalization or semantic ranking. It cannot safely serve the bounded hybrid workflow unchanged. The adapter reads the existing product entities and builds a separate projection, never new catalog records.
* No server cart entity, repository, controller or business service exists in this checkout. OrderService is checkout, with stock reservation, coupon, tax/shipping and payment orchestration; it is NOT a cart adapter. In the follow-up integration, the Angular source was found under `../front/Aljawhara`. The real cart is `CartService`/localStorage, and the existing UI is `home/smart-assistant`. It now uses owner-validated, read-only browser-cart preparation followed by an explicit real CartService commit. It does not need a fabricated backend cart.
* KWD rounding is three decimals, away from zero. The core mirrors that currency convention without depending on the application/payment project. Estimated recommendation totals exclude checkout shipping/tax/coupons.
* Existing outbox worker accepts payment/email message types and dead-letters unknown types. AI does not enqueue messages into that worker. An independent durable projection sweep/checkpoint handles indexing, including deletions and detail/category changes.
* Monitoring records route templates/metadata. AI logs only codes, duration and usage, never full messages, catalog descriptions, capability tokens or SDK exceptions.
* Existing application startup automatically migrates commerce and seeds. AI uses a separate migration context/history in the SAME database, applied explicitly by operators; startup never applies AI migrations.
* Existing deploy workflow publishes master without tests. AI work is isolated on a codex branch; there is no production deployment authorization.

## Design boundaries

`Ecom.AIShopping` is a dependency-free class library: contracts, validated plans, deterministic package/budget calculations, conversation orchestration and ports. `Ecom.AIShopping.Infrastructure` implements Responses/embeddings, commerce reads, PostgreSQL state/projection/indexing and DI. API owns HTTP identity/capabilities, request limits, ProblemDetails and admin operations. No circular references or Domain changes.

The model can request only bounded read-only search/details tools. It produces needs and candidate IDs, never authoritative commerce facts. The recommendation builder resolves every selected ID from eligible live catalog rows, computes quantities and totals in decimal, and rejects unknown package quantities for per-person needs. Catalog text is untrusted data. Conversations use optimistic concurrency; stale tabs cannot overwrite newer recommendations. Confirmation uses a durable operation ID and a cart gateway requiring atomic idempotency. Default gateway fails closed with CART_INTEGRATION_REQUIRED because no real service exists here.

The Angular workflow uses `BrowserCartPreparationService` instead of that optional server-cart gateway. It returns live-verified item data and a short preparation deadline without claiming a mutation. The existing CartService adds the approved batch under a Web Lock and stores contents/receipt atomically in its existing storage key, with backward-compatible legacy-array reads. Success is displayed after persistence. No new commerce tables or migrations are needed for this frontend link. The browser state and stock quote are not authoritative for checkout.

Feature is off by default. No OpenAI client, paid call, background indexing or AI database connection is required when disabled. Semantic search is separately switchable; exact pgvector scans are the initial strategy, fused with lexical rankings using RRF(k=60). No approximate vector index is justified without measurements.

Optional pg_trgm candidates support single-typo title matching, including Arabic on C-locale databases through a one-to-one internal key. Material is enforced from explicit catalog details, with curated query translations. FTS/semantic candidate ranking is followed by live name/category concept suitability, variant and stock checks. Selection preserves compatible prior choices, then uses essential-first greedy package totals under budget; it does not claim globally optimal basket optimization. Distinct needs sharing a SKU combine physical units before rounding to packages. Manual quantity overrides survive unchanged needs and reset when people/requirements change.
