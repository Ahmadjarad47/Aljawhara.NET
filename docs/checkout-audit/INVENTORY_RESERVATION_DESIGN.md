# Inventory reservation design

## Electronic payment

At checkout, each normalized product line is decremented with `WHERE TotalInStock >= quantity` and an `InventoryReservation(Active)` is inserted in the same SQL transaction. The default expiry is 30 minutes. A verified payment confirms every active reservation atomically. Expiry claims each reservation once, restores stock once, releases a reserved coupon once, expires the order, and revokes guest access.

If a Sadad invoice already exists when inventory expires, the attempt becomes `RequiresReconciliation`. Later verified payment also remains reconciliation and cannot advance fulfilment automatically.

## Cash on delivery policy

COD stock is committed at checkout: its reservation is created as `Confirmed`, because fulfilment may start without electronic payment. Cancellation before shipping/delivery conditionally changes the reservation to `Released` and restores stock exactly once. COD uses `TransactionStatus.Pending` until the explicit collection workflow records `Collected`; it is never represented as gateway-verified `Paid`.

## Coupon policy

`UsedCount` represents confirmed plus currently reserved uses. Claiming the last use is one conditional SQL update constrained by active dates and `UsedCount < UsageLimit`. Electronic expiry/cancellation decrements it only after the coupon reservation transition is claimed. Verified payment and COD confirm the reservation without a second increment.

SQL constraints enforce non-negative use counts and `UsedCount <= UsageLimit`. Deployment preflight intentionally fails if legacy data violates these invariants.
