// Synthetic auth responses only. No SMS provider, database or real credentials.
export const authWrites = [];
export function serveAuthFixture(req, res, url, mode) {
    if (!mode.startsWith('auth') || req.method !== 'POST'
        || (!url.pathname.startsWith('/api/auth/') && url.pathname !== '/api/cart/merge-guest')) return false;
    let raw = '';
    req.on('data', chunk => { raw += chunk; });
    req.on('end', () => {
        const body = raw ? JSON.parse(raw) : {};
        authWrites.push({ path: url.pathname, body });
        const send = (data, status = 200, message = '', errorCode = null) => {
            res.writeHead(status, { 'content-type': 'application/json' });
            res.end(JSON.stringify({ isSuccess: status === 200, data, message, errorCode }));
        };
        const session = { userId: '00000000-0000-0000-0000-000000009001', fullName: 'مشتری آزمایشی', mobile: '09000000000', accessToken: 'fixture-only-not-a-real-token', refreshToken: 'fixture-only-not-a-real-refresh' };
        if (url.pathname === '/api/auth/login/otp/request') {
            if (mode === 'auth-otp-failure') send(null, 400, 'ارسال آزمایشی ناموفق بود.');
            else send({ maskedMobile: '0900***0000', expirySeconds: 180, resendCooldownSeconds: 1,
                outcome: mode === 'auth-unregistered' ? 'RequiresRegistration' : mode === 'auth-blocked' ? 'AccountNotEligible' : 'OtpSent' });
        } else if (url.pathname === '/api/auth/login') {
            if (mode === 'auth-unregistered') send(null, 400, 'حسابی یافت نشد.', 'RequiresRegistration');
            else if (mode === 'auth-password-failure') send(null, 400, 'رمز آزمایشی نادرست است.', 'InvalidCredentials');
            else send(session);
        } else if (url.pathname.endsWith('/verify')) {
            if ((body.code ?? body.Code) !== '123456') send(null, 400, 'کد آزمایشی معتبر نیست.');
            else send(session);
        } else if (url.pathname === '/api/auth/register' || url.pathname === '/api/auth/register/resend') {
            if (mode === 'auth-register-failure') send(null, 400, 'ثبت‌نام آزمایشی انجام نشد.');
            else send({ maskedMobile: '0900***0000', expirySeconds: 180, resendCooldownSeconds: 1, outcome: 'RegistrationOtpSent' });
        } else if (url.pathname === '/api/auth/reset-password' && mode === 'auth-reset-failure') send(null, 400, 'کد بازیابی آزمایشی معتبر نیست.');
        else send({});
    });
    return true;
}
