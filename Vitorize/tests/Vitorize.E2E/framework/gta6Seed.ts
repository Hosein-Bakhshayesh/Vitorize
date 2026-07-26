import { expect, type APIRequestContext } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { apiBaseUrl, adminMobile, adminPassword } from '../tests/support/app';

// Idempotent, production-workflow seed for the GTA VI SupportRequired test product.
// Everything is created/reconciled through the real Admin API (auth + uploads +
// product/variant/image/category/brand/tag endpoints). The slug is the natural
// key, so re-running reconciles in place and never duplicates rows or images.

export const GTA6_SLUG = 'gta-vi-legal-account-playstation-5';
const CATEGORY_SLUG = 'playstation-games-accounts';
const BRAND_SLUG = 'rockstar-games';

const mediaBase = new URL(apiBaseUrl).origin; // e.g. http://127.0.0.1:5177
const mediaDir = path.resolve(__dirname, '../fixtures/media');

const CurrencyTypeToman = 2;
const ProductTypeGameAccount = 2; // enum ProductType: game account
const DeliveryTypeSupportRequired = 3;

export const GTA6 = {
  slug: GTA6_SLUG,
  title: 'پیش‌خرید اکانت قانونی بازی GTA VI برای PlayStation 5',
  englishTitle: 'GTA VI Legal Account Pre-Order for PlayStation 5',
  shortDescription:
    'پیش‌خرید اکانت قانونی بازی GTA VI برای PlayStation 5 با تحویل از طریق پشتیبانی. پس از ثبت سفارش، تیکت اختصاصی به‌صورت خودکار ایجاد می‌شود و اطلاعات اکانت و راهنمای فعال‌سازی از همان تیکت در اختیار خریدار قرار می‌گیرد.',
  seoTitle: 'خرید اکانت قانونی GTA VI برای PS5 | نسخه Standard و Ultimate',
  seoDescription:
    'پیش‌خرید اکانت قانونی GTA VI برای PlayStation 5 در دو نسخه Standard و Ultimate با تحویل امن از طریق تیکت پشتیبانی Vitorize.',
  focusKeyword: 'خرید اکانت GTA VI برای PS5',
  category: { slug: CATEGORY_SLUG, title: 'بازی و اکانت پلی‌استیشن' },
  brand: { slug: BRAND_SLUG, title: 'Rockstar Games' },
  coverAlt: 'کاور بازی GTA VI برای پلی‌استیشن 5',
  posterAlt: 'پوستر عرضه بازی GTA VI',
  wideAlt: 'تصویر تبلیغاتی Jason و Lucia در GTA VI',
  standardTitle: 'نسخه استاندارد',
  ultimateTitle: 'نسخه آلتیمیت',
  standardSku: 'GTA6-PS5-STANDARD',
  ultimateSku: 'GTA6-PS5-ULTIMATE',
  standardPrice: 10_000_000,
  ultimatePrice: 12_000_000,
  tags: ['GTA VI', 'Grand Theft Auto VI', 'PlayStation 5', 'Rockstar Games', 'اکانت قانونی', 'پیش‌خرید'],
  features: [
    { title: 'پلتفرم', value: 'PlayStation 5', icon: 'gamepad-2' },
    { title: 'نوع محصول', value: 'اکانت قانونی', icon: 'shield-check' },
    { title: 'ناشر', value: 'Rockstar Games', icon: 'building-2' },
    { title: 'شیوه تحویل', value: 'از طریق پشتیبانی', icon: 'headphones' },
    { title: 'وضعیت', value: 'پیش‌خرید', icon: 'clock' },
    { title: 'نسخه‌ها', value: 'Standard و Ultimate', icon: 'layers' },
    { title: 'فعال‌سازی', value: 'راهنمای کامل در تیکت سفارش', icon: 'ticket' }
  ]
};

export interface Gta6Seeded {
  productId: string;
  categoryId: string;
  brandId: string;
  standardVariantId: string;
  ultimateVariantId: string;
  coverUrl: string;   // absolute media URL of the primary cover
  posterUrl: string;  // absolute media URL of the portrait poster
  thumbnailPath: string; // relative /uploads/... path
}

async function adminToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${apiBaseUrl}/auth/login`, { data: { mobile: adminMobile, password: adminPassword } });
  expect(res.ok(), `admin login ${res.status()}`).toBeTruthy();
  const body = await res.json();
  const token = body?.data?.accessToken as string | undefined;
  expect(token, 'admin accessToken').toBeTruthy();
  return token!;
}

function authHeaders(token: string) { return { Authorization: `Bearer ${token}` }; }

async function getData<T>(request: APIRequestContext, token: string, url: string): Promise<T> {
  const res = await request.get(url, { headers: authHeaders(token) });
  expect(res.ok(), `GET ${url} -> ${res.status()}`).toBeTruthy();
  return (await res.json()).data as T;
}

async function sendData<T>(request: APIRequestContext, token: string, method: 'post' | 'put', url: string, data: unknown): Promise<T> {
  const res = await request[method](url, { headers: authHeaders(token), data });
  expect(res.ok(), `${method.toUpperCase()} ${url} -> ${res.status()} ${await res.text()}`).toBeTruthy();
  return (await res.json()).data as T;
}

async function uploadImage(request: APIRequestContext, token: string, file: string): Promise<string> {
  const buffer = fs.readFileSync(path.join(mediaDir, file));
  const res = await request.post(`${apiBaseUrl}/admin/uploads/product-image`, {
    headers: authHeaders(token),
    multipart: { file: { name: file, mimeType: 'image/jpeg', buffer } }
  });
  expect(res.ok(), `upload ${file} -> ${res.status()} ${await res.text()}`).toBeTruthy();
  return (await res.json()).data.filePath as string; // relative /uploads/products/xxx
}

function abs(relative: string): string {
  return relative.startsWith('http') ? relative : `${mediaBase}${relative}`;
}

function buildDescriptionHtml(coverUrl: string, posterUrl: string): string {
  const fig = (url: string, alt: string) =>
    `<figure class="image image-style-align-center"><img src="${url}" alt="${alt}"></figure>`;
  return [
    '<p dir="rtl"><strong>ارسال و آماده‌سازی اکانت قانونی پس از ثبت سفارش از طریق تیکت انجام می‌شود.</strong></p>',
    '<blockquote dir="rtl"><p>این محصول از نوع تحویل با پشتیبانی است. پس از تکمیل پرداخت، یک تیکت اختصاصی برای سفارش ایجاد می‌شود و مشخصات اکانت، وضعیت آماده‌سازی و راهنمای فعال‌سازی از طریق همان تیکت ارسال خواهد شد.</p></blockquote>',

    '<h2 dir="rtl">معرفی بازی GTA VI</h2>',
    '<p dir="rtl">در GTA VI وارد دنیایی مدرن، گسترده و پویا می‌شوید. داستان بازی در ایالت خیالی Leonida و شهر Vice City جریان دارد و روی ماجراهای شخصیت‌های اصلی، Jason و Lucia، تمرکز می‌کند. محیط شهری بزرگ، مأموریت‌های متنوع، وسایل نقلیه گوناگون و جزئیات بصری نسل جدید از مهم‌ترین ویژگی‌های این عنوان هستند.</p>',
    fig(posterUrl, GTA6.posterAlt),

    '<h2 dir="rtl">داستان و شخصیت‌های اصلی</h2>',
    '<p dir="rtl">روایت بازی حول همکاری و تصمیم‌های Jason و Lucia شکل می‌گیرد. آن‌ها در مسیر ماجراجویی خود با موقعیت‌های پرخطر، تعقیب‌وگریزها و انتخاب‌هایی روبه‌رو می‌شوند که روند داستان را تحت تأثیر قرار می‌دهد.</p>',
    '<ul dir="rtl"><li>روایت شخصیت‌محور و سینمایی</li><li>محیط باز و گسترده</li><li>مأموریت‌ها و فعالیت‌های متنوع</li><li>فضای مدرن Vice City و Leonida</li><li>بهینه‌سازی‌شده برای PlayStation 5</li></ul>',

    '<h2 dir="rtl">گرافیک، گیم‌پلی و تجربه نسل جدید</h2>',
    '<p dir="rtl">نسخه PlayStation 5 با تمرکز بر کیفیت تصویر، جزئیات محیطی، نورپردازی پیشرفته و بارگذاری سریع طراحی شده است. تجربه نهایی بازی ممکن است بسته به نسخه منتشرشده، به‌روزرسانی‌های رسمی و تنظیمات کنسول متفاوت باشد.</p>',
    '<ul dir="rtl"><li>محیط شهری زنده و پرجزئیات</li><li>رانندگی و وسایل نقلیه متنوع</li><li>مأموریت‌های داستانی و فعالیت‌های جانبی</li><li>پشتیبانی از قابلیت‌های نسل جدید کنسول</li><li>دریافت به‌روزرسانی‌ها از مسیر رسمی حساب</li></ul>',
    fig(coverUrl, GTA6.wideAlt),

    '<h2 dir="rtl">مقایسه نسخه Standard و Ultimate</h2>',
    '<figure class="table"><table><thead><tr><th>امکانات و محتوا</th><th>نسخه Standard</th><th>نسخه Ultimate</th></tr></thead><tbody>' +
      '<tr><td>بازی اصلی GTA VI</td><td>دارد</td><td>دارد</td></tr>' +
      '<tr><td>دسترسی به محتوای پایه</td><td>دارد</td><td>دارد</td></tr>' +
      '<tr><td>محتوای اضافه نسخه Ultimate</td><td>ندارد</td><td>دارد</td></tr>' +
      '<tr><td>آیتم‌ها یا Bonusهای اختصاصی اعلام‌شده برای نسخه Ultimate</td><td>ندارد</td><td>دارد</td></tr>' +
      '<tr><td>قیمت</td><td>10,000,000 تومان</td><td>12,000,000 تومان</td></tr>' +
      '<tr><td>شیوه تحویل</td><td>تحویل از طریق تیکت پشتیبانی</td><td>تحویل از طریق تیکت پشتیبانی</td></tr>' +
      '</tbody></table></figure>',
    '<p dir="rtl"><em>جزئیات دقیق محتوای هر نسخه بر اساس اطلاعات نهایی ناشر و موجودی تأمین‌کننده در زمان تحویل در تیکت سفارش اعلام می‌شود. تفاوت‌های نسخه‌ها در این صفحه به‌عنوان داده‌ی نمونه/آزمایشی کاتالوگ ثبت شده است.</em></p>',
    '<hr>',

    '<h2 dir="rtl">روش خرید، تحویل و فعال‌سازی</h2>',
    '<ol dir="rtl"><li>نسخه Standard یا Ultimate را انتخاب کنید.</li><li>محصول را به سبد خرید اضافه کرده و پرداخت را تکمیل کنید.</li><li>پس از ثبت موفق سفارش، تیکت اختصاصی به‌طور خودکار ایجاد می‌شود.</li><li>تیم پشتیبانی اطلاعات اکانت را در همان تیکت ارسال می‌کند.</li><li>کاربر مطابق راهنما، اکانت را روی PlayStation 5 اضافه و بازی را فعال می‌کند.</li><li>در صورت وجود سؤال یا مشکل، ادامه مکالمه از طریق همان تیکت انجام می‌شود.</li></ol>',
    '<blockquote dir="rtl"><p>رمز عبور یا اطلاعات حساس اکانت فقط در پیام خصوصی تیکت برای خریدار و تیم مجاز پشتیبانی قابل مشاهده است و در بخش عمومی سفارش یا اعلان‌ها ثبت نمی‌شود.</p></blockquote>',

    '<h2 dir="rtl">سؤالات متداول</h2>',
    '<h3 dir="rtl">آیا تحویل این محصول فوری است؟</h3>',
    '<p dir="rtl">خیر. این محصول نیازمند بررسی و آماده‌سازی توسط پشتیبانی است و روند تحویل از طریق تیکت سفارش انجام می‌شود.</p>',
    '<h3 dir="rtl">تفاوت نسخه Standard و Ultimate چیست؟</h3>',
    '<p dir="rtl">نسخه Standard شامل محصول پایه است و نسخه Ultimate شامل محتوای اضافه تعریف‌شده برای این نسخه خواهد بود. جزئیات نهایی هنگام تحویل اعلام می‌شود.</p>',
    '<h3 dir="rtl">مشخصات اکانت از چه طریقی ارسال می‌شود؟</h3>',
    '<p dir="rtl">پس از ثبت سفارش، تیکت اختصاصی ایجاد می‌شود و مشخصات اکانت و راهنمای استفاده فقط از طریق همان تیکت ارسال می‌شود.</p>',
    '<h3 dir="rtl">آیا می‌توانم وضعیت تحویل را پیگیری کنم؟</h3>',
    '<p dir="rtl">بله. تمام وضعیت‌ها و پاسخ‌های پشتیبانی در تیکت متصل به سفارش قابل مشاهده است.</p>'
  ].join('');
}

async function reconcileCategory(request: APIRequestContext, token: string): Promise<string> {
  const list = await getData<any[]>(request, token, `${apiBaseUrl}/admin/categories`);
  const found = list.find(c => c.slug === CATEGORY_SLUG);
  if (found) return found.id;
  const created = await sendData<any>(request, token, 'post', `${apiBaseUrl}/admin/categories`, {
    title: GTA6.category.title, slug: CATEGORY_SLUG, isActive: true, sortOrder: 10
  });
  return created.id;
}

async function reconcileBrand(request: APIRequestContext, token: string): Promise<string> {
  const list = await getData<any[]>(request, token, `${apiBaseUrl}/admin/brands`);
  const found = list.find(b => b.slug === BRAND_SLUG);
  if (found) return found.id;
  const created = await sendData<any>(request, token, 'post', `${apiBaseUrl}/admin/brands`, {
    title: GTA6.brand.title, slug: BRAND_SLUG, isActive: true
  });
  return created.id;
}

async function reconcileTags(request: APIRequestContext, token: string): Promise<string[]> {
  const list = await getData<any[]>(request, token, `${apiBaseUrl}/admin/product-tags`);
  const ids: string[] = [];
  for (let i = 0; i < GTA6.tags.length; i++) {
    const title = GTA6.tags[i];
    let found = list.find(t => t.title === title);
    if (!found) found = await sendData<any>(request, token, 'post', `${apiBaseUrl}/admin/product-tags`,
      { title, slug: `gta6-tag-${i + 1}`, isActive: true });
    ids.push(found.id);
  }
  return ids;
}

/** Creates or reconciles the whole product idempotently and returns its identifiers. */
export async function seedGta6Product(request: APIRequestContext): Promise<Gta6Seeded> {
  const token = await adminToken(request);
  const categoryId = await reconcileCategory(request, token);
  const brandId = await reconcileBrand(request, token);
  const tagIds = await reconcileTags(request, token);

  const products = await getData<any[]>(request, token, `${apiBaseUrl}/admin/products`);
  const existing = products.find(p => p.slug === GTA6_SLUG);

  // Reuse already-uploaded assets on re-run so images are never duplicated.
  let thumbnailPath: string;
  let posterPath: string;
  if (existing?.thumbnailImagePath) {
    thumbnailPath = existing.thumbnailImagePath;
    const gallery = await getData<any[]>(request, token, `${apiBaseUrl}/admin/products/${existing.id}/images`);
    posterPath = gallery.find(g => g.imagePath !== thumbnailPath)?.imagePath
      ?? await uploadImage(request, token, 'gta6-poster.jpg');
  } else {
    thumbnailPath = await uploadImage(request, token, 'gta6-cover.jpg');
    posterPath = await uploadImage(request, token, 'gta6-poster.jpg');
  }
  const coverUrl = abs(thumbnailPath);
  const posterUrl = abs(posterPath);

  const payload = {
    categoryId, brandId,
    title: GTA6.title, slug: GTA6_SLUG,
    shortDescription: GTA6.shortDescription,
    fullDescription: buildDescriptionHtml(coverUrl, posterUrl),
    productType: ProductTypeGameAccount,
    deliveryType: DeliveryTypeSupportRequired,
    basePrice: GTA6.standardPrice,
    currencyType: CurrencyTypeToman,
    requiresVerification: false,
    requiresSupportMessage: true, // opt in to automatic support-ticket creation
    minOrderQuantity: 1,
    isFeatured: true,
    isActive: true,
    seoTitle: GTA6.seoTitle, seoDescription: GTA6.seoDescription, focusKeyword: GTA6.focusKeyword,
    thumbnailImagePath: thumbnailPath, thumbnailAltText: GTA6.coverAlt,
    tagIds,
    sortOrder: 5,
    features: GTA6.features.map((f, i) => ({ title: f.title, value: f.value, iconKey: f.icon, sortOrder: (i + 1) * 10, isActive: true })),
    inputFields: [] as unknown[]
  };

  const product = existing
    ? await sendData<any>(request, token, 'put', `${apiBaseUrl}/admin/products/${existing.id}`, payload)
    : await sendData<any>(request, token, 'post', `${apiBaseUrl}/admin/products`, payload);
  const productId = product.id;

  // Gallery: portrait poster + wide cover, reconciled by image path.
  const gallery = await getData<any[]>(request, token, `${apiBaseUrl}/admin/products/${productId}/images`);
  const ensureGallery = async (imagePath: string, altText: string, sortOrder: number) => {
    if (gallery.some(g => g.imagePath === imagePath)) return;
    await sendData<any>(request, token, 'post', `${apiBaseUrl}/admin/products/${productId}/images`,
      { imagePath, altText, sortOrder, setAsThumbnail: false });
  };
  await ensureGallery(posterPath, GTA6.posterAlt, 1);
  await ensureGallery(thumbnailPath, GTA6.wideAlt, 2);

  // Two variants reconciled by SKU.
  const variants = await getData<any[]>(request, token, `${apiBaseUrl}/admin/products/${productId}/variants`);
  const upsertVariant = async (title: string, sku: string, price: number, isDefault: boolean, sortOrder: number): Promise<string> => {
    const body = { title, sku, price, value: sku, stockMode: 3, isDefault, isActive: true, sortOrder };
    const found = variants.find(v => v.sku === sku);
    if (found) { await sendData<any>(request, token, 'put', `${apiBaseUrl}/admin/product-variants/${found.id}`, body); return found.id; }
    const created = await sendData<any>(request, token, 'post', `${apiBaseUrl}/admin/products/${productId}/variants`, body);
    return created.id;
  };
  const standardVariantId = await upsertVariant(GTA6.standardTitle, GTA6.standardSku, GTA6.standardPrice, true, 1);
  const ultimateVariantId = await upsertVariant(GTA6.ultimateTitle, GTA6.ultimateSku, GTA6.ultimatePrice, false, 2);

  return { productId, categoryId, brandId, standardVariantId, ultimateVariantId, coverUrl, posterUrl, thumbnailPath };
}
