# Dynamic shipping

The dashboard page `/admin/shipping` manages the default delivery fee, the optional
free-shipping subtotal threshold, and up to 100 city overrides. All amounts are KWD
with at most three decimal places. Changes apply to new orders; existing orders,
payment retries, and idempotent checkout retries retain their stored amounts.

## API

- `GET /api/shipping`: public settings for checkout estimates and shipping offers.
- `GET /api/admin/shipping`: settings for the editor; requires the `Admin` role.
- `PUT /api/admin/shipping`: replace the configuration; requires the `Admin` role.

Example PUT body (use the version returned by the latest GET):

```json
{
  "version": 1,
  "defaultFee": 2,
  "freeShippingThreshold": 20,
  "areas": [
    { "city": "الخيران", "fee": 6 },
    { "city": "المطلاع", "fee": 4 }
  ]
}
```

PUT replaces the complete area list: omitted areas revert to the default fee.
An empty list applies the default to every city. `freeShippingThreshold: null`
disables the subtotal offer; `0` makes every order eligible. Valid `FreeShipping`
coupons still override delivery fees. The threshold uses the server-calculated
subtotal **before** coupon discounts. City names match after Unicode NFC
normalization, whitespace cleanup, and case folding; spelling variants remain
distinct. A missing or unmatched city receives the default fee.

Negative amounts, amounts over 1,000,000 KWD, excess decimal precision, blank or
duplicate city names, and incomplete requests return HTTP 400. A stale version
returns HTTP 409; reload and reconcile before retrying. Successful updates return
the saved configuration with an incremented version. Reads are not cached.

The storefront loads the settings when checkout opens, updates estimates when
the address/cart/coupon changes, and does not enable confirmation if settings
cannot load. The backend always recalculates delivery during order creation.
The existing saved-address selection behavior is unchanged.

## Deployment

Deploy the backend and migration before the frontend. Migration
`20261018202539_DynamicShippingSettings` creates and seeds the configuration with
the previous rules: default 2 KWD, free shipping at 20 KWD, and the seven existing
city overrides. API startup applies pending migrations. No live database is
changed by merely building or testing these changes.

## Verification

Backend tests in `ShippingTests` cover seeded defaults, admin authorization,
saved rates used by actual checkout, retained order/idempotency amounts,
threshold boundaries, free-shipping coupons, validation, and edit conflicts.
Frontend tests cover dashboard form saves/conflicts and checkout recalculation
and load failures. PostgreSQL tests create disposable databases using
`ECOM_TEST_POSTGRES`.

Validation on this change: 97 of 98 checkout/backend tests passed in the full
run; the remaining backup test passed on rerun after setting `ECOM_TEST_PG_BIN`
to the installed PostgreSQL tools. All 17 targeted frontend tests passed.
The API builds successfully. The frontend development build completes, while
logging existing prerender errors in unrelated authentication pages. Building
the entire solution also exposes missing references in the unchanged
`Ecom.DatabaseTransfer.Tests.csproj`; that project is outside this change.
