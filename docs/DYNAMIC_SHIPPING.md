# Dynamic shipping

The dashboard page `/admin/shipping` manages the default delivery fee, the optional
free-shipping subtotal threshold, and governorates containing their own area
prices (up to 20 governorates, 100 areas per governorate, 1000 areas total). The
editor suggests areas from the selected governorate's existing Kuwait directory.
Each area has an independent price, even within the same governorate. All amounts are KWD
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
  "governorates": [
    {
      "state": "محافظة الأحمدي",
      "areas": [
        { "city": "الخيران", "fee": 6 },
        { "city": "الفحيحيل", "fee": 3 }
      ]
    },
    {
      "state": "محافظة الجهراء",
      "areas": [{ "city": "المطلاع", "fee": 4 }]
    }
  ]
}
```

PUT replaces the complete governorate and area hierarchy: omitted areas or
governorates revert to the global default fee. Empty governorates are preserved
and use the default for all their areas. An empty governorate list applies the
default everywhere. `freeShippingThreshold: null`
disables the subtotal offer; `0` makes every order eligible. Valid `FreeShipping`
coupons still override delivery fees. The threshold uses the server-calculated
subtotal **before** coupon discounts. Matching uses shipping address `state`
(governorate) **and** `city` (area). Names match after Unicode NFC
normalization, whitespace cleanup, and case folding; spelling variants remain
distinct. The optional Arabic `محافظة ` prefix is ignored for governorate
matching. A supplied governorate never uses an area price from another
governorate. Older addresses with no governorate can still match a uniquely
named area; ambiguous names receive the global default. Missing or unmatched
areas receive the default fee.

Negative amounts, amounts over 1,000,000 KWD, excess decimal precision, blank or
duplicate governorates, duplicate area names within the same governorate, and
incomplete requests return HTTP 400. The same area name can appear under
different governorates with different prices. A stale version
returns HTTP 409; reload and reconcile before retrying. Successful updates return
the saved configuration with an incremented version. Reads are not cached.

The storefront loads the settings when checkout opens, updates estimates when
the address/cart/coupon changes, and does not enable confirmation if settings
cannot load. The backend always recalculates delivery during order creation.
The existing saved-address selection behavior is unchanged.

## Deployment

Deploy the backend and frontend together, applying migrations before serving the
new frontend. The nested `governorates` contract replaces the former top-level
`areas` list. Migration
`20261018202539_DynamicShippingSettings` creates and seeds the configuration with
the previous rules: default 2 KWD, free shipping at 20 KWD, and the seven existing
city overrides. Migration `20261018204115_ShippingGovernorateAreas` adds the
governorate hierarchy, seeds the six governorates, and assigns existing rates
in place without resetting administrator-edited prices, restoring deleted
rates, or changing the free-shipping settings. Existing rates for areas whose
governorate cannot be determined are retained under `مناطق غير مصنفة`; they keep
their legacy matching behavior until the admin moves them to a governorate using
the editor. Downgrading is rejected if duplicate area names across governorates
cannot be represented in the previous flat schema. API startup applies pending
migrations. No live database is
changed by merely building or testing these changes.

## Verification

Backend tests in `ShippingTests` cover seeded defaults, admin authorization,
saved rates used by actual checkout, retained order/idempotency amounts,
threshold boundaries, free-shipping coupons, validation, edit conflicts,
different fees in the same governorate, repeated area names across governorates,
and upgrades preserving edited/deleted/custom rates.
Frontend tests cover grouped dashboard saves/conflicts, adding a governorate,
independent area prices, and checkout recalculation and load failures.
PostgreSQL tests create disposable databases using
`ECOM_TEST_POSTGRES`.

All 102 checkout/backend regression tests and 19 targeted frontend tests pass,
including the migration upgrade tests.
The API builds successfully. The frontend development build completes, while
logging existing prerender errors in unrelated authentication pages. Building
the entire solution also exposes missing references in the unchanged
`Ecom.DatabaseTransfer.Tests.csproj`; that project is outside this change.
