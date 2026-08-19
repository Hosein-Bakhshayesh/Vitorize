# Database model/script mismatches deferred to Phase 1 Task 3

This audit compares `VitorizeDbContext`, the current local reference schema, and the
historical deployment scripts. No application entity or DbContext mapping was changed
in Task 2. The database remains the source of truth.

| Area | Current observation | Runtime impact | Task 3 decision required |
|---|---|---|---|
| Input-value indexes | `VitorizeDbContext` declares `IX_CartItemInputValues_CartItemId` and `IX_OrderItemInputValues_OrderItemId`; the reference database and product script only have unique composite indexes beginning with those columns. | Queries remain index-supported through the composite indexes; metadata is not an exact match. | Decide whether to remove redundant model declarations or add explicit database indexes. |
| SMS sort direction | The SMS history script creates `CreatedAt DESC` index keys. The DbContext declares the same key columns without descending metadata. | No known functional failure; generated model metadata differs from the database. | Scaffold/normalize index direction deliberately. |
| Database CHECK constraints | The reference schema contains business CHECK constraints for SMS, product inputs, sensitive storage, orders, payments, reviews, wallet, gift reservations, and other tables. Most are not represented in fluent configuration. | SQL Server enforces the rules correctly; model validation does not describe every database rule. | Decide which constraints should be documented in the model after re-scaffolding. |
| OTP purpose | The reference database and DbContext have no restrictive `OtpCodes.Purpose` CHECK constraint. A historical script only repairs environments that still have a legacy constraint excluding purpose `4`. | Current reference schema supports login OTP. | Keep the corrective script environment-specific; do not introduce a new core constraint without defining the complete enum range. |
| Deployment ledger | `dbo.DatabaseScriptHistory` and its immutability trigger are operational deployment objects and are intentionally absent from the application DbContext. | None; the application never reads or writes the ledger. | Preserve as an operational exclusion when re-scaffolding. |
| Partial historical schema | The SMS and product scripts create missing tables, but do not normalize every possible partially-created table/column/index shape. | Unknown partial deployments may require manual reconciliation. | Use preflight/baseline comparison to produce targeted corrective scripts, rather than altering historical files. |
| Duplicate reference seeding | Startup seeding and SQL scripts both seed roles/settings/font defaults idempotently. Startup seeding is not recorded in the SQL ledger. | Values are preserved, but a newly-added runtime default can temporarily lead the SQL manifest. | Choose one authoritative release process and add a drift test between seeder keys and SQL seeds. |

The baseline candidate is a schema-only extraction of the validated local reference
database after the canonical chain was applied. It captures these current facts; it does
not claim that the mismatches above are resolved.

For the SMS-history and product-experience entities introduced by the audited scripts,
the compared column types, Unicode lengths, nullability, decimal `18,2` precision,
defaults, foreign keys and delete behaviors match the current reference schema. No enum
column or obsolete column mismatch was found beyond the CHECK-constraint metadata and
index differences listed above.
