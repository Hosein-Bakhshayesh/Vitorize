import assert from 'node:assert/strict';
import { resolve } from 'node:path';

export async function checkAuth(page, output, fixture) {
    const base = 'http://127.0.0.1:5088';
    const root = page.locator('.auth-page');
    const go = async path => { await page.goto(base + path, { waitUntil: 'networkidle' }); await page.evaluate(() => document.fonts.ready); };
    const submit = () => root.locator('button[type=submit]').first().click();
    const report = { viewports: [], checks: [] };
    const mobile = '09000000000';
    const fakePassword = 'Fixture-only-123!';
    const clearSession = () => page.context().clearCookies();
    const passwordFields = async () => { await root.getByLabel('شماره موبایل', { exact: true }).fill(mobile); await root.getByLabel('رمز عبور', { exact: true }).fill(fakePassword); };
    const registrationFields = async () => {
        await root.getByLabel('نام و نام خانوادگی').fill('مشتری آزمایشی');
        await root.getByLabel('شماره موبایل', { exact: true }).fill(mobile);
        await root.getByLabel('ایمیل (اختیاری)').fill('fixture@example.test');
        await root.getByLabel('رمز عبور', { exact: true }).fill(fakePassword);
    };
    for (const width of [1728, 1280, 1024, 768, 402, 360, 320]) {
        await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 });
        for (const path of ['/login', '/login?otp=1', '/register', '/forgot-password', '/reset-password']) {
            await go(path);
            await root.waitFor();
            const geometry = await root.evaluate(el => ({
                width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth,
                cardWidth: el.getBoundingClientRect().width,
                unlabeled: [...el.querySelectorAll('input:not([type=hidden])')].filter(input => !input.labels?.length).length,
                smallInputs: [...el.querySelectorAll('.st-input')].some(input => input.getBoundingClientRect().height < 44),
            }));
            assert.equal(geometry.overflow, false, `${path} at ${width}`);
            assert.equal(geometry.unlabeled, 0, path);
            assert.equal(geometry.smallInputs, false, path);
            assert(geometry.cardWidth <= 480);
            if ([1728, 402].includes(width)) await page.screenshot({ path: resolve(output, `auth-${path.slice(1).replace('?otp=1', '-otp')}-${width}.png`), fullPage: true });
            report.viewports.push({ path, ...geometry });
        }
    }
    await page.setViewportSize({ width: 402, height: 874 });
    await go('/login?returnUrl=%2Fshop');
    assert.equal(await root.locator('input[name=returnUrl]').inputValue(), '/shop');
    assert.equal(await root.getByRole('link', { name: 'ثبت‌نام کنید' }).getAttribute('href'), '/register?returnUrl=%2Fshop');
    await submit();
    assert.equal(await root.locator('#pw-mobile').getAttribute('aria-invalid'), 'true');
    assert.equal(fixture.writes.length, 0);
    fixture.setMode('auth-password-failure');
    await passwordFields(); await submit();
    await root.getByRole('alert').filter({ hasText: 'رمز آزمایشی نادرست است.' }).waitFor();
    assert.deepEqual(fixture.writes.at(-1).body, { Mobile: mobile, Password: fakePassword });
    fixture.setMode('auth-unregistered');
    await passwordFields(); await submit();
    await root.getByTestId('login-requires-registration').waitFor();
    assert.equal(await root.getByTestId('login-register-cta').getAttribute('href'), '/register?returnUrl=%2Fshop');
    assert.equal(await page.locator('#otp-code').count(), 0);
    fixture.setMode('auth');
    await passwordFields(); await submit();
    await page.waitForURL('**/shop');
    assert((await page.context().cookies()).some(c => c.name === 'Vitorize.Customer.Auth' && c.httpOnly));
    await clearSession();
    report.checks.push('Password form: required validation, failure, registration outcome, unchanged POST payload and return URL, local synthetic session cookie');

    for (const mode of ['auth-unregistered', 'auth-blocked', 'auth-otp-failure']) {
        fixture.setMode(mode); await go('/login?otp=1&returnUrl=%2Fshop');
        await root.getByLabel('شماره موبایل', { exact: true }).fill(mobile);
        await root.getByLabel('شماره موبایل', { exact: true }).press('Enter');
        if (mode === 'auth-unregistered') await root.getByTestId('login-requires-registration').waitFor();
        else await root.locator('#otp-error').waitFor();
        assert.equal(await root.locator('#otp-code').count(), 0);
    }
    fixture.setMode('auth'); await go('/login?otp=1&returnUrl=%2Fshop');
    const beforeEmptyOtp = fixture.writes.length;
    await submit(); await root.locator('#otp-error').waitFor();
    assert.equal(fixture.writes.length, beforeEmptyOtp);
    await root.getByLabel('شماره موبایل', { exact: true }).fill(mobile);
    await submit(); await root.locator('#otp-code').waitFor();
    await page.screenshot({ path: resolve(output, 'auth-login-code-402.png'), fullPage: true });
    await root.locator('#otp-code').fill('000000'); await root.locator('#otp-code').press('Enter');
    await root.locator('#otp-error').waitFor();
    assert.equal(await root.locator('#otp-code').getAttribute('aria-invalid'), 'true');
    await root.getByRole('button', { name: 'ارسال مجدد کد' }).click();
    await page.waitForFunction(() => document.querySelector('#otp-code')?.value === '');
    await root.getByRole('button', { name: 'تغییر شماره موبایل' }).click();
    await root.locator('#otp-mobile').waitFor();
    await root.getByRole('button', { name: 'رمز عبور', exact: true }).focus();
    await page.keyboard.press('Enter'); await root.locator('#pw-pass').waitFor();
    await root.getByRole('button', { name: 'کد یکبار مصرف' }).click();
    await submit(); await root.locator('#otp-code').waitFor();
    await root.locator('#otp-code').fill('123456'); await root.getByRole('button', { name: 'تایید و ورود' }).click();
    await page.waitForURL('**/shop');
    assert((await page.context().cookies()).some(c => c.name === 'Vitorize.Customer.Auth'));
    await clearSession();
    report.checks.push('OTP: Enter submission, empty/unknown/blocked/failure outcomes, code rejection, resend, change mobile, keyboard method switching and local completion');

    await go('/register?stage=verify&returnUrl=%2Fshop');
    assert.equal(await root.locator('#register-code').count(), 0, 'query cannot force verification without pending state');
    const beforeRegister = fixture.writes.length;
    await submit(); assert.equal(await root.locator('#register-name').getAttribute('aria-invalid'), 'true');
    assert.equal(fixture.writes.length, beforeRegister);
    await registrationFields(); await root.locator('#register-email').fill('invalid');
    await submit(); assert.equal(await root.locator('#register-email').getAttribute('aria-invalid'), 'true');
    assert.equal(fixture.writes.length, beforeRegister);
    fixture.setMode('auth-register-failure'); await registrationFields(); await submit();
    await root.getByTestId('register-error').waitFor();
    fixture.setMode('auth'); await registrationFields(); await submit();
    await root.getByTestId('register-otp-code').waitFor();
    assert.deepEqual(fixture.writes.filter(w => w.path === '/api/auth/register').at(-1).body, { FullName: 'مشتری آزمایشی', Mobile: mobile, Email: 'fixture@example.test', Password: fakePassword });
    assert(!(await page.context().cookies()).some(c => c.name === 'Vitorize.Customer.Auth'), 'registration alone is not login');
    assert.equal(await root.getByTestId('register-change-mobile').getAttribute('href'), '/register?returnUrl=%2Fshop');
    for (const width of [1728, 402, 320]) {
        await page.setViewportSize({ width, height: 1000 });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        if (width !== 320) await page.screenshot({ path: resolve(output, `auth-register-code-${width}.png`), fullPage: true });
    }
    await root.getByTestId('register-otp-resend').click(); await root.getByTestId('register-notice').waitFor();
    await root.getByTestId('register-otp-code').fill('000000'); await submit();
    await root.getByTestId('register-error').waitFor();
    await root.getByTestId('register-otp-code').fill('123456'); await submit(); await page.waitForURL('**/shop');
    assert((await page.context().cookies()).some(c => c.name === 'Vitorize.Customer.Auth'));
    assert(!(await page.context().cookies()).some(c => c.name === 'vitorize-registration'));
    await clearSession();
    report.checks.push('Registration: labels, required/email validation, API failure, pending-state gate, masked number, resend/rejected code, verify POST and preserved return URL');

    await page.setViewportSize({ width: 402, height: 874 });
    await go('/forgot-password'); const beforeForgot = fixture.writes.length;
    await submit(); await root.locator('#forgot-mobile-error').waitFor();
    assert.equal(fixture.writes.length, beforeForgot);
    await root.getByLabel('شماره موبایل').fill(mobile); await root.getByLabel('شماره موبایل').press('Enter');
    await page.waitForURL('**/reset-password?mobile=*');
    assert.equal(await root.locator('#reset-mobile').inputValue(), mobile);
    assert.deepEqual(fixture.writes.at(-1).body, { Mobile: mobile });
    const beforeReset = fixture.writes.length;
    await submit(); await root.locator('#reset-form-error').waitFor();
    assert.equal(await root.locator('#reset-code').getAttribute('aria-invalid'), 'true');
    assert.equal(fixture.writes.length, beforeReset);
    await root.getByLabel('کد بازیابی', { exact: true }).fill('123456');
    await root.getByLabel('رمز عبور جدید', { exact: true }).fill(fakePassword);
    await root.getByLabel('تکرار رمز عبور', { exact: true }).fill('mismatch'); await submit();
    await root.getByText('رمز عبور و تکرار آن یکسان نیست.', { exact: true }).waitFor();
    assert.equal(fixture.writes.length, beforeReset);
    await root.getByLabel('تکرار رمز عبور', { exact: true }).fill(fakePassword);
    fixture.setMode('auth-reset-failure'); await submit();
    await root.getByText('کد بازیابی آزمایشی معتبر نیست.', { exact: true }).waitFor();
    assert.deepEqual(fixture.writes.at(-1).body, { Mobile: mobile, Code: '123456', NewPassword: fakePassword, ConfirmNewPassword: fakePassword });
    fixture.setMode('auth'); await root.getByLabel('تکرار رمز عبور', { exact: true }).press('Enter'); await page.waitForURL('**/login');
    const toast = page.locator('.vz-toasts');
    await toast.getByText('رمز عبور با موفقیت تغییر کرد. اکنون وارد شوید.', { exact: true }).waitFor();
    const toastGeometry = await toast.boundingBox();
    assert(toastGeometry.x >= 0 && toastGeometry.x + toastGeometry.width <= 402, 'recovery toast stays inside viewport');
    assert(toastGeometry.y + toastGeometry.height <= 874 - 68, 'recovery toast clears mobile navigation');
    assert.equal(await toast.evaluate(el => getComputedStyle(el).position), 'fixed');
    report.checks.push('Recovery: empty and mismatch validation prevents writes, Enter submit, unchanged payload, server error and successful local reset navigation');
    await page.evaluate(() => document.documentElement.dataset.theme = 'dark');
    assert.equal(await root.locator('.auth-card').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(32, 39, 41)');
    await page.screenshot({ path: resolve(output, 'auth-dark.png'), fullPage: true });
    await toast.locator('.vz-toast__close').last().click();
    await toast.getByText('رمز عبور با موفقیت تغییر کرد. اکنون وارد شوید.', { exact: true }).waitFor({ state: 'detached' });
    assert.equal(await toast.getByText('رمز عبور با موفقیت تغییر کرد. اکنون وارد شوید.', { exact: true }).count(), 0);
    await page.evaluate(() => document.documentElement.dataset.theme = 'light');
    console.log(JSON.stringify({ auth: report }, null, 2));
    return report;
}
