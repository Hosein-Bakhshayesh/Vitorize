# Vitorize browser E2E

This project is isolated from production credentials and data. It targets a disposable SQL Server database, uses the Development-only mock payment paths and the seeded Mock SMS provider, and captures screenshots and traces on failure. Set `E2E_VIDEO=true` to retain failure videos on agents where Playwright FFmpeg is installed.

```powershell
# After publishing the baseline and running Database/Deploy-Database.ps1:
powershell -File tests/Vitorize.E2E/scripts/Prepare-E2EDatabase.ps1
cd tests/Vitorize.E2E
npm ci
npx playwright install chromium
$env:E2E_MANAGE_STACK='true'
npm test
```

Set `E2E_SQL_CONNECTION` to point the managed stack at a different disposable database. Never point this harness at staging or production. The deterministic fixture contains only public catalog/SEO data and may be applied repeatedly.

The current suite covers server-rendered SEO HTML, canonical/robots/sitemaps, exact redirects, 404/noindex behavior, security headers, public asset partitioning, desktop/mobile, light/dark, and axe WCAG checks. Authenticated commerce/admin scenarios require a separately approved ephemeral-user fixture and are intentionally not represented as production-verified by this harness.
