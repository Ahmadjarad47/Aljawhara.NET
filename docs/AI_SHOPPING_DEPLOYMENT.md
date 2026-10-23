# AI Shopping deployment and rollback

## Release gate

Feature defaults to OFF in `AIShoppingOptions`. It has not been deployed. The existing Angular SmartAssistant and browser CartService are now connected through prepare-cart, with automated tests. Before enabling the public feature, finish verified package metadata, production-like retrieval evaluation, live-model evaluation and operational security checks below. The existing application runs commerce migrations and demo seeding at startup; this is a pre-existing deployment risk, not changed by AI. The separate AI context is NEVER automatically migrated by API startup.

## Dependencies and configuration

Same PostgreSQL database, `ai_shopping` schema, separate `__EFMigrationsHistory`. Existing .NET 10 runtime/Npgsql. Maintained official OpenAI .NET SDK pinned to 2.14.0; Responses surface is still annotated OPENAI001 by that SDK, suppressed only inside the adapter/tests. No Redis, microservice, separate production database or agent framework.

Supply secrets only from the deployment environment/approved secret store:

```text
AIShopping__Enabled=false
AIShopping__ApiKey=<secret>
AIShopping__Model=gpt-6-luna
AIShopping__EmbeddingModel=text-embedding-3-small
AIShopping__EmbeddingDimensions=1536
AIShopping__SemanticEnabled=false
AIShopping__TrigramEnabled=false
AIShopping__MinimumTrigramSimilarity=0.35
AIShopping__MaximumCosineDistance=0.65
AIShopping__IndexingEnabled=false
AIShopping__MaxIterations=4
AIShopping__MaxToolCalls=8
AIShopping__MaxOutputTokens=2500
AIShopping__MaxInputTokens=32000
AIShopping__MaxTurnEstimatedUsd=0.03
AIShopping__InputUsdPerMillion=0.10
AIShopping__OutputUsdPerMillion=0.50
AIShopping__EmbeddingUsdPerMillion=0.02
AIShopping__TimeoutSeconds=45
AIShopping__MaxConcurrentRequests=4
AIShopping__RequestsPerMinute=8
AIShopping__IpRequestsPerMinute=20
AIShopping__CandidateLimit=40
AIShopping__IndexBatchSize=25
AIShopping__IndexIntervalSeconds=60
AIShopping__Limits__MaxMessageLength=2000
AIShopping__Limits__MaxTurns=30
AIShopping__Limits__MaxNeeds=15
AIShopping__Limits__MaxPackages=100
AIShopping__Limits__HistoryTurns=6
AIShopping__Limits__ConversationMinutes=120
AIShopping__ImageHosts__0=aljawharaplus.com
ConnectionStrings__DefaultConnection=<existing database secret>
```

Add the ACTUAL trusted R2/custom CDN hostname to ImageHosts; no wildcard host authorization. Unlisted URLs are omitted and are never fetched server-side. Persist ASP.NET Data Protection keys securely, with suitable at-rest protection and a consistent application discriminator/key ring on every replica; guest capabilities otherwise become invalid after restart or between replicas. Do not change existing identity key settings without reviewing their effect on Identity tokens.

Pricing rates are configurable estimates, not billing guarantees. Switching to `gpt-6.1-sol` requires updating input/output USD rates and MaxTurnEstimatedUsd; the default budget is tuned to Luna and may reject stronger models. Reasoning is low, automatic paid retries are disabled, response storage is false. Provider abuse-monitoring/data-retention policies still apply; store:false is not a promise of zero retention. Review account-specific data controls before sending customer messages. See [function calling](https://developers.openai.com/api/docs/guides/function-calling?api-mode=responses), [structured output](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses), [Luna](https://developers.openai.com/api/docs/models/gpt-6-luna) and [embeddings](https://developers.openai.com/api/docs/guides/embeddings).

## Migrations and backfill

Review generated SQL on staging using a deployment role. Back up the database. NEVER point test variables at production. Apply the dedicated migration explicitly:

```powershell
dotnet ef migrations script --project Ecom.AIShopping.Infrastructure --context AIShoppingDbContext --idempotent --output artifacts/ai-shopping-migration.sql
# Review script before this deployment step:
dotnet ef database update --project Ecom.AIShopping.Infrastructure --context AIShoppingDbContext
```

This creates only AI-owned tables/indexes. No commerce FK/schema/model modifications. Lexical full-text GIN index uses normalized Arabic with simple tokenization. For semantics, provision pgvector on the existing server, review and explicitly apply `Ecom.AIShopping.Infrastructure/Persistence/Scripts/enable-semantic.sql` via the approved database tooling. It installs vector if permitted and adds an optional 1536-dimensional column. No HNSW/IVFFlat index. [pgvector installation and exact search](https://github.com/pgvector/pgvector).

Optional typo tolerance: review/apply `enable-trigram.sql`, then set TrigramEnabled=true. A one-to-one internal ASCII key allows Arabic word comparisons even with a C database locale; product names remain unchanged. Title/concept and verified material checks still apply after candidate retrieval. Compare typo recall before adding another index; current catalog-size assumptions use a bounded exact scan.

Run lexical backfill with SemanticEnabled=false; an API key is not needed by the lexical command. Indexing public traffic remains off during initial backfill. The CLI does not start hosted workers:

```powershell
dotnet run --project tools/Ecom.AIShopping.Cli -- reindex
dotnet run --project tools/Ecom.AIShopping.Cli -- status
dotnet run --project tools/Ecom.AIShopping.Cli -- index
```

`reindex` starts a complete sweep from ID zero; content hashes still reuse unchanged embeddings. `index` resumes a checkpoint and finishes the sweep. No price/stock changes regenerate embeddings. Semantic mode intentionally generates paid embeddings when enabled; approve/set a provider budget before backfill. Checkpoint progress is durable. A database advisory lock prevents concurrent sweep writers. Per-document exponential delay with jitter retains failures and allows lexical data to remain searchable. Retries happen on later sweeps, not unbounded synchronous provider retries. Category, detail, variant and deletion changes are found by repeated bounded sweeps; worst-case freshness depends on catalog size/batch interval. This polling projection is eventual, while price/stock/eligibility reads are live. Use status/structured logs to monitor failed documents and last sweep time. Physically deleted rows are purged after complete sweeps.

## Cart integration gate

For this storefront, deploy the updated backend and Angular code together. The UI uses prepare-cart and its existing localStorage CartService; no server-cart gateway is required. The configured `environment.apiUrl` must point to the API deployment containing the new routes. Apply/backfill the AI schema as above, supply the OpenAI key/model settings, configure real image hosts, and explicitly enable AIShopping. Frontend cart readers support the old array plus the new receipt envelope. Refresh older cached clients when deploying this storage reader; rolling back the frontend to a reader that only supports arrays requires migrating any `{items,assistantOperations}` value back to its items array, which loses client receipts. Do not initiate such a migration while approvals are running.

If a future backend cart is introduced, it must implement IShoppingCartGateway with trusted owner mapping, atomic cart checks/idempotency and cancellation. The optional /confirm endpoint stays closed until then. Do not replace its default gateway with an in-memory cart and do not wire this browser storefront to it.

## Limits, retention, operations

Current request/concurrency limiters are process-local and suited to the existing single API service. They are not aggregate account-wide limits across replicas; enforce an upstream aggregate budget/rate policy before scaling out. Direct client IP is used. Configure trusted reverse-proxy forwarding at the existing host boundary; never trust arbitrary forwarding headers. New guest tokens cannot escape the per-IP limiter. Conversations have fixed expiry and max turns/history. The worker removes expired rows while enabled, with or without indexing. When disabled, run an operator retention cleanup for expired AI state if the service will stay off. No model history is authoritative for cart or prices.

Metrics: existing request latency/trace metadata, model input/output tokens and USD estimate, embedding tokens and combined request estimate, projection scanned/changed/embedded/failure counts, maintenance/semantic-fallback codes. No full prompts or SDK errors are logged. A semantic failure falls back to lexical and suppresses further semantic attempts for that request. Compare FTS vs semantic vs RRF on a representative catalog and measure p50/p95 latency before enabling semantics; two-product fixtures cannot justify production performance conclusions.

## Rollback

1. Set Enabled=false (reload configuration where supported, or restart the API). Public AI routes stop before provider/database resolution. Stop CLI backfills separately.
2. Commerce checkout and payment paths are unchanged. Preserve AI data temporarily for investigation if needed.
3. To remove optional vectors, disable semantic/index workers and review/apply `disable-semantic.sql`. It does NOT drop the shared extension.
4. To remove AI tables, review a reverse migration script and explicitly migrate AIShoppingDbContext to `0`. It removes only AI tables; no commerce records are changed. Dropping AI conversations/receipts loses their history, so wait for confirmation retry/retention windows first. Cart adapter operation receipts must still prevent replays independently.

## Before production

Validate the linked storefront against a configured staging API, package/material detail coverage, variant exclusions and real catalog suitability. Execute the pgvector CI job and paid evaluation in staging with explicit authorization. Verify HTTPS/Web Locks, localStorage persistence/replay, aggregate cost caps, Data Protection persistence, reverse-proxy IP handling, retention cleanup, CDN allowlist, OpenAI account/model access/current rates and alert thresholds. Resolve or consciously accept existing baseline failures. Do not treat local integration tests as production release approval.
