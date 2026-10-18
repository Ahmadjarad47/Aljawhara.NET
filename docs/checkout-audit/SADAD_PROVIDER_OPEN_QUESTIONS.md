# Sadad provider open questions

These questions block sandbox certification or production activation. Written provider answers should be versioned with the integration evidence.

1. Which GetById field is the authoritative amount captured from the customer: `amount`, `total`, `subtotal`, a transaction field, or another field?
2. Provide normative equations for `amount`, `total`, `subtotal`, `discount_Amount`, `discount_Amount_Total`, item totals, shipping and tax for fixed and percentage discounts.
3. Is KWD accepted, returned and compared at exactly three decimal places? What rounding mode is used?
4. Does `currency_Code` describe the amount actually charged? How should the statement “amount is always sent in KWD regardless of invoice currency” be interpreted?
5. Is `vendor_Id` guaranteed to be the authenticated merchant/vendor identity on every GetById response, including old and refunded invoices?
6. What is the exhaustive invoice status enum? Which statuses are terminal, and can Paid ever regress?
7. What is the schema of each `transactions` item? Which field proves a successful captured transaction, and which transaction ID is globally/merchant unique?
8. Can one invoice have multiple successful or partial payment transactions? If so, how is the final captured total calculated?
9. Define `refunded_Amount`, partial/full refund timing, precision and relationship to invoice status and transaction entries.
10. Does webhook support an HMAC signature, asymmetric signature, mTLS, stable source IP range or another authentication mechanism? Include canonicalization and replay-window rules.
11. Does invoice creation support an idempotency key or merchant reference uniqueness constraint? What happens after client timeout when the server completed the create?
12. For GetAll `Ref_Number`, is matching exact and case-sensitive? Is the result complete, strongly consistent and paginated? Can duplicate references exist?
13. Provide the normative GetById and GetAll response schemas in OpenAPI, including nullable/required fields and number formats.
14. What HTTP responses should acknowledge a valid-but-not-yet-paid webhook? Does any 2xx stop retries, or only 200?
15. Are webhook attempts assigned a stable event ID? Is `TransactionId` stable across retries?
16. What are sandbox test methods for Paid, failed, partial payment and refunds without real charges?
17. What production host, TLS requirements, timeouts, rate limits and credential-rotation process are guaranteed?

Until answered and exercised in sandbox, signature validation, discount activation, GetAll recovery and production Paid transitions remain disabled.

