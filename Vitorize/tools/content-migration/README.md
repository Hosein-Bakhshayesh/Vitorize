# Content migration toolkit

The toolkit is review-first and never changes production automatically.

1. Run `Invoke-LegacyContentInventory.ps1` against the legacy public host and review the generated inventory.
2. Fill the templates with stable `ExternalKey` values. Validate duplicate keys, slugs, destination paths, HTML, and media ownership before import.
3. Sanitize all imported HTML through the existing `IHtmlContentSanitizer`; never copy scripts, event handlers, embedded credentials, or third-party passwords.
4. Download media into quarantine, verify MIME/signatures and dimensions, then upload through the canonical media service. Preserve descriptive alt text and source attribution separately.
5. Import products/categories/brands/content in a rehearsal database first. Reviews require ownership/date/rating validation and must remain unapproved until reviewed. FAQ and legal pages require editorial approval.
6. Convert the reviewed redirect mapping with `Database/Tools/Import-LegacyRedirects.ps1`. Its default behavior is validation plus SQL generation; database application requires an explicit `-Apply` and exact database-name confirmation.
7. Re-run the import with the same external keys to prove idempotency. Rollback is performed from the pre-import backup or by a reviewed external-key scoped deletion script; never use broad deletes.
