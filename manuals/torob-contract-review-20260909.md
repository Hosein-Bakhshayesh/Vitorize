# بررسی قرارداد محصولات ترب — ۲۰۲۶-۰۹-۰۹

مرجع: https://panel.torob.com/s/torobApiV3 (متن کامل صفحه در مرورگر بررسی شد).

## یافته‌های سرور منتشرشده، پیش از اصلاح این مرحله

درخواست‌ها به `https://vitorize.com/api/v1/thirdparties/torob/products` با POST، بدنه JSON، Content-Type و Accept برابر application/json و User-Agent برابر torob.com ارسال شدند. مقدار X-Torob-Token آزمایشی و غیرمحرمانه بود؛ نسخهٔ هدر 1 بود.

| بدنه | وضعیت واقعی | نتیجه |
|---|---|---|
| `{"page":1,"sort":"date_added_desc"}` | 200 | فهرست ۱۰۰ محصول |
| `{"sort":"product_id_desc"}` | 400 | کد قبلی به‌اشتباه وجود یکی از page، page_urls یا page_uniques را اجباری می‌کرد |
| `{"page":1}` | 400 | نبودن sort؛ رد درخواست مطابق سند |
| `{` | 400 | قالب پیش‌فرض ProblemDetails به‌جای قالب error ترب |

رد درخواست cursor یک مغایرت قابل بازتولید با سند است. چون متن کامل درخواست ناموفق ربات در تیکت موجود نیست، علت دقیق همان درخواست پشتیبانی هنوز قابل اثبات نیست.

## اصلاحات

- حالت `product_id_desc` بدون page و با cursor اختیاری اضافه شد؛ پاسخ شامل next_cursor است. ادامه بر مبنای آخرین شناسه انجام می‌شود، نه شمارهٔ ردیف. حذف محصول مرزی باعث جاافتادن محصول بعدی نمی‌شود.
- شناسه‌های page_unique قبلی حفظ شدند؛ هر صفحه غیرنهایی ۱۰۰ محصول دارد و next_cursor در انتها null می‌شود.
- قالب خطای JSON نامعتبر یا نوع نادرست فیلدها فقط برای endpoint ترب به `{"error":"..."}` با HTTP 400 تبدیل شد.
- سقف سفارشی ۱۰۰ شناسه/لینک و سقف ۶۴ کیلوبایت پروکسی حذف شدند؛ سند برای درخواست مستقیم محصولات این سقف‌ها را تعیین نکرده است.
- طول category_name، short_desc و لینک تصاویر به‌ترتیب با سقف ۲۰۰، ۵۰۰ و ۱۰۰۰ کاراکتر سند هماهنگ شد.
- پروکسی، Content-Length موجود و هدرهای Accept و User-Agent را نیز منتقل می‌کند.
- تصمیم قبلی دربارهٔ احراز درخواست حفظ شده: فقط X-Torob-Token غیرخالی الزامی است؛ بررسی نسخه و امضای JWT فعال نشده است. بنابراین این مرحله ادعای پیاده‌سازی بخش امنیت JWT سند را ندارد.

## آزمون

فرمان اجراشده:

```powershell
dotnet test Vitorize/Vitorize.Tests/Vitorize.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Torob" --logger "trx;LogFileName=torob-contract.trx"
```

نتیجه: **۳۵ آزمون موفق، صفر ناموفق**. خروجی: `Vitorize/Vitorize.Tests/TestResults/torob-contract.trx`.

آزمون HTTP از دو وب‌سرور محلی واقعی استفاده می‌کند: مسیر عمومی وب ← پروکسی ← کنترلر و فیلترهای MVC ← سرویس کاتالوگ ← دیتابیس آزمایشی مستقل با ۲۰۵ محصول. خواندن دیتابیس تولید یا ایجاد سفارش و پیامک در این آزمون انجام نمی‌شود.

پوشش شامل دو ترتیب تاریخ، سه صفحهٔ متوالی، ادامهٔ cursor تا پایان بدون تکرار، حذف محصول مرزی، برابری نتیجهٔ جست‌وجوی لینک و شناسه، محصول حذف‌شده، درخواست مستقیم بزرگ‌تر از محدودیت قبلی، نوع و طول فیلدهای خروجی و ورودی‌های نامعتبر است.

## انتشار

اصلاحات بالا در ۲۰۲۶-۰۹-۰۹ منتشر شدند (حالت cursor و پیام «پارامتر ناشناخته» روی `vitorize.com` دیده شد). تغییر ساختار دیتابیس و کوئری SQL لازم نبود.

---

# بازبینی دوم — ۲۰۲۶-۰۹-۱۰ (پس از تیکت «ساختار بدنه یا نوع پارامترها مطابقت ندارد»)

## مغایرت‌هایی که روی سرور تولید اثبات شد

| مورد | رفتار قبلی | مشکل |
|---|---|---|
| `{"page":1,"sort":"date_added_desc","limit":100}` | 400 «پارامتر ناشناخته» | سند ترب در بخش cursor می‌گوید «در این حالت page، limit و size ارسال نمی‌شوند» ⇒ ربات در حالت صفحه‌ای `limit`/`size` می‌فرستد |
| `{"page":"1",…}`، `{"page":1.0,…}` | 400 «نوع صحیح پارامترها» | `JsonNumberHandling.Strict` |
| بدنه با Content-Type غیر از `application/json` (form، multipart، text/plain، بدون هدر) | **415 با بدنهٔ خالی** | `[Consumes]` قبل از فیلتر خطا اجرا می‌شد؛ سند هر خطا را 400 با `{"error"}` می‌خواهد |
| بدون `X-Torob-Token` | 401 | تا تأیید ترب فقط لاگ می‌شود |
| `{"page_urls":["https://vitorize.com/product/telegram-stars"]}` | 200 با `total:0` | canonical و JSON-LD صفحهٔ محصول بدون `?variant=` است؛ ربات همان را برمی‌گرداند |
| `current_price` | **ریال** (تومان×۱۰) | ترب تومانی است؛ API `360000000` می‌داد و صفحه `۳۶,۰۰۰,۰۰۰ تومان` |
| `date_added`/`date_updated` | ۷ رقم اعشار (فرمت "O") | سند حداکثر ۶ رقم (`[.ffffff]`) |
| `count` | نبود | پیوست `class Result` فیلد `count` دارد؛ مثال‌ها `total` |
| `old_price`, `short_desc`, `subtitle`, `category_name`, `guarantee` | حذف در حالت null / اصلاً نبود | پیوست همهٔ کلیدها را Optional (nullable) فهرست کرده |

## اصلاحات

- **دریافت آسان‌گیر** (`Vitorize.Api/Services/TorobRequestParser.cs`): بدنه صرف‌نظر از Content-Type خوانده می‌شود (JSON، form-urlencoded، multipart، query-string، text/plain، بدون هدر، با BOM)؛ `page` از عدد، عدد اعشاری صحیح، رشته و ارقام فارسی؛ کلیدهای ناشناخته (`limit`, `size`, …) نادیده و در لاگ ثبت می‌شوند؛ `sort` بدون حساسیت به بزرگی حروف؛ `page_url`/`page_unique` و `key[]` پذیرفته می‌شوند. ترتیب اولویت: `page_urls` ← `page_uniques` ← `product_id_desc`/cursor ← `page`+`sort`. 400 فقط برای موارد صریح سند: بدنهٔ خالی، `{"page":1}` بدون sort، sort نامعتبر، `page<1`، cursor نامعتبر، خالی، غیرمتنی (`{}`/آرایه) یا بدون `sort`، فهرست خالی. هیچ پارامتری پیش‌فرض نمی‌گیرد (الزام سند)؛ فقط `"cursor": null` به‌معنای «بدون cursor» (صفحهٔ اول) پذیرفته می‌شود. کنترلر دیگر `[ApiController]`/`[Consumes]`/`[FromBody]` ندارد و اکشن **بدون پارامتر** است (هر پارامتر باعث می‌شود MVC بدنهٔ فرم را قبل از پارسر مصرف کند).
- **پاسخ**: قیمت‌ها تومانی (`ToToman`: ریال÷۱۰)؛ تاریخ `yyyy-MM-ddTHH:mm:ss+00:00`؛ `count` کنار `total`؛ همهٔ ۱۵ کلید محصول همیشه حاضر (null صریح)؛ `guarantee: null`. جست‌وجوی `page_urls` با URL بدون variant همهٔ offerهای محصول را برمی‌گرداند؛ `www.`، `http://`، اسلش انتهایی، درصدکدینگ و پارامترهای ردیابی (به‌جز `variant`) نرمال می‌شوند.
- **توکن** (`TorobRequestAuthenticator.cs`): JWT در `X-Torob-Token` decode و امضای EdDSA آن با کلید عمومی رسمی ترب (`Torob:PublicKey`) بررسی می‌شود؛ `exp`/`nbf` با `Torob:ClockSkewSeconds` (۳۰۰) و `aud` با `Torob:ExpectedAudience` (`vitorize.com`) مقایسه می‌شود. نتیجه فقط لاگ می‌شود؛ رد درخواست تنها با `Torob:EnforceToken=true`. نسخهٔ توکن از `C-Torob-Token-Version` یا `X-Torob-Token-Version` (هر دو نام در مستندات ترب آمده).
- **لاگ تشخیصی**: هر درخواست یک خط `EventType=TorobProductsRequest` در لاگ API (`logs/vitorize-api-*.log`) با IP، Host، User-Agent، Content-Type، نام هدرها، وضعیت توکن/امضا/aud/exp، حالت تشخیص‌داده‌شده، کلیدهای نادیده‌گرفته‌شده، وضعیت پاسخ، متن خطا و ۴ کیلوبایت اول بدنه (قطعات ≤۱۰۰۰ کاراکتری به‌خاطر redaction). پیش‌نمایش بدنه قبل از ثبت با `SensitiveLogData.RedactFreeText` پاک‌سازی می‌شود و کلیدهای JSON حساس (`"password"`, `"token"`, `api_key`, …) اکنون در همان تابع سراسری redaction پوشش داده می‌شوند. پاک‌سازی روی پنجرهٔ ۶۴ کیلوبایتی انجام و بعد به ۴ کیلوبایت بریده می‌شود؛ اگر برش داخل یک رشتهٔ JSON بیفتد، آن مقدار ناقص کامل حذف می‌شود تا هیچ مقدار نیمه‌بریده‌ای ثبت نشود. Warning برای غیر-200 یا بی‌توکن. پروکسی Web هم `EventType=TorobProxyRequest` می‌نویسد و `X-Forwarded-For/Host/Proto` و `X-Correlation-ID` را به API می‌فرستد.

## آزمون

```powershell
dotnet test Vitorize/Vitorize.Tests/Vitorize.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~Torob"
```

نتیجه: **۹۶ آزمون Torob موفق، صفر ناموفق** (HTTP دو‌هاسته از پروکسی تا دیتابیس آزمایشی، پارسر، بازرس توکن با جفت‌کلید Ed25519 آزمایشی و کلید رسمی ترب، قالب لاگ در برابر redaction، سرویس کاتالوگ).

## انتشار و بازبینی

هر دو برنامهٔ **API و Web** باید منتشر شوند (پروکسی هدرهای forward اضافه می‌کند). تغییر دیتابیس ندارد. پس از انتشار، `outputs/torob-live-check-20260910/Invoke-TorobProbe.ps1` اجرا شود (نتیجه کنار اسکریپت ذخیره می‌شود) و سپس نتیجهٔ بازبینی ترب با خطوط `TorobProductsRequest` لاگ API تطبیق داده شود:

```powershell
Select-String -Path '<ApiRoot>\logs\vitorize-api-*.log' -Pattern 'TorobProductsRequest'
```

## راستی‌آزمایی پس از انتشار — ۲۰۲۶-۰۹-۱۲

هر دو برنامه منتشر شدند و `Invoke-TorobProbe.ps1` روی `https://vitorize.com/api/v1/thirdparties/torob/products` اجرا شد (گزارش: `outputs/torob-live-check-20260910/torob-probe-20260912-083047.txt`). نتیجه: **همهٔ ۱۸ ردیف OK**؛ ۱۳ شکل مجاز (page رشته‌ای/اعشاری، `limit`/`size`، form، multipart، text/plain، بدون Content-Type، BOM، بدون توکن، cursor صفحهٔ اول و دوم، URL خام محصول) ⇒ 200 و ۵ شکل غیرمجاز ⇒ 400 با `{"error"}`.

تطبیق با فروشگاه: `telegram-stars` در فروشگاه ۹ تنوع دارد و URL خام همان ۹ offer را با همان قیمت‌های تومانی (۱۹۸٬۷۸۲ تا ۵٬۹۶۳٬۴۶۳) و همان وضعیت موجودی برمی‌گرداند؛ تنوع «اکانت آماده آمریکا» شمارهٔ مجازی در فروشگاه «۲۰,۰۰۰ تومان ناموجود» و در فید `current_price: 20000, availability: false`. پاسخ شامل `count`، همهٔ ۱۵ کلید محصول با null صریح، `guarantee` و تاریخ با فرمت `2026-09-11T00:04:02+00:00` است.

باقی‌مانده: درخواست بازبینی مجدد از ترب و سپس خواندن خطوط `TorobProductsRequest` در لاگ API برای دیدن درخواست واقعی ربات.

تصمیم‌های ثبت‌شده: `{"page":1}` بدون `sort` طبق مستند اصلی 400 می‌ماند (نمونهٔ curl راهنمای توکن ترب همین بدنه را نشان می‌دهد؛ اگر در لاگ دیده شد، پیش‌فرض `date_added_desc` اضافه شود). اجباری‌کردن توکن فقط پس از دیدن `JwtSignatureValid=True` روی درخواست واقعی ترب.
