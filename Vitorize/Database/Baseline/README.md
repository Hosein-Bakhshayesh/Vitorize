# Vitorize schema baseline

`VitorizeDb.schema-candidate.dacpac` is a schema-only extraction of the disposable
validation database after the canonical chain was applied on 2026-07-14. It contains no
table data, users, passwords, API keys, or other application secrets. The deployment
ledger table is present but empty in every database published from this DACPAC.

Verify the adjacent SHA-256 file before use. Publish with SqlPackage and
`BlockOnPossibleDataLoss=True`, then run `../Deploy-Database.ps1` to seed non-secret
reference data and record a fresh deployment history.

This is explicitly a **candidate** because model/script differences remain assigned to
Phase 1 Task 3. Review `../MODEL-SCRIPT-MISMATCHES.md` and
`../DEPLOYMENT-MANIFEST.md` before Production deployment.
