# پوشش راهنمای مدیریت Vitorize

مبنای بازبینی: `master` در `21e375e20ab38072d80f6a7e47449065a2a42fd4`، کد و UI جاری. راهنمای PDF در فصل‌های ۱ تا ۱۵ و تصاویر واقعی موجود در `manuals/screenshots/` نگهداری می‌شود. جدول زیر تمام routeهای Admin و قابلیت‌های عمیق/عملیاتی وابسته را به بخش مستند متصل می‌کند.

| Feature | Route | Current | Manual Section | Screenshot | Explanation Level | Final Status |
|---|---|---:|---|---|---|---|
| داشبورد و شاخص‌ها | `/admin`, `/admin/dashboard` | Yes | ۱ | `adm-dashboard.png` | عملیاتی کامل | DOCUMENTED |
| محصولات: فهرست، جستجو، صفحه‌بندی و خروجی | `/admin/products` | Yes | ۲-۱ | `adm-products.png` | عملیاتی کامل | DOCUMENTED |
| ایجاد/ویرایش محصول و CKEditor | `/admin/products/create`, `/admin/products/{id}` | Yes | ۲-۲، ۱۵-۴ | `adm-product-create.png`, `adm-product-edit.png` | عملیاتی کامل | DOCUMENTED |
| اطلاعات موردنیاز خریدار / فرم پویا | `/admin/products/{id}` | Yes | ۱۵-۱ تا ۱۵-۳ | `adm-product-edit.png` | کامل، امنیتی و گردش‌کار | DOCUMENTED |
| جزئیات محصول | `/admin/products/{id}/details` | Yes | ۲-۴ | `adm-product-details.png` | عملیاتی کامل | DOCUMENTED |
| گالری و تصویر شاخص | `/admin/products/{id}/images` | Yes | ۲-۵ | `adm-product-images.png` | کامل | DOCUMENTED |
| واریانت، ویژگی و انتخابگر آیکن | محصول / dialog واریانت | Yes | ۲-۶، ۱۵-۴ | `adm-product-edit.png` | کامل | DOCUMENTED |
| دسته‌بندی، برند و برچسب | `/admin/categories`, `/admin/brands`, `/admin/product-tags` | Yes | ۲-۷ | `adm-categories.png`, `adm-brands.png`, `adm-product-tags.png` | کامل | DOCUMENTED |
| بنرها و محتوا | `/admin/banners` | Yes | ۱۲ | `adm-banners.png` | کامل | DOCUMENTED |
| کدهای گیفت، batch و import | `/admin/gift-codes` | Yes | ۳ | `adm-gift-codes.png`, `adm-giftcode-import.png` | کامل | DOCUMENTED |
| سفارش‌ها، پرداخت، تحویل و ورودی‌های سفارش | `/admin/orders` | Yes | ۴، ۱۵-۳ | `adm-orders.png`, `adm-order-detail.png` | کامل | DOCUMENTED |
| پرداخت، راستی‌آزمایی و بازپرداخت | `/admin/payments` | Yes | ۵ | `adm-payments.png` | کامل | DOCUMENTED |
| کوپن | `/admin/coupons` | Yes | ۶ | `adm-coupons.png`, `adm-coupon-create.png` | کامل | DOCUMENTED |
| کاربران و جزئیات کاربر | `/admin/users` | Yes | ۷-۱ | `adm-users.png`, `adm-user-detail.png` | کامل | DOCUMENTED |
| نقش‌ها و مجوزها | `/admin/roles` | Yes | ۷-۲ | `adm-roles.png` | کامل | DOCUMENTED |
| احراز هویت | `/admin/verifications` | Yes | ۷-۳ | `adm-verifications.png`, `adm-verification-detail.png` | کامل | DOCUMENTED |
| کیف پول و تراکنش‌ها | `/admin/wallets` | Yes | ۷-۴ | `adm-wallets.png` | کامل | DOCUMENTED |
| تیکت‌ها و SupportRequired | `/admin/tickets` | Yes | ۸، ۱۵-۳ | `adm-tickets.png` | کامل، با خلاصهٔ امن ورودی | DOCUMENTED |
| بررسی نظر | `/admin/reviews` | Yes | ۹ | `adm-reviews.png` | کامل | DOCUMENTED |
| گزارش‌ها و خروجی‌ها | `/admin/reports` | Yes | ۱۰ | `adm-reports.png` | کامل | DOCUMENTED |
| لاگ ممیزی | `/admin/audit-logs` | Yes | ۱۱-۱ | `adm-audit-logs.png` | کامل | DOCUMENTED |
| لاگ امنیت و خطا | `/admin/security-logs`, `/admin/error-logs` | Yes | ۱۱-۲ و ۱۱-۳ | `adm-security-logs.png`, `adm-error-logs.png` | کامل | DOCUMENTED |
| پایش و worker/health | `/admin/monitoring` | Yes | ۱۱ | `adm-monitoring.png` | کامل | DOCUMENTED |
| اعلان‌های داخلی | `/admin/notifications` | Yes | ۷ و ۱۲ | `adm-notifications.png` | کامل | DOCUMENTED |
| مدیریت SMS | `/admin/sms` | Yes | ۱۲ | `adm-sms.png` | کامل | DOCUMENTED |
| تنظیمات و همهٔ ۱۷۹ کلید | `/admin/settings` | Yes | ۱۲، ۱۵-۵ | `adm-settings-01.png` تا `adm-settings-17.png` | کامل | DOCUMENTED |
| فونت فروشگاه: Peyda / Funnel Display | `/admin/settings` (Typography) | Yes | ۱۵-۵ | `adm-settings-01.png` | کامل | DOCUMENTED |
| ابزارهای عملیاتی | `/admin/tools` | Yes | ۱۲-ابزارها | `adm-tools.png` | کامل | DOCUMENTED |
| رفع اشکال و گردش‌کارهای سراسری | میان‌برهای صفحه‌ها | Yes | ۱۳، ۱۴، ۱۵-۶ | تصاویر مرتبط | کامل | DOCUMENTED |

نتیجهٔ ممیزی: همهٔ قابلیت‌های Admin جاریِ فهرست‌شده در route و جریان‌های عمیق، به بخش عملیاتی راهنما نگاشت شده‌اند.
