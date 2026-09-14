// Isolated wallet fixture. No real account, balance or gateway is contacted.
export const walletPageWrites = [];
let balance;
export function resetWalletPage() { balance = 9876543; walletPageWrites.length = 0; }
resetWalletPage();
export function serveWalletPageFixture(req, res, url, mode) {
    if (!mode.startsWith('wallet-page') || !url.pathname.startsWith('/api/wallet')) return false;
    const send = (data, status = 200, message = '') => { res.writeHead(status, { 'content-type': 'application/json' }); res.end(JSON.stringify({ isSuccess: status === 200, data, message })); };
    if (req.method === 'GET') {
        const tx = url.pathname.endsWith('/transactions');
        const failed = mode === 'wallet-page-failure' || mode === (tx ? 'wallet-page-tx-failure' : 'wallet-page-balance-failure');
        const data = mode === 'wallet-page-null' ? null : tx ? mode === 'wallet-page-empty' ? [] : [
            { id: '00000000-0000-0000-0000-000000020001', type: 1, amount: 200123, balanceAfter: 9876543, referenceType: 1, description: 'شارژ آزمایشی قدیمی', createdAt: '2026-09-01T10:00:00Z' },
            { id: '00000000-0000-0000-0000-000000020002', type: 2, amount: 301234, balanceAfter: 9575309, referenceType: 2, description: mode === 'wallet-page-long' ? 'FIXTURE-LONG-DESCRIPTION-'.repeat(20) : 'پرداخت سفارش آزمایشی جدید', createdAt: '2026-09-02T10:00:00Z' },
            { id: '00000000-0000-0000-0000-000000020003', type: 1, amount: 10101, balanceAfter: 9585410, referenceType: 1, description: '', createdAt: '2026-08-01T10:00:00Z' },
        ] : { balance: mode === 'wallet-page-empty' ? 0 : mode === 'wallet-page-long' ? 999999999999 : balance };
        const respond = () => send(failed ? null : data, failed ? 503 : 200);
        if (mode === 'wallet-page-slow') setTimeout(respond, 1000); else respond();
        return true;
    }
    if (req.method === 'POST') {
        let raw = '';
        req.on('data', chunk => { raw += chunk; });
        req.on('end', () => {
            const body = raw ? JSON.parse(raw) : null;
            walletPageWrites.push({ path: url.pathname, body });
            const respond = () => {
                if (url.pathname === '/api/wallet/topup') {
                    if (mode === 'wallet-page-start-failure') send(null, 400, 'شارژ آزمایشی پذیرفته نشد.');
                    else send({ topUpId: '00000000-0000-0000-0000-000000021001', paymentUrl: mode === 'wallet-page-gateway' ? 'http://127.0.0.1:5088/shop?fixture-gateway=wallet' : null });
                } else if (url.pathname === '/api/wallet/topup/mock/verify/00000000-0000-0000-0000-000000021001') {
                    if (mode === 'wallet-page-verify-failure') send(null, 400, 'تأیید آزمایشی انجام نشد.');
                    else if (mode === 'wallet-page-unpaid') send({ isPaid: false, amount: 123456 }, 200, 'پرداخت آزمایشی ناموفق بود.');
                    else { balance = 12345678; send({ isPaid: true, amount: 123456 }); }
                } else send(null, 404);
            };
            if (mode === 'wallet-page-busy') setTimeout(respond, 1000); else respond();
        });
        return true;
    }
    return false;
}
