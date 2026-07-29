# REQ-010 Admin paging inventory

Reviewed against the Admin controllers, read services, and Blazor pages on 2026-07-29.
`Paged` means filtering, stable ordering, count, and page projection are all executed in SQL;
`Small lookup` means a bounded configuration/reference selector rather than an operational list.

| Surface | Current finding | Disposition |
|---|---|---|
| Products | Formerly fetched every product and filtered/paged in the browser. | Migrated in `8b5fdf7`; follow-up validation remains part of REQ-010. |
| Product variants/images | Product details previously loaded all child variants and media. | Migrated to SQL-backed child paging in `9016c4a`; page size is capped at 100, ordered by `SortOrder` plus stable keys. Product edit and media-management screens remain in the final detail-screen reconciliation. |
| Categories, brands, banners, roles, product tags | Configuration/reference lists with CRUD UI; currently unpaged. | Review as small lookups; do not use as operational dropdown sources. |
| Orders and order items | Orders page formerly fetched all records and paged/filtered in memory. | Migrated to the `paged` SQL contract in `a885f5c`. Order items remain selected-order detail. |
| Payments, refunds, reconciliation | Payment list formerly capped results and filtered/paged in memory. | List migrated in `a885f5c`; refunds and financial audit/reconciliation history now use independent SQL child paging in `d20a3f8`. Raw provider callback payloads are intentionally not exposed to the Admin UI. |
| Wallets and wallet transactions | Wallet page formerly requested 500 rows and paged in memory; transactions are loaded per wallet. | Wallet list migrated in `a885f5c`; transaction history is SQL-paged, capped at 100, in `a3bdcd5`. |
| Support tickets/messages/assignments | Ticket page formerly fetched all tickets; messages are per-ticket detail. No assignment model exists. | Ticket queue migrated in `02d3354`; message detail is independent SQL paging, capped at 100, in `22b42a2`. Assignment history is not applicable because no assignment model or workflow exists. |
| Users/customers | Users already use a SQL-backed paged contract. | Verify and retain. |
| Verifications | Page formerly fetched all verification profiles and filtered in memory. | Migrated in `02d3354`. |
| Audit, error, security, login/session logs | Services formerly capped records then paged in the browser. | Audit, error, and security logs migrated in `a885f5c`; login/session records are represented by security logs. |
| SMS history and gift-code codes | Already use SQL-backed `PagedResult`. | Verify and retain. |
| Notifications | Service formerly capped records and UI requested 500. | Migrated in `cd98354`. |
| Coupons | Page formerly loaded all coupons and filtered in memory. | Migrated in `cd98354`. |
| Reports/dashboard | Purpose-built aggregates and explicitly limited recent/top lists, not browseable operational tables. | Not applicable; retain deliberate limits. |
| Media/files | Uploads are product-scoped; no global media browser exists. | Not applicable. |
| Settings/fonts/tools/seed/monitoring | Configuration, diagnostics, or bounded control surfaces. | Not applicable; no operational table paging. |

## Detail and export reconciliation in progress

- Payment refund/audit, wallet transaction, ticket-message, and product variant/media child lists are now
  paged in SQL. The parent header endpoints no longer materialize the respective histories.
- Product fulfilment items are part of a single fulfilment ticket and remain to be assessed against the
  order-item cap/UX; there is no assignment-history entity.
- Formula-injection protection in `AdminCsv` and the SMS CSV writer prefixes an apostrophe only for text
  cells beginning with `=`, `+`, `-`, `@`, tab, or carriage return. Numeric CSV cells remain numeric.
- Existing client-side exports are being explicitly classified. Current-page exports may use the already
  visible server page; selected-record exports require server-side ID validation before REQ-010 can close.

## Focused SQL test-host finding

The apparent hang was not test discovery or a second application host. `dotnet test --list-tests` completed
immediately; execution then spent roughly 50–75 seconds silently publishing the DACPAC and running
`Deploy-Database.ps1` for the isolated SQL Server database before xUnit emitted its first line. The stable
`SqlServerIntegrationCollection` fixture is retained and focused tests run through that single fixture.
No ad-hoc factory or retry loop was added.

`eaee518` also replaces the gift-code importer's unbounded product selector with a
dedicated ID/title/slug lookup, capped at 100 rows with selected-value hydration.
