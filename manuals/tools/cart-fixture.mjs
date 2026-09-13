// In-memory cart API for browser QA only. Never connects to real customer data.
const guid = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0');
export const cartWrites = [];
let items = [];
export function resetCart() {
    cartWrites.length = 0;
    items = [
        { id: guid(801), productId: guid(901), productVariantId: guid(951), productTitle: 'اشتراک پریمیوم دیجیتال', variantTitle: 'نسخه سه‌ماهه', quantity: 2, unitPrice: 250000, totalPrice: 500000, thumbnailImagePath: '/media/test.svg?cart=1', currencyType: 2 },
        { id: guid(802), productId: guid(902), productTitle: 'گیفت کارت فروشگاه', variantTitle: 'پیش‌فرض', quantity: 1, unitPrice: 300000, totalPrice: 300000, currencyType: 2 },
        { id: guid(803), productId: guid(903), productVariantId: guid(953), productTitle: 'اشتراک ویژه با عنوان طولانی برای بررسی نمایش در اندازه‌های مختلف صفحه', variantTitle: 'نسخه سالانه قابل استفاده روی دسکتاپ و موبایل', quantity: 1, unitPrice: 500000, totalPrice: 500000, thumbnailImagePath: '/media/missing-cart.png', currencyType: 2 },
    ];
}
function cart(mode) {
    const current = mode === 'cart-empty' ? [] : items;
    const subtotal = current.reduce((sum, item) => sum + item.totalPrice, 0);
    // Deliberately non-round server totals prove the UI renders authoritative values.
    const vatEnabled = mode !== 'cart-no-vat' && current.length > 0;
    const vatAmount = vatEnabled ? 43210 : 0;
    return { id: guid(800), items: current, totalQuantity: current.reduce((sum, item) => sum + item.quantity, 0), subtotalAmount: subtotal, vatEnabled, vatRatePercent: 10, vatAmount, finalAmount: subtotal + vatAmount };
}
export function serveCartFixture(req, res, url, mode) {
    if (!mode.startsWith('cart')) return false;
    const send = (data, status = 200, message = '') => {
        res.writeHead(status, { 'content-type': 'application/json' });
        res.end(JSON.stringify({ isSuccess: status === 200, data, message }));
    };
    if (url.pathname === '/api/cart' && req.method === 'GET') {
        if (mode === 'cart-load-failure' || mode === 'cart-refresh-failure') send(null, 503, 'Fixture unavailable');
        else send(cart(mode));
        return true;
    }
    if (url.pathname === '/media/missing-cart.png') {
        res.writeHead(404); res.end(); return true;
    }
    if (url.pathname.startsWith('/api/cart/items/') || url.pathname === '/api/cart/clear' || url.pathname === '/api/coupons/validate') {
        let raw = '';
        req.on('data', chunk => { raw += chunk; });
        req.on('end', () => {
            const body = raw ? JSON.parse(raw) : null;
            cartWrites.push({ method: req.method, path: url.pathname, body });
            if (mode === 'cart-mutation-failure') { send(null, 400, 'تغییر سبد انجام نشد؛ موجودی محصول را بررسی کنید.'); return; }
            if (url.pathname === '/api/coupons/validate') {
                const code = body.code ?? body.Code;
                if (code !== 'TEST') { send(null, 400, 'کد تخفیف نامعتبر است.'); return; }
                send({ couponId: guid(999), code, discountAmount: 150000, vatEnabled: true, vatRatePercent: 10, vatAmount: 12345, finalAmount: 1111111 });
            } else if (url.pathname === '/api/cart/clear') {
                items = []; send(null);
            } else {
                const id = url.pathname.split('/').at(-1);
                if (req.method === 'DELETE') items = items.filter(item => item.id !== id);
                if (req.method === 'PUT') {
                    const item = items.find(item => item.id === id);
                    item.quantity = body.quantity ?? body.Quantity;
                    item.totalPrice = item.quantity * item.unitPrice;
                }
                send(cart(mode));
            }
        });
        return true;
    }
    return false;
}
