# REQ-010 Admin paging inventory

Reviewed against the Admin controllers, read services, and Blazor pages on 2026-07-29.
`Paged` means filtering, stable ordering, count, and page projection are all executed in SQL;
`Small lookup` means a bounded configuration/reference selector rather than an operational list.

| Surface | Current finding | Disposition |
|---|---|---|
| Products | Formerly fetched every product and filtered/paged in the browser. | Migrated in `8b5fdf7`; follow-up validation remains part of REQ-010. |
| Product variants/images | Loaded only for one selected product. | Per-parent bounded detail; not an operational global list. |
| Categories, brands, banners, roles, product tags | Configuration/reference lists with CRUD UI; currently unpaged. | Review as small lookups; do not use as operational dropdown sources. |
| Orders and order items | Orders page fetches all records and pages/filters in memory. | Migrate. Order items stay in selected order detail. |
| Payments, refunds, reconciliation | Payment read service caps results and the page filters/pages in memory. | Migrate. Refund/reconciliation history stays in payment detail. |
| Wallets and wallet transactions | Wallet page requests 500 rows and pages in memory; transactions are loaded per wallet. | Migrate wallets; keep per-wallet detail bounded/paged. |
| Support tickets/messages/assignments | Ticket page fetches all tickets; messages are per-ticket detail. No assignment model exists. | Migrate tickets; message detail needs bounded paging; assignment is not applicable. |
| Users/customers | Users already use a SQL-backed paged contract. | Verify and retain. |
| Verifications | Page fetches all verification profiles and filters in memory. | Migrate. |
| Audit, error, security, login/session logs | Services cap records then pages in the browser. | Migrate all operational logs. |
| SMS history and gift-code codes | Already use SQL-backed `PagedResult`. | Verify and retain. |
| Notifications | Service caps records and UI requests 500. | Migrate. |
| Coupons | Page loads all coupons and filters in memory. | Migrate. |
| Reports/dashboard | Purpose-built aggregates and explicitly limited recent/top lists, not browseable operational tables. | Not applicable; retain deliberate limits. |
| Media/files | Uploads are product-scoped; no global media browser exists. | Not applicable. |
| Settings/fonts/tools/seed/monitoring | Configuration, diagnostics, or bounded control surfaces. | Not applicable; no operational table paging. |

Exports must be reviewed with each migrated operational list. Current-page, selected-record,
and all-filtered exports must never silently fall back to the Blazor collection.
