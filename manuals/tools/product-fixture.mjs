// Test-only public API responses. No real account, order or database is used.
export const productWrites = [];
export const productGuid = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0');
export function serveProductFixture(req, res, url, mode, related) {
    if (!mode.startsWith('product')) return false;
    const send = (data, status = 200) => {
        res.writeHead(status, { 'content-type': 'application/json' });
        res.end(JSON.stringify({ isSuccess: status === 200, data, message: status === 200 ? '' : 'خطای آزمایشی سبد خرید' }));
    };
    if (url.pathname.startsWith('/api/products/slug/')) {
        const slug = url.pathname.split('/').at(-1);
        if (slug === 'missing') { send(null, 404); return true; }
        const variants = Array.from({ length: slug === 'many' ? 12 : slug === 'implicit' ? 1 : slug === 'single' ? 0 : 3 }, (_, i) => ({
            id: productGuid(501 + i), title: slug === 'implicit' ? 'پیش‌فرض' : `نسخه ${i + 1} اشتراک دیجیتال`,
            price: (i + 1) * 100000, discountPrice: i === 1 ? 180000 : null,
            availableStock: i === 0 && slug !== 'implicit' ? 0 : 10,
            isUnlimitedStock: i === 2, isDefault: i === 0, sortOrder: i,
        }));
        send({
            id: productGuid(500), slug, title: 'اشتراک پریمیوم دیجیتال', categoryTitle: 'محصولات پریمیوم',
            categorySlug: 'category-0', brandTitle: 'Vitorize', basePrice: 100000, currencyType: slug === 'rial' ? 1 : 2,
            deliveryType: 2, availableStock: 10, isUnlimitedStock: true, minOrderQuantity: 2, maxOrderQuantity: 4,
            forceOutOfStock: slug === 'out', variants,
            redirectUrl: slug === 'redirect' ? '/shop' : null,
            thumbnailImagePath: '/media/test.svg?gallery=0', thumbnailAltText: 'تصویر اصلی محصول',
            imageItems: [{ imagePath: '/media/test.svg?gallery=0', sortOrder: 0 }, { imagePath: '/media/test.svg?gallery=1', altText: 'تصویر دوم محصول', sortOrder: 1 }],
            shortDescription: 'توضیحات آزمایشی محصول برای بررسی ظاهر صفحه.',
            fullDescription: '<h2>درباره محصول</h2><p>این متن فقط در محیط آزمایشی مرورگر نمایش داده می‌شود. اطلاعات، قیمت و نسخه‌های محصول از API دریافت می‌شوند.</p>',
            features: [{ title: 'نوع اشتراک', value: 'پریمیوم', iconKey: 'circle-check', isActive: true }, { title: 'پلتفرم', value: 'دسکتاپ و موبایل', isActive: true }, { title: 'FEATURE_MUST_STAY_HIDDEN', value: 'hidden', isActive: false }],
            faqs: [{ id: productGuid(600), question: 'چگونه از محصول استفاده کنم؟', answer: 'پاسخ آزمایشی اختصاصی این محصول.' }],
        });
        return true;
    }
    if (/^\/api\/products\/[^/]+\/related$/.test(url.pathname)) { send(related.slice(0, 4)); return true; }
    if (url.pathname.startsWith('/api/product-reviews/product/')) {
        const summary = { averageRating: 4, totalApprovedReviews: 1, fourStarCount: 1 };
        send(url.pathname.endsWith('/summary') ? summary : { summary, reviews: { items: [
            { id: productGuid(700), productId: productGuid(500), userDisplayName: 'کاربر آزمایشی', comment: 'نظر تأییدشده آزمایشی برای بررسی صفحه محصول.', rating: 4, isBuyer: true, isApproved: true, createdAt: '2026-09-01T00:00:00Z', replies: [] },
        ], page: 1, pageSize: 10, totalCount: 1 } });
        return true;
    }
    if (url.pathname === '/api/cart/items' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => { body += chunk; });
        req.on('end', () => {
            const payload = JSON.parse(body);
            productWrites.push(payload);
            send({ items: [], totalQuantity: payload.quantity ?? payload.Quantity }, mode === 'product-cart-failure' ? 400 : 200);
        });
        return true;
    }
    return false;
}
