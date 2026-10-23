# AI Shopping API v1 contract

The existing Angular storefront was located at `D:/FreeLance/Ecom.taani/front/Aljawhara`. Its real cart is browser-side `CartService`, persisted in localStorage. Its SmartAssistant now uses **prepare-cart + CartService**, described below. The separate server-cart `/confirm` endpoint remains closed with CART_INTEGRATION_REQUIRED because this application has no server-side cart; the storefront does not call that endpoint. No simulated cart service or checkout-as-cart operation was introduced.

All routes: `/api/ai-shopping`. JSON camelCase, `Cache-Control: no-store`, max body 16 KiB. Feature disabled: 404 AI_SHOPPING_DISABLED, before AI service resolution. Current contracts are generated in the existing development Swagger document. DTO source: `Ecom.AIShopping/Contracts/ShoppingContracts.cs`. Framework-independent TypeScript integration: `docs/ai-shopping/ai-shopping-client.ts`; this is integration code, not a deployed storefront.

## Identity

Authenticated clients send existing JWT bearer tokens. Owners are derived from validated server claims; never supply a user ID or owner hash. Anonymous clients first `POST /sessions` with no body. It returns `{token, header: "X-AI-Shopping-Session", expiresInSeconds:86400}`. Send the opaque server-generated token in that header on subsequent calls. Store only in memory or an appropriately protected session store, never URLs/logs/analytics. Guest ORDER tokens are unrelated and do not grant conversation access. Logging in starts a new user-bound conversation; guest state is not silently transferred. No cookie authentication or ambient cookies are used by the integration client.

## Chat and revisions

`POST /chat`:

```json
{"conversationId":null,"expectedVersion":null,"message":"بدي تجهيزات نزهة لـ 5 أشخاص"}
```

For subsequent messages supply the returned `conversationId` and `version` as `expectedVersion`. Response fields:

| Field | Meaning |
|---|---|
| conversationId / recommendationId | Server UUIDs; recommendationId changes after each revision |
| version | Positive optimistic concurrency version |
| message | Plain text; render with textContent/framework text interpolation |
| status | ready, empty, clarification |
| recommendations | Real productId, null variantId, actual productName/imageUrl, unitPrice, currency=KWD, nullable unitsPerPackage, recommendedPackageQuantity, lineTotal, reason, needKeys |
| estimatedTotal | Decimal KWD merchandise subtotal; excludes checkout shipping, taxes and coupons |
| unavailableNeeds | Plain-text explanations for unmet needs/constraints |
| requiresConfirmation | A proposal needs review; this is NOT authorization to mutate |
| expiresAtUtc | Fixed expiry; activity does not extend it indefinitely |
| usage | Input/output tokens, embeddingInputTokens, combined estimated USD cost, durationMs; see test/deployment limitations |

`GET /conversations/{id}` returns the saved preview, not a fresh stock/price quote. It never returns owner hashes, prompts, histories or private state. Another owner's ID returns 404. Call recalculate to refresh prices/stock.

`POST /conversations/{id}/recalculate`:

```json
{"expectedVersion":1,"items":[{"productId":123,"packageQuantity":2}]}
```

IDs here illustrate the schema only; use IDs from the current response. The list replaces the preview selection: omitted items are removed and excluded from future planning. Only already recommended products are accepted. Duplicate IDs, invalid quantities, new products or variant/price fields are rejected. This refreshes prices and checks stock/budget without paid model calls. Manual quantity overrides survive unchanged needs; changing people/needs recalculates quantities. For replacement use chat, e.g. `بدل الكاسات البلاستيك بدي ورقية`.

## Existing storefront: explicit browser-cart approval

`POST /conversations/{id}/recommendations/{recommendationId}/prepare-cart`:

```json
{"expectedVersion":2,"approved":true,"currentCart":[{"productId":123,"quantity":1}]}
```

The UI calls this only from **إضافة التجهيزات للسلة**. Current cart entries are grouped by ProductId, including variant lines, and contain quantities only. Browser prices/names/extra fields are rejected. Server checks owner, current recommendation/version, expiry, current authoritative price/packaging, and stock after accounting for submitted cart quantities. Response: `{operationId,status:"prepared",expiresAtUtc,replayUntilUtc,items:[{productId,name,image,price,quantity,availablePackages}],unavailableItems:[{productId,requestedQuantity,addedQuantity:0,errorCode}]}`. It does NOT report products as added. Preparation expires in 30 seconds, capped by conversation expiry.

The Angular SmartAssistant then calls the existing `CartService.addApprovedShoppingList`. Under a same-origin Web Lock, it reloads the actual persisted cart, rechecks the observed availability against its latest quantities, checks the preparation deadline and identity guard, reuses existing product/variant merge rules, and commits cart items plus operation receipts in one localStorage write. Only a successful write produces a UI success message. Replay receipts last through replayUntilUtc and survive reload/removal/clear; identical operation IDs never add again while retained. Partial availability adds only verified eligible lines, and the UI reports omissions. Local storage is migrated lazily from the original array to `{schemaVersion:2,items,assistantOperations}` when receipts exist; all cart reads support both shapes.

This browser cart remains a convenience state, never a stock reservation or financial authority. Checkout retains its existing server pricing, inventory, promotion, order and payment validations. Manual cart actions retain their existing synchronous semantics; Web Locks serialize AI approval transactions, not arbitrary writes by older cached frontend versions or browser tools. Refresh older clients after deploying the storage reader. Clearing site storage removes client replay history, as it also removes the cart itself. HTTPS/localhost and Web Locks support are required for AI batch additions; unsupported browsers fail clearly instead of reporting a false success.

The actual Angular integration is in `src/app/core/service/ai-shopping.service.ts`, `src/app/home/smart-assistant/*`, and the existing `CartService`. It uses the configured environment API URL, JWT/guest capabilities, no-store requests, no automatic retries of AI requests, safe Angular text interpolation and `/product/:id` routes. It supports editable package counts, removal, conversational replacement, explicit update before approval, status/errors, and identity-bound cancellation. Attachments were removed because this API supports text input only.

## Optional future server-cart confirmation

`POST /conversations/{id}/recommendations/{recommendationId}/confirm`:

```json
{"expectedVersion":2,"approved":true}
```

Invoke only from **إضافة التجهيزات للسلة**, after customer review. Server verifies ownership, expiry, current version/ID, availability, price and packaging. Changed pricing/packaging: 409 PREVIEW_CHANGED; recalculate, display the changed preview, require approval again. Stock shortages return failed lines; remaining lines can succeed. Gateway must recheck combined cart quantities, rules, availability and authoritative pricing atomically with actual insertion.

Response: `{operationId,status,items:[{productId,requestedQuantity,addedQuantity,errorCode}]}`; status added/partial/unavailable is computed from actual gateway results. Retrying the same recommendationId returns its recorded result, including partial failures. It never repeats successful additions. To reconsider failed items, generate and approve a new preview. Gateway must independently persist operationId because a crash can occur between cart commit and receipt commit. It must bind authenticated/guest owners using a trusted integration; the SHA-256 owner hash alone is not a cart bearer token.

## Errors and storefront behavior

ProblemDetails contains status, title/code and traceId. Relevant codes: INVALID_MESSAGE (400), INVALID_QUANTITY_EDIT (400), EXPLICIT_APPROVAL_REQUIRED (400), SHOPPING_SESSION_REQUIRED/INVALID_SHOPPING_SESSION (401), CONVERSATION_NOT_FOUND (404), STALE_RECOMMENDATION/PREVIEW_CHANGED/PRODUCT_UNAVAILABLE/BUDGET_EXCEEDED (409), AI_RATE_LIMIT/AI_BUSY/AI_BUDGET_EXCEEDED/CONVERSATION_LIMIT (429), INVALID_PLAN/INVALID_TOOL_ARGUMENTS/UNAUTHORIZED_TOOL/TOOL_LIMIT_EXCEEDED (502), AI_PROVIDER_UNAVAILABLE/AI_UNAVAILABLE/CART_INTEGRATION_REQUIRED (503), AI_TIMEOUT (504). Paid model requests are not automatically retried.

The connected Angular SmartAssistant preserves the existing theme and RTL Arabic, uses /product/:id links, 3-decimal KWD, labelled integer package inputs, remove/replace actions, estimated subtotal and unavailable reasons. Approval stays disabled while edits are pending. Loading/error/recovery controls are keyboard-accessible. Existing cart signals update only after actual browser persistence succeeds. Names/messages/reasons use Angular text interpolation; JSON text may contain HTML-looking strings and remains untrusted. Image hosts must be configured in the backend allowlist before deployment. Local automated rendering tests passed; staging validation and deployment remain outstanding.

Admin-only: `POST /index/batch?resetCheckpoint=false` runs one bounded projection batch. Prefer the CLI for complete backfills and status. This is not a public model tool and can incur embedding usage when explicitly configured.
