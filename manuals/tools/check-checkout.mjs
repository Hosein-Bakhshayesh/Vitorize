import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { checkoutGuid } from './checkout-fixture.mjs';

export async function checkCheckout(page, output, fixture) {
    const base = 'http://127.0.0.1:5088';
    const root = page.locator('.checkout-page');
    const result = page.locator('.payment-page');
    const ready = () => page.waitForFunction(() => document.querySelector('.checkout-page')?.getAttribute('aria-busy') === 'false');
    const go = async (path = '/checkout') => { await page.goto(base + path, { waitUntil: 'networkidle' }); await page.evaluate(() => document.fonts.ready); };
    const fill = async () => {
        await root.locator('input[type=email]').fill('fixture@example.test');
        await root.locator('select').selectOption({ label: 'آمریکا' });
        await root.locator('input[id^="checkout-input-"][id$="-password"]').fill('fixture-only');
        await root.getByLabel('تکرار رمز آزمایشی', { exact: true }).fill('fixture-only');
        await root.locator('textarea').fill('اطلاعات صرفاً آزمایشی');
    };
    const pay = () => root.getByTestId('checkout-pay').click();
    const report = { viewports: [], checks: [] };
    for (const width of [1728, 1280, 1100, 1024, 768, 402, 360, 320]) {
        await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 });
        await go(); await ready();
        assert.equal(await root.getByTestId('checkout-input-card').count(), 1);
        assert.match(await root.getByTestId('checkout-payable').textContent(), /۲۲۳,۴۵۶/);
        assert.match(await root.getByTestId('checkout-vat-row').textContent(), /۲۳,۴۵۶/);
        const geometry = await page.evaluate(() => ({ width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth,
            columns: getComputedStyle(document.querySelector('.checkout-layout')).gridTemplateColumns.split(' ').length,
            fieldTracks: getComputedStyle(document.querySelector('.checkout-fields')).gridTemplateColumns,
            fieldColumns: new Set([...document.querySelectorAll('.checkout-fields > .st-field')].map(el => Math.round(el.getBoundingClientRect().left))).size,
        }));
        assert.equal(geometry.overflow, false, `overflow at ${width}`);
        assert.equal(geometry.columns, width > 1024 ? 2 : 1);
        assert.equal(geometry.fieldColumns, width > 1100 ? 2 : 1);
        assert.equal(await root.locator('[aria-current=step]').count(), 1);
        if ([1728, 402].includes(width)) {
            await page.screenshot({ path: resolve(output, `checkout-${width}.png`), fullPage: true });
            await page.screenshot({ path: resolve(output, `checkout-${width}-viewport.png`) });
        }
        report.viewports.push(geometry);
    }
    await page.setViewportSize({ width: 1280, height: 600 });
    await go(); await ready();
    await root.getByTestId('checkout-pay').scrollIntoViewIfNeeded();
    const shortViewportPay = await root.getByTestId('checkout-pay').boundingBox();
    assert(shortViewportPay.y >= 0 && shortViewportPay.y + shortViewportPay.height <= 600, 'pay button reachable on short desktop');
    await page.setViewportSize({ width: 402, height: 874 });
    await go(); await ready();
    const beforeInvalid = fixture.writes.length;
    await pay();
    await root.locator('.st-field__error').first().waitFor();
    await page.waitForFunction(() => document.activeElement?.id.endsWith('-email'));
    assert.equal(fixture.writes.length, beforeInvalid, 'validation blocks all order/payment writes');
    assert.equal(await root.locator('input[type=email]').getAttribute('aria-invalid'), 'true');
    await fill();
    await root.getByLabel('تکرار رمز آزمایشی', { exact: true }).fill('mismatch');
    await pay();
    await root.getByText('تکرار «رمز آزمایشی» یکسان نیست.', { exact: true }).waitFor();
    assert.equal(fixture.writes.length, beforeInvalid);
    await fill();
    fixture.setMode('checkout-input-failure');
    await pay();
    await page.getByText('ذخیره اطلاعات آزمایشی انجام نشد.', { exact: true }).waitFor();
    assert.equal(fixture.writes.filter(w => w.path === '/api/checkout').length, 0);
    fixture.setMode('checkout-order-failure');
    await pay();
    await page.getByText('ثبت سفارش آزمایشی انجام نشد.', { exact: true }).waitFor();
    assert.equal(fixture.writes.filter(w => w.path.startsWith('/api/payments/')).length, 0);
    report.checks.push('Required fields, confirmation mismatch, first-error focus, failed input persistence and order creation block payment');

    fixture.setMode('checkout');
    await go('/checkout?coupon=TEST'); await ready();
    assert.match(await root.getByTestId('checkout-payable').textContent(), /۱۵۵,۵۵۵/);
    assert.match(await root.getByTestId('checkout-vat-row').textContent(), /۵,۵۵۵/);
    await root.getByRole('button', { name: 'حذف کد تخفیف', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.st-promo input')?.disabled === false);
    await root.getByLabel('کد تخفیف', { exact: true }).fill('BAD');
    await root.getByLabel('کد تخفیف', { exact: true }).press('Enter');
    await root.getByText('کد تخفیف نامعتبر است.', { exact: true }).waitFor();
    assert.match(await root.getByTestId('checkout-payable').textContent(), /۲۲۳,۴۵۶/);
    await fill();
    fixture.writes.length = 0;
    fixture.setMode('checkout-payment-failure');
    await pay();
    await root.getByTestId('checkout-pending-order').waitFor();
    assert.equal(fixture.writes.filter(w => w.path === '/api/checkout').length, 1);
    const save = fixture.writes.find(w => w.method === 'PUT');
    const values = save.body.inputValues ?? save.body.InputValues;
    assert.equal(values.email, 'fixture@example.test');
    assert.equal(values.region, 'آمریکا');
    assert.equal(save.body.quantity ?? save.body.Quantity, 2);
    assert.match(fixture.writes.find(w => w.path === '/api/checkout').idempotencyKey, /^[a-f0-9]{32}$/);
    fixture.setMode('checkout');
    await pay();
    await page.waitForURL('**/payment/result?orderId=*&paid=1');
    await result.getByRole('heading', { name: 'پرداخت با موفقیت انجام شد' }).waitFor();
    assert.equal(fixture.writes.filter(w => w.path === '/api/checkout').length, 1, 'payment retry must not duplicate checkout');
    assert.equal(fixture.writes.filter(w => w.path.startsWith('/api/payments/start/')).length, 2);
    report.checks.push('Coupon query/invalid/remove, server totals, persisted line payload, idempotency key, pending-order retry without duplicate checkout');

    fixture.setMode('checkout-low-wallet');
    await go(); await ready();
    assert.equal(await root.getByRole('button', { name: /کیف پول ویتورایز/ }).isDisabled(), true);
    fixture.setMode('checkout');
    await go(); await ready(); await fill();
    const wallet = root.getByRole('button', { name: /کیف پول ویتورایز/ });
    await wallet.focus(); await page.keyboard.press('Enter');
    await page.waitForFunction(() => document.querySelectorAll('.st-paycard')[1]?.getAttribute('aria-pressed') === 'true');
    fixture.writes.length = 0;
    await pay();
    await page.waitForURL('**/payment/result?orderId=*&paid=1');
    assert.equal(fixture.writes.filter(w => w.path.startsWith('/api/payments/wallet/pay/')).length, 1);
    assert.equal(fixture.writes.filter(w => w.path.startsWith('/api/payments/start/')).length, 0);
    for (const width of [1728, 402, 320]) {
        await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 });
        for (const paid of ['1', '0']) {
            await go(`/payment/result?orderId=${checkoutGuid(1300)}&paid=${paid}`);
            await result.locator('h1').waitFor();
            if (paid === '0') await result.getByRole('button', { name: 'تلاش مجدد پرداخت', exact: true }).waitFor();
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, `payment overflow ${width}`);
            if (width !== 320) await page.screenshot({ path: resolve(output, `payment-${paid === '1' ? 'success' : 'failure'}-${width}.png`), fullPage: true });
        }
    }
    await result.getByRole('button', { name: 'تلاش مجدد پرداخت', exact: true }).click();
    await page.waitForURL('**/payment/result?orderId=*&paid=1');
    fixture.setMode('checkout-no-retry');
    await go(`/payment/result?orderId=${checkoutGuid(1300)}&paid=0`);
    assert.equal(await result.getByRole('button', { name: 'تلاش مجدد پرداخت', exact: true }).count(), 0);
    await go('/payment/result?paid=0');
    assert.equal(await result.getByRole('link', { name: 'جزئیات سفارش', exact: true }).count(), 0);
    fixture.setMode('checkout-payment-failure');
    await go(`/payment/result?orderId=${checkoutGuid(1300)}&paid=0`);
    await result.getByRole('button', { name: 'تلاش مجدد پرداخت', exact: true }).click();
    await page.getByText('اتصال به درگاه آزمایشی انجام نشد.', { exact: true }).waitFor();
    await page.waitForFunction(() => !document.querySelector('.payment-page button'));
    fixture.setMode('checkout-external');
    await go(); await ready(); await fill(); await pay();
    await page.waitForURL('**/shop?fixture-gateway=1');
    report.checks.push('Keyboard wallet, insufficient balance, wallet-only endpoint, success/failure layouts, retry eligibility/failure, absent order ID, loopback gateway redirect');
    fixture.setMode('checkout-empty');
    await go(); await ready();
    assert.equal(await root.getByTestId('checkout-pay').count(), 0);
    fixture.setMode('checkout');
    await go(); await ready();
    await page.evaluate(() => window.vzTheme.set('dark'));
    assert.equal(await root.locator('.checkout-summary').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(32, 39, 41)');
    await page.screenshot({ path: resolve(output, 'checkout-dark.png'), fullPage: true });
    await page.evaluate(() => window.vzTheme.set('light'));
    console.log(JSON.stringify({ checkout: report }, null, 2));
    return report;
}
