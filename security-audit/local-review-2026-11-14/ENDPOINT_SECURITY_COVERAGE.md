# Endpoint security coverage

The running Development API exposed **214 method/route operations across 183 Swagger paths**. The [live route inventory](live-route-inventory.tsv) gives the complete method/route list and observed HTTP status by evidence source. **47 operations had a recorded response other than a rate-limit rejection; 167 have no recorded live coverage.** The 47 include one monitoring shell operation; the remaining 46 are API operations. This count includes ordinary workflow, fixture and performance requests. Only **21 operations** had explicit defensive HTTP contract assertions. In-process test coverage is reported separately and does not increase the live count.

| Evidence | Unique operations in Swagger | Meaning |
| --- | ---: | --- |
| [Contract assertions](api-contract-evidence.json) | 21 | 49 expectations, including auth, role, validation, logout and category deletion guard |
| [Playwright workflow traffic](browser-evidence.json) | 23 | Browser made an actual request; status alone does not prove all endpoint controls |
| [Fixture HTTP](fixture-http.json) | 5 | Login and administrator fixture creation |
| [Order, review and deletion HTTP](order-and-delete-evidence.json) | 8 | Delivered order transitions, own order read, review creation, and retained history after product deletion |
| [Performance traffic](latency.json) | 3 | Catalog list variants, product detail, and shipping read |
| [Host endpoints](host-route-evidence.json) | 1 | Monitoring shell; health, Swagger and OpenAPI documents also returned 200 outside this Swagger inventory |

These rows overlap. Concrete paths were matched to the most specific method/template, case insensitively. A 401 or 403 is evidence for the boundary in that request, not action-body coverage. A 404 can be an expected domain result, such as a deleted public product or a customer without a review. The source cross-check covered controller route attributes and the mappings in `Program.cs`; static assets and generated documentation are outside the inventory denominator. Swagger security declarations are not evidence of runtime authorization.

JWT login, customer/admin role separation, logout revocation and ordinary request validation were exercised. Full object ownership matrix, every token lifecycle scenario, browser submission of a review, active upload/deletion against object storage, real providers, and exploit-style scenarios are not covered.
