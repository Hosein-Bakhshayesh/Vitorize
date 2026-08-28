# ارتقای Production از V0025 به V0030

این ارتقا برای دیتابیس عملیاتی موجودی است که ledger آن دقیقاً تا `V0025` ثبت شده است.
مهاجرت‌ها فقط رو به جلو هستند؛ سفارش‌ها، کاربران، پرداخت‌ها و فایل‌های آپلودی حذف نمی‌شوند.
`V0026` صرفاً پیکربندی منسوخ احراز هویت در سطح کالا را حذف می‌کند تا احراز هویت بر مبلغ
نهایی سفارش اعمال شود.

## روش پیشنهادی (امن‌تر)

پس از تهیه و آزمون Backup، از پوشه `Database` این دستور را اجرا کنید. مقدار کانکشن را فقط
برای همان Process تنظیم کنید و آن را در history یا source نگه ندارید:

```powershell
$env:VITORIZE_DATABASE_DEPLOYMENT_CONNECTION = '<production-connection-string>'
powershell -NoProfile -ExecutionPolicy Bypass -File .\Deploy-Database.ps1 `
  -ServerInstance '<production-server>' -Database '<production-database>' `
  -Environment Production -DryRun

powershell -NoProfile -ExecutionPolicy Bypass -File .\Deploy-Database.ps1 `
  -ServerInstance '<production-server>' -Database '<production-database>' `
  -Environment Production -ConfirmDatabaseName '<production-database>'
Remove-Item Env:VITORIZE_DATABASE_DEPLOYMENT_CONNECTION
```

Runner ابتدا preflight فقط‌خواندنی انجام می‌دهد، checksum همه migrationها را بررسی می‌کند،
فقط V0026 تا V0030 را اعمال می‌کند و در پایان post-deploy verification را اجرا می‌کند.

## فایل SQL مستقل آماده برای SSMS

اگر قرار است کوئری را مستقیماً در SSMS اجرا کنید، فایل
`Tools\\Upgrade-V0025-to-V0030-Standalone.sql` را باز کنید، دیتابیس صحیح را از dropdown انتخاب کنید و
بدون فعال‌کردن SQLCMD Mode، Execute بزنید.

این فایل در شروع وجود دقیق V0025 را کنترل می‌کند، از اجرای دوباره جلوگیری می‌کند و همهٔ
تغییرات و ثبت نسخه‌ها را در یک تراکنش انجام می‌دهد؛ در نتیجه با هر خطا تمام تغییرات rollback
می‌شوند. برای ادامه پس از هر اجرای ناقص، فقط از `Deploy-Database.ps1` استفاده کنید.
