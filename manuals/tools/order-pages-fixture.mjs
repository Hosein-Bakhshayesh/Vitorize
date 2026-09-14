// In-memory orders only: mutations never touch a real order, payment or review.
export const orderGuid = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0');
export const orderPageWrites = [];
let orders, reviews;
export function resetOrderPages() {
    reviews = [];
    orderPageWrites.length = 0;
    orders = new Map([1, 2, 3, 4].map(n => {
        const item = { id: orderGuid(11000 + n), productId: orderGuid(12000 + n), productTitle: 'اشتراک دیجیتال آزمایشی', variantTitle: 'نسخه سالانه', quantity: 2, unitPrice: 100000, totalPrice: 200000,
            deliveryType: 1, deliveryStatus: n === 3 ? 2 : 1, inputValues: [
                { fieldKey: 'email', fieldLabel: 'ایمیل دریافت', fieldType: 2, value: 'fixture@example.test' },
                { fieldKey: 'secret', fieldLabel: 'رمز ثبت‌شده', fieldType: 12, value: '********', isSensitive: true, isMasked: true },
            ], deliveries: n === 3 ? [
                { id: orderGuid(13001), isVisibleToCustomer: true, deliveredContent: 'FIXTURE-ONLY-CODE-ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890' },
                { id: orderGuid(13002), isVisibleToCustomer: false, deliveredContent: 'HIDDEN-CONTENT-MUST-NOT-RENDER' },
            ] : [],
            kyc: n === 4 ? { blocksFulfillment: true, lifecycleLabel: 'نیازمند مدارک', customerAction: 'SubmitVerification', customerActionLabel: 'مدارک موردنیاز را تکمیل کنید.', policyTitle: 'سیاست آزمایشی', policyInstructions: 'دستورالعمل صرفاً آزمایشی؛ هیچ مدرک واقعی بارگذاری نکنید.', documents: [{ documentTypeId: orderGuid(14001), title: 'مدرک آزمایشی', isRequired: true, uploadStatus: 'Missing' }] } : null,
        };
        const order = { id: orderGuid(10000 + n), orderNumber: `VZ-TEST-${n}`, status: n === 2 ? 4 : n === 3 ? 3 : n === 4 ? 2 : 1, paymentStatus: n >= 3 ? 2 : 1,
            canCustomerCancel: n === 1, canCustomerHide: n === 2, subtotalAmount: 200000, discountAmount: 20000, finalAmount: 197654,
            vatEnabled: true, vatRatePercent: 10, vatAmount: 17654, createdAt: `2026-09-0${n}T10:00:00Z`, paidAt: n >= 3 ? `2026-09-0${n}T10:05:00Z` : null, items: [item],
        };
        return [order.id, order];
    }));
}
resetOrderPages();
export function serveOrderPagesFixture(req, res, url, mode) {
    if (!mode.startsWith('order-pages')) return false;
    const send = (data, status = 200, message = '') => { res.writeHead(status, { 'content-type': 'application/json' }); res.end(JSON.stringify({ isSuccess: status === 200, data, message })); };
    const id = url.pathname.split('/')[3];
    if (req.method === 'GET') {
        if (url.pathname === '/api/orders') send(mode === 'order-pages-empty' ? [] : [...orders.values()], mode === 'order-pages-failure' ? 503 : 200);
        else if (url.pathname.startsWith('/api/orders/')) {
            const order = orders.get(id);
            if (!order || mode === 'order-pages-missing') send(null, 404, 'سفارش یافت نشد.');
            else send({ ...order, vatEnabled: mode !== 'order-pages-no-vat', canCustomerCancel: mode === 'order-pages-blocked' ? false : order.canCustomerCancel, customerCancelBlockReason: mode === 'order-pages-blocked' ? 'در انتظار تعیین تکلیف پرداخت آزمایشی' : null });
        } else if (url.pathname === '/api/product-reviews/mine') send(reviews);
        else if (url.pathname.startsWith('/api/payments/retry-eligibility/')) send({ canRetry: mode !== 'order-pages-blocked' && url.pathname.endsWith(orderGuid(10001)) });
        else return false;
        return true;
    }
    if (req.method === 'POST' && (url.pathname.startsWith('/api/orders/') || url.pathname.startsWith('/api/payments/') || url.pathname === '/api/product-reviews')) {
        let raw = '';
        req.on('data', chunk => { raw += chunk; });
        req.on('end', () => {
            const body = raw ? JSON.parse(raw) : null;
            orderPageWrites.push({ path: url.pathname, method: req.method, body });
            if (mode === 'order-pages-mutation-failure') { send(null, 400, 'عملیات آزمایشی پذیرفته نشد.'); return; }
            if (url.pathname.endsWith('/cancel')) { const order = orders.get(id); order.status = 4; order.canCustomerCancel = false; order.canCustomerHide = true; send({}); }
            else if (url.pathname.endsWith('/hide')) { orders.delete(id); send({}); }
            else if (url.pathname.startsWith('/api/payments/retry/')) send({ paymentId: orderGuid(15001), paymentUrl: mode === 'order-pages-gateway' ? 'http://127.0.0.1:5088/shop?fixture-gateway=orders' : null });
            else if (url.pathname.startsWith('/api/payments/mock/verify/')) send({ isPaid: true });
            else if (url.pathname === '/api/product-reviews') {
                const review = { id: orderGuid(16001), productId: body.ProductId, title: body.Title, comment: body.Comment, rating: body.Rating, isApproved: true };
                reviews.push(review); send(review);
            } else send({});
        });
        return true;
    }
    return false;
}
