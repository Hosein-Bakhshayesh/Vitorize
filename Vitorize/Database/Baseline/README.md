# Vitorize schema baseline

`VitorizeDb.schema-candidate.dacpac` is a schema-only extraction of the disposable
validation database after the canonical chain through V0005 and the Phase 2/3 SQL
verification suites passed on 2026-07-15. It contains no
table data, users, passwords, API keys, or other application secrets. The deployment
ledger table is present but empty in every database published from this DACPAC.

Verify the adjacent SHA-256 file before use. Publish with SqlPackage and
`BlockOnPossibleDataLoss=True`, then run `../Deploy-Database.ps1` to seed non-secret
reference data and record a fresh deployment history.

This remains explicitly a **candidate** pending DBA/release-owner approval. Review
`../MODEL-SCRIPT-MISMATCHES.md`, `../DEPLOYMENT-MANIFEST.md`, and the Phase 3 runbook
before Production deployment.
