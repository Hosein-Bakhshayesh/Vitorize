# REQ-010 Admin paging inventory

Reviewed against the Admin controllers, read services, and Blazor pages on 2026-07-29.
`Paged` means filtering, stable ordering, count, and page projection are all executed in SQL;
`Small lookup` means a bounded configuration/reference selector rather than an operational list.

| Surface | Current finding | Disposition |
|---|---|---|
| Products | Formerly fetched every product and filtered/paged in the browser. | Migrated in `8b5fdf7`; follow-up validation remains part of REQ-010. |
| Product variants/images | Loaded only for one selected product. | Per-parent bounded detail; not an operational global list. |
| Categories, brands, banners, roles, product tags | Configuration/reference lists with CRUD UI; currently unpaged. | Review as small lookups; do not use as operational dropdown sources. |
| Orders and order items | Orders page formerly fetched all records and paged/filtered in memory. | Migrated to the `paged` SQL contract in `a885f5c`. Order items remain selected-order detail. |
| Payments, refunds, reconciliation | Payment list formerly capped results and filtered/paged in memory. | Migrated to the `paged` SQL contract in `a885f5c`. Refund/reconciliation history remains payment detail. |
| Wallets and wallet transactions | Wallet page formerly requested 500 rows and paged in memory; transactions are loaded per wallet. | Wallet list migrated in `a885f5c`; per-wallet transaction history remains a bounded-detail follow-up. |
| Support tickets/messages/assignments | Ticket page formerly fetched all tickets; messages are per-ticket detail. No assignment model exists. | Ticket queue migrated in `02d3354`; message detail needs bounded paging; assignment is not applicable. |
| Users/customers | Users already use a SQL-backed paged contract. | Verify and retain. |
| Verifications | Page formerly fetched all verification profiles and filtered in memory. | Migrated in `02d3354`. |
| Audit, error, security, login/session logs | Services formerly capped records then paged in the browser. | Audit, error, and security logs migrated in `a885f5c`; login/session records are represented by security logs. |
| SMS history and gift-code codes | Already use SQL-backed `PagedResult`. | Verify and retain. |
| Notifications | Service formerly capped records and UI requested 500. | Migrated in `cd98354`. |
| Coupons | Page formerly loaded all coupons and filtered in memory. | Migrated in `cd98354`. |
| Reports/dashboard | Purpose-built aggregates and explicitly limited recent/top lists, not browseable operational tables. | Not applicable; retain deliberate limits. |
| Media/files | Uploads are product-scoped; no global media browser exists. | Not applicable. |
| Settings/fonts/tools/seed/monitoring | Configuration, diagnostics, or bounded control surfaces. | Not applicable; no operational table paging. |

Exports must be reviewed with each migrated operational list. Current-page, selected-record,
and all-filtered exports must never silently fall back to the Blazor collection.

`eaee518` also replaces the gift-code importer's unbounded product selector with a
dedicated ID/title/slug lookup, capped at 100 rows with selected-value hydration.
