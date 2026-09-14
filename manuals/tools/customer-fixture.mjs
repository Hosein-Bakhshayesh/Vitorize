// Read-only customer dashboard data for an isolated local browser session.
export const customerGuid = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0');
export function serveCustomerFixture(req, res, url, mode) {
    if (!mode.startsWith('customer') || req.method !== 'GET') return false;
    const orders = Array.from({ length: 8 }, (_, i) => ({
        id: customerGuid(9500 + i), orderNumber: `VZ-2026-${String(i + 1).padStart(6, '0')}`,
        status: i % 6 + 1, paymentStatus: i % 5 + 1, finalAmount: 1234567 + i * 100000,
        createdAt: `2026-09-${String(i + 1).padStart(2, '0')}T10:00:00Z`, items: [],
    }));
    const data = {
        '/api/auth/me': { id: customerGuid(9001), fullName: mode === 'customer-long' ? 'مشتری با نام و نام خانوادگی بسیار طولانی برای بررسی چیدمان حساب کاربری' : 'مشتری آزمایشی', mobile: '09000000000', verificationStatus: 1 },
        '/api/wallet': { balance: mode === 'customer-empty' ? 0 : mode === 'customer-long' ? 123456789012345 : 9876543 },
        '/api/orders': mode === 'customer-empty' ? [] : orders,
        '/api/tickets': mode === 'customer-empty' ? [] : [1, 2, 3, 4].map((status, i) => ({ id: customerGuid(9600 + i), status })),
    };
    if (!(url.pathname in data)) return false;
    const fail = mode === 'customer-failure' || mode === 'customer-wallet-failure' && url.pathname === '/api/wallet';
    const send = () => {
        res.writeHead(fail ? 503 : 200, { 'content-type': 'application/json' });
        res.end(JSON.stringify({ isSuccess: !fail, data: fail || mode === 'customer-null' ? null : data[url.pathname], message: fail ? 'Fixture unavailable' : '' }));
    };
    if (mode === 'customer-slow') setTimeout(send, 1200); else send();
    return true;
}
