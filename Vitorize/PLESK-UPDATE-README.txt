VITORIZE - PLESK PRODUCTION UPDATE (V0025 -> V0030)
===================================================

This is an UPDATE package for the existing production installation. It is not
a fresh-install package and it must not be used to create a database.

THESE SERVER FOLDERS ARE STATE. NEVER DELETE OR REPLACE THEM:

  API site: App_Data\DataProtection
            App_Data\PublicMedia
            App_Data\PrivateDocuments
            wwwroot\uploads
            logs

  Web site: App_Data\DataProtection
            logs

They contain uploaded product/media files, identity documents, encryption keys,
session protection keys and logs. Do not enable an FTP/deployment option such as
"delete extra files at destination".

SAFE DEPLOYMENT ORDER
---------------------

1. Put the site in maintenance mode or schedule a short maintenance window.
2. Take a tested SQL Server backup of the existing Vitorize database.
3. From the included Database folder, first run the read-only DryRun in
   UPGRADE-V0025-TO-V0030-README.md. Review its output.
4. Apply the V0026 .. V0030 upgrade with Deploy-Database.ps1. The included
   Tools\Upgrade-V0025-to-V0030-Standalone.sql is also ready for direct SSMS
   execution and has no SQLCMD dependency; it is only for a database whose
   ledger is exactly V0025.
5. Extract Vitorize.Api.Plesk.zip into the existing API site root and extract
   Vitorize.Web.Plesk.zip into the existing Web site root. The archive contents
   are already at their root; do not create an extra folder level.
6. Restart/recycle both application pools.
7. Smoke-test the home page, login, admin login, a product page and the order
   details page.

CONFIGURATION
-------------

The API and Web archives contain the same production appsettings files and
connection strings as the immediately preceding Plesk package. Do not replace
them with the placeholder files from the repository. The archives deliberately
do not include App_Data, uploads or logs.

PACKAGE VERIFICATION
--------------------

SHA256SUMS.txt lists the exact SHA-256 checksum of both archives and the SQL
upgrade script. Verify it after uploading the files; a mismatch means do not
deploy the affected file.
