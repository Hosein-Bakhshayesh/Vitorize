// Entirely synthetic checkout/payment server. Never creates a real order or payment.
export const checkoutWrites = [];
export const checkoutGuid = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0');
export function serveCheckoutFixture(req, res, url, mode) {
    if (!mode.startsWith('checkout')) return false;
    const field = (key, label, type, required, extra = {}) => ({ id: checkoutGuid(1200 + type), key, label, fieldType: type, isActive: true, isRequired: required, displayStage: 2, ...extra });
    const item = {
        id: checkoutGuid(1101), productId: checkoutGuid(1102), productTitle: 'اشتراک پریمیوم دیجیتال', variantTitle: 'نسخه سالانه', quantity: 2,
        unitPrice: 100000, totalPrice: 200000, thumbnailImagePath: '/media/test.svg?checkout=1',
        inputFields: mode === 'checkout-no-inputs' ? [] : [
            field('email', 'ایمیل دریافت', 2, true, { sortOrder: 0 }),
            field('region', 'منطقه حساب', 6, true, { options: ['آمریکا', 'اروپا'], sortOrder: 1 }),
            field('password', 'رمز آزمایشی', 12, false, { isSensitive: true, requiresConfirmation: true, sortOrder: 2 }),
            field('consent', 'تأیید اطلاعات', 8, false, { placeholder: 'اطلاعات را بررسی کرده‌ام', sortOrder: 3 }),
            field('note', 'توضیحات سفارش', 5, false, { sortOrder: 4 }),
        ], inputValues: [],
    };
    const cart = { id: checkoutGuid(1100), items: mode === 'checkout-empty' ? [] : [item], totalQuantity: 2, subtotalAmount: 200000, vatEnabled: true, vatRatePercent: 10, vatAmount: 23456, finalAmount: 223456 };
    const send = (data, status = 200, message = '') => {
        res.writeHead(status, { 'content-type': 'application/json' });
        res.end(JSON.stringify({ isSuccess: status === 200, data, message }));
    };
    if (req.method === 'GET') {
        if (url.pathname === '/api/cart') send(cart);
        else if (url.pathname === '/api/auth/me') send({ id: checkoutGuid(1000), fullName: 'مشتری آزمایشی', mobile: '09000000000' });
        else if (url.pathname === '/api/wallet') send({ balance: mode === 'checkout-low-wallet' ? 10 : 1000000 });
        else if (url.pathname.startsWith('/api/payments/retry-eligibility/')) send({ canRetry: mode !== 'checkout-no-retry' });
        else if (url.pathname.startsWith('/api/orders/')) send({ id: checkoutGuid(1300), items: [] });
        else return false;
        return true;
    }
    if (url.pathname.startsWith('/api/payments/') || url.pathname === '/api/checkout' || url.pathname.startsWith('/api/cart/items/') || url.pathname === '/api/coupons/validate') {
        let raw = '';
        req.on('data', chunk => { raw += chunk; });
        req.on('end', () => {
            const body = raw ? JSON.parse(raw) : null;
            checkoutWrites.push({ path: url.pathname, method: req.method, body, idempotencyKey: req.headers['idempotency-key'] });
            if (url.pathname.startsWith('/api/cart/items/')) {
                if (mode === 'checkout-input-failure') send(null, 400, 'ذخیره اطلاعات آزمایشی انجام نشد.');
                else send(cart);
            } else if (url.pathname === '/api/coupons/validate') {
                if ((body.code ?? body.Code) !== 'TEST') send(null, 400, 'کد تخفیف نامعتبر است.');
                else send({ couponId: checkoutGuid(1400), code: 'TEST', discountAmount: 50000, vatEnabled: true, vatRatePercent: 10, vatAmount: 5555, finalAmount: 155555 });
            } else if (url.pathname === '/api/checkout') {
                if (mode === 'checkout-order-failure') send(null, 400, 'ثبت سفارش آزمایشی انجام نشد.');
                else send({ orderId: checkoutGuid(1300), finalAmount: 223456 });
            } else if (url.pathname.startsWith('/api/payments/wallet/pay/')) {
                send({ isPaid: true, orderId: checkoutGuid(1300) });
            } else if (url.pathname.startsWith('/api/payments/mock/verify/')) {
                send({ isPaid: mode !== 'checkout-unpaid', orderId: checkoutGuid(1300) });
            } else if (url.pathname.startsWith('/api/payments/start/') || url.pathname.startsWith('/api/payments/retry/')) {
                if (mode === 'checkout-payment-failure') send(null, 400, 'اتصال به درگاه آزمایشی انجام نشد.');
                else send({ paymentId: checkoutGuid(1301), orderId: checkoutGuid(1300), paymentUrl: mode === 'checkout-external' ? 'http://127.0.0.1:5088/shop?fixture-gateway=1' : null });
            } else send(null, 500, 'Unexpected fixture endpoint');
        });
        return true;
    }
    return false;
}
