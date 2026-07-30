# Rollback

Do not drop a live Vitorize database. Before application use, correct a failed deployment by dropping only the newly created empty database after DBA approval. After any business data is written, stop API/Web traffic, restore the tested pre-deployment backup to a separate database, validate it, then perform a controlled cutover. The required manifest chain is forward-only; use backup/restore rather than destructive reverse SQL. Preserve uploaded media and Data Protection keys alongside the database backup.
