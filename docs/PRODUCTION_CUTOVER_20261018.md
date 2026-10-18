# Production cutover and prelaunch reset — 2026-10-18

Executed at the user's explicit request to move the online site to `ecom_prod`, preserving products and clearing prelaunch purchase/review data.

- Live process connection verified: `ecom_prod`.
- Products: **584**, with complete-row fingerprints identical to the original database.
- Product details (14), variants (42), variant values (57), categories (16), subcategories (36), carousels (5), users (3), roles/links, coupons (15), addresses and other retained tables also reconciled by full-row fingerprints.
- Orders, order items, transactions, payment attempts/audits, reservations, outbox, order counters and ratings: **zero** after startup.
- Removed timestamp-archive entries belonging to cleared tables; retained product/user/etc. precision records.
- Product stock, prices, IDs, images and descriptions preserved without stock adjustments.
- `ecom_migration_test` preserved unchanged as a recovery source.
- Production demo seeding disabled in DataSeeder; dedicated test passed before deployment. Only the infrastructure assembly was deployed, preserving the running API and its configuration.
- Internal health: Healthy. Public HTTPS health: HTTP 200.

Recovery materials on VPS (root-only directory):
`/root/ecom-backups/prod-cutover-20261018T124141Z/`

Contains both database backups/manifests, original infrastructure DLL, original service unit, before fingerprints, and `result.json`. Earlier complete backup: `/root/ecom-backups/before-online-reset-20261018T123152Z.dump`.

Production connection override: `/etc/systemd/system/aljawhara.service.d/90-production-database.conf`, using `/etc/aljawhara/production-database.env` (credentials not recorded here).

One preliminary restore failed while restoring FK metadata after pg_dump changed search_path; its data transaction rolled back. The corrected restore, clearing transaction, preserved-table comparison, service startup, live connection verification and health checks all succeeded. No database was dropped or recreated. Existing source data was not deleted.

The earlier legacy migration report describes the prior test-only migration; this later explicitly authorized production cutover supersedes its production-pending status.
