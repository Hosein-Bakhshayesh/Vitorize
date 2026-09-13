import assert from 'node:assert/strict';
import { resolve } from 'node:path';

export async function checkCart(page, output, fixture) {
    const root = page.locator('.cart-page');
    const rows = root.getByTestId('cart-item');
    const ready = () => page.waitForFunction(() => document.querySelector('.cart-page')?.getAttribute('aria-busy') === 'false');
    const go = async (query = '') => {
        await page.goto('http://127.0.0.1:5088/cart' + query, { waitUntil: 'networkidle' });
        await ready();
        await page.evaluate(() => document.fonts.ready);
    };
    const payable = () => root.getByTestId('cart-payable').locator('span').last().textContent();
    const report = { viewports: [], checks: [] };
    for (const width of [1728, 1280, 1100, 1024, 768, 402, 360, 320]) {
        await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 });
        await go();
        assert.equal(await rows.count(), 3);
        assert.equal(await root.locator('.cart-count').textContent(), '۴ کالا');
        assert.equal(await rows.nth(1).locator('.cart-variant').count(), 0, 'implicit SKU hidden');
        assert.equal(await rows.first().locator('.cart-variant').textContent(), 'نسخه سه‌ماهه');
        assert.match(await payable(), /۱,۳۴۳,۲۱۰/);
        assert.match(await root.getByTestId('cart-vat-row').textContent(), /۴۳,۲۱۰/);
        const geometry = await page.evaluate(() => {
            const rect = el => { const r = el.getBoundingClientRect(); return { x: r.x, y: r.y, right: r.right, bottom: r.bottom, width: r.width, height: r.height }; };
            const overlap = (a, b) => a.x < b.right && a.right > b.x && a.y < b.bottom && a.bottom > b.y;
            const rows = [...document.querySelectorAll('.cart-page .st-cart-item')];
            return {
                width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth,
                columns: getComputedStyle(document.querySelector('.cart-layout')).gridTemplateColumns.split(' ').length,
                overlaps: rows.some(row => overlap(rect(row.querySelector('.st-qty')), rect(row.querySelector('.st-cart-item__total')))),
                imagesSquare: rows.every(row => { const r = rect(row.querySelector('.st-cart-item__image')); return Math.abs(r.width - r.height) < 1; }),
            };
        });
        assert.equal(geometry.overflow, false, `overflow at ${width}`);
        assert.equal(geometry.overlaps, false, `quantity/total overlap at ${width}`);
        assert.equal(geometry.columns, width > 1024 ? 2 : 1);
        assert.equal(geometry.imagesSquare, true);
        assert.equal(await rows.first().locator('img').evaluate(el => el.complete && el.naturalWidth > 0), true);
        assert.equal(await rows.nth(2).locator('.st-img__ph').isVisible(), true, 'broken image fallback');
        report.viewports.push(geometry);
        if ([1728, 402].includes(width)) {
            await page.screenshot({ path: resolve(output, `cart-${width}.png`), fullPage: true });
            await page.screenshot({ path: resolve(output, `cart-${width}-viewport.png`) });
        }
    }
    const waitWrite = async action => {
        const before = fixture.writes.length;
        await action();
        for (let i = 0; i < 100 && fixture.writes.length === before; i++) await page.waitForTimeout(50);
        assert.equal(fixture.writes.length, before + 1);
        await ready();
        return fixture.writes.at(-1);
    };
    await page.setViewportSize({ width: 1280, height: 1000 });
    await go();
    let write = await waitWrite(() => rows.first().getByRole('button', { name: /^افزایش تعداد/ }).click());
    await page.waitForFunction(() => document.querySelector('.cart-quantity')?.textContent === '۳');
    assert.equal(write.method, 'PUT');
    assert(write.path.endsWith('801'));
    assert.equal(write.body.quantity ?? write.body.Quantity, 3);
    assert.match(await payable(), /۱,۵۹۳,۲۱۰/);
    await waitWrite(() => rows.first().getByRole('button', { name: /^کاهش تعداد/ }).click());
    await page.waitForFunction(() => document.querySelector('.cart-quantity')?.textContent === '۲');
    assert.match(await payable(), /۱,۳۴۳,۲۱۰/);
    fixture.setMode('cart-mutation-failure');
    await waitWrite(() => rows.first().getByRole('button', { name: /^افزایش تعداد/ }).click());
    await page.getByText('تغییر سبد انجام نشد؛ موجودی محصول را بررسی کنید.', { exact: true }).waitFor();
    assert.equal(await rows.first().locator('.cart-quantity').textContent(), '۲');
    assert.equal(await rows.count(), 3);
    await waitWrite(() => rows.first().getByRole('button', { name: /^حذف / }).click());
    assert.equal(await rows.count(), 3, 'failed deletion preserves items');
    fixture.setMode('cart');
    report.checks.push('Server-backed quantity updates and payable; rejected stock/update/delete keeps previous items');

    await root.getByLabel('کد تخفیف', { exact: true }).fill('BAD');
    await waitWrite(() => root.getByRole('button', { name: 'اعمال کد تخفیف', exact: true }).click());
    await root.locator('#cart-coupon-message').getByText('کد تخفیف نامعتبر است.', { exact: true }).waitFor();
    assert.match(await payable(), /۱,۳۴۳,۲۱۰/);
    await root.getByLabel('کد تخفیف', { exact: true }).fill(' TEST ');
    write = await waitWrite(() => root.getByLabel('کد تخفیف', { exact: true }).press('Enter'));
    await page.waitForFunction(() => document.querySelector('#cart-coupon')?.disabled);
    assert.equal(write.path, '/api/coupons/validate');
    assert.equal(write.body.code ?? write.body.Code, 'TEST');
    assert.equal(write.body.orderAmount ?? write.body.OrderAmount, 1300000);
    assert.match(await payable(), /۱,۱۱۱,۱۱۱/);
    assert.match(await root.getByTestId('cart-vat-row').textContent(), /۱۲,۳۴۵/);
    await page.screenshot({ path: resolve(output, 'cart-coupon.png'), fullPage: true });
    await root.getByRole('button', { name: 'حذف کد تخفیف', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('#cart-coupon')?.disabled === false);
    assert.match(await payable(), /۱,۳۴۳,۲۱۰/);
    report.checks.push('Coupon enter submission, trimmed API payload, invalid/success/remove states; authoritative VAT and payable (no UI arithmetic)');

    fixture.setMode('cart-refresh-failure');
    await waitWrite(() => rows.first().getByRole('button', { name: /^حذف / }).click());
    await root.getByTestId('cart-load-error').waitFor();
    assert.equal(await rows.count(), 3, 'read failure preserves last known items');
    fixture.setMode('cart');
    await root.getByRole('button', { name: 'تلاش مجدد', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.cart-page .st-cart-item').length === 2);
    assert.equal(await root.getByTestId('cart-load-error').count(), 0);
    write = await waitWrite(() => rows.first().getByRole('button', { name: /^کاهش تعداد/ }).click());
    await page.waitForFunction(() => document.querySelectorAll('.cart-page .st-cart-item').length === 1);
    assert.equal(write.method, 'DELETE', 'decrementing one retains existing remove behavior');
    assert(write.path.endsWith('802'));
    await waitWrite(() => root.getByRole('button', { name: 'خالی کردن سبد خرید', exact: true }).click());
    await root.locator('.st-errpage__title').waitFor();
    assert.equal(await rows.count(), 0);
    assert.equal(await root.locator('.st-cart-sum').count(), 0);
    report.checks.push('Delete, decrement-to-remove, clear, failed refresh retains items, retry recovers');

    fixture.reset();
    fixture.setMode('cart-empty');
    await page.setViewportSize({ width: 402, height: 874 });
    await go();
    assert.equal(await rows.count(), 0);
    assert.equal(await root.getByTestId('cart-load-error').count(), 0);
    await page.screenshot({ path: resolve(output, 'cart-empty.png'), fullPage: true });
    fixture.setMode('cart-load-failure');
    await go();
    await root.getByTestId('cart-load-error').waitFor();
    assert.equal(await root.locator('.st-errpage__title').count(), 0, 'load error is not an empty cart');
    fixture.setMode('cart');
    await root.getByRole('button', { name: 'تلاش مجدد', exact: true }).click();
    await rows.first().waitFor();
    await go('?mergeError=1');
    assert.match(await root.getByRole('alert').textContent(), /اطلاعات آن حفظ شده است/);
    fixture.setMode('cart-no-vat');
    await go();
    assert.equal(await root.getByTestId('cart-vat-row').count(), 0);
    assert.match(await payable(), /۱,۳۰۰,۰۰۰/);
    await page.evaluate(() => window.vzTheme.set('dark'));
    assert.equal(await root.locator('.st-cart-sum').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(32, 39, 41)');
    await page.screenshot({ path: resolve(output, 'cart-dark.png'), fullPage: true });
    await page.evaluate(() => window.vzTheme.set('light'));
    await root.getByRole('button', { name: 'ادامه و پرداخت امن', exact: true }).click();
    await page.waitForURL('**/login?returnUrl=%2Fcheckout');
    report.checks.push('Empty/error/merge-failure views, no-VAT response, dark theme and guest checkout login route');
    console.log(JSON.stringify({ cart: report }, null, 2));
    return report;
}
