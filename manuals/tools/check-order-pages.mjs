import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { orderGuid } from './order-pages-fixture.mjs';

export async function checkOrderPages(page, output, fixture) {
    const base = 'http://127.0.0.1:5088';
    const listPath = '/customer/orders', detailPath = n => `${listPath}/${orderGuid(10000 + n)}`;
    const list = page.locator('.order-list-page'), detail = page.locator('.order-detail-page');
    const dialog = page.locator('.customer-orders-page [role=dialog]');
    const go = async path => { await page.goto(base + path, { waitUntil: 'networkidle' }); await page.evaluate(() => document.fonts.ready); };
    const report = { viewports: [], checks: [] };
    await page.context().clearCookies(); await go(listPath); await page.waitForURL('**/login?**');
    await page.locator('#pw-mobile').fill('09000000000'); await page.locator('#pw-pass').fill('Fixture-only-123!');
    await page.locator('form[action="/auth/customer/login"] button[type=submit]').click(); await page.waitForURL('**/customer/orders');
    for (const width of [1728, 1280, 1100, 1024, 900, 768, 402, 320]) {
        await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 });
        for (const path of [listPath, detailPath(3)]) {
            await go(path);
            const geometry = await page.evaluate(() => ({ width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth }));
            assert.equal(geometry.overflow, false, `${path} at ${width}`);
            if (path === listPath) {
                const tableVisible = await list.locator('.st-mobile-records-desktop').isVisible();
                assert.equal(tableVisible, width > 900);
                assert.equal(await list.locator('[data-testid=order-row]:visible').count(), 4);
                assert.match(await list.locator('[data-testid=order-row]:visible').first().textContent(), /VZ-TEST-4/);
            } else {
                await detail.locator('.order-summary').waitFor();
                assert.match(await detail.locator('.st-sumrow.total').textContent(), /۱۹۷,۶۵۴/);
                assert.match(await detail.getByTestId('order-vat-row').textContent(), /۱۷,۶۵۴/);
                assert.equal(await detail.getByTestId('order-state').getAttribute('data-order-state'), 'Delivered');
                assert.equal(await detail.getByText('HIDDEN-CONTENT-MUST-NOT-RENDER').count(), 0);
                assert.equal(await detail.getByText('********', { exact: true }).count(), 1);
                assert.equal(await detail.locator('.order-detail-layout').evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length), width > 1024 ? 2 : 1);
            }
            if ([1728, 402].includes(width)) await page.screenshot({ path: resolve(output, `${path === listPath ? 'orders-list' : 'order-detail'}-${width}.png`), fullPage: true });
            report.viewports.push({ path, ...geometry });
        }
    }
    await page.setViewportSize({ width: 402, height: 874 });
    await go(detailPath(1)); assert.equal(await detail.getByTestId('order-state').getAttribute('data-order-state'), 'AwaitingPayment');
    assert.match(await detail.getByTestId('item-delivery-notice').textContent(), /پس از پرداخت/);
    await go(detailPath(4)); assert.equal(await detail.getByTestId('order-state').getAttribute('data-order-state'), 'AwaitingKyc');
    assert.equal(await detail.getByRole('link', { name: 'تکمیل احراز هویت' }).getAttribute('href'), '/customer/verification?orderItem=' + orderGuid(11004));
    assert.equal(await detail.getByRole('link', { name: 'ثبت تیکت برای این سفارش' }).getAttribute('href'), '/customer/tickets/new?orderId=' + orderGuid(10004));
    await page.screenshot({ path: resolve(output, 'order-kyc-402.png'), fullPage: true });
    fixture.setMode('order-pages-blocked'); await go(detailPath(1));
    assert.equal(await detail.getByTestId('order-cancel').count(), 0);
    assert.equal(await detail.getByRole('button', { name: 'تلاش مجدد پرداخت' }).count(), 0);
    await detail.getByTestId('order-cancel-blocked').waitFor();
    fixture.setMode('order-pages-no-vat'); await go(detailPath(3)); assert.equal(await detail.getByTestId('order-vat-row').count(), 0);
    fixture.setMode('order-pages');
    await page.evaluate(() => { Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async text => { window.__copiedOrderFixture = text; } } }); });
    await detail.getByRole('button', { name: 'کپی محتوای تحویل‌شده' }).click();
    await page.waitForFunction(() => window.__copiedOrderFixture?.startsWith('FIXTURE-ONLY-CODE'));
    report.checks.push('Canonical pending/delivered/KYC state, payment-gated fulfillment text, server VAT/totals, masked inputs, hidden delivery exclusion, copy stub and original KYC/ticket links');

    const openConfirm = async root => { await root.getByTestId('order-cancel').filter({ visible: true }).first().click(); await dialog.waitFor(); };
    await go(listPath); await openConfirm(list);
    const beforeCancel = fixture.writes.length;
    await dialog.locator('button').first().focus(); await page.keyboard.press('Shift+Tab');
    assert.equal(await dialog.locator('button').last().evaluate(el => el === document.activeElement), true);
    await page.keyboard.press('Tab'); assert.equal(await dialog.locator('button').first().evaluate(el => el === document.activeElement), true);
    assert.equal(await page.evaluate(() => document.body.style.overflow), 'hidden');
    assert.equal(await page.locator('.customer-shell .st-header').evaluate(el => el.inert), true);
    await page.screenshot({ path: resolve(output, 'order-confirm-402.png'), fullPage: true });
    await page.keyboard.press('Escape'); await dialog.waitFor({ state: 'detached' });
    assert.equal(fixture.writes.length, beforeCancel);
    assert.equal(await page.locator('.customer-shell .st-header').evaluate(el => el.inert), false);
    await openConfirm(list); await dialog.getByRole('button', { name: 'لغو سفارش', exact: true }).click();
    await dialog.waitFor({ state: 'detached' });
    await page.waitForFunction(() => !document.querySelector('.order-list-page [data-testid=order-cancel]'));
    assert.equal(fixture.writes.at(-1).path, `/api/orders/${orderGuid(10001)}/cancel`);
    await list.getByTestId('order-hide').filter({ visible: true }).first().click(); await dialog.waitFor();
    await dialog.getByRole('button', { name: 'حذف از لیست', exact: true }).click(); await dialog.waitFor({ state: 'detached' });
    await page.waitForFunction(() => document.querySelectorAll('.st-mobile-record[data-testid=order-row]').length === 3);
    report.checks.push('Cancel/hide confirmation, keyboard trap/Escape, inert background/scroll lock, no write on dismiss and exact fixture-only mutation endpoints');

    fixture.reset(); await go(detailPath(1)); fixture.setMode('order-pages-mutation-failure');
    await detail.getByTestId('order-cancel').click(); await dialog.waitFor(); await dialog.getByRole('button', { name: 'لغو سفارش', exact: true }).click();
    await dialog.waitFor({ state: 'detached' }); await page.getByText('عملیات آزمایشی پذیرفته نشد.', { exact: true }).waitFor();
    assert.equal(await detail.getByTestId('order-state').getAttribute('data-order-state'), 'AwaitingPayment');
    fixture.setMode('order-pages');
    await detail.getByTestId('order-cancel').click(); await dialog.waitFor(); await dialog.getByRole('button', { name: 'لغو سفارش', exact: true }).click(); await dialog.waitFor({ state: 'detached' });
    await page.waitForFunction(() => document.querySelector('[data-testid=order-state]')?.dataset.orderState === 'Cancelled');
    await detail.getByTestId('order-hide').click(); await dialog.waitFor(); await dialog.getByRole('button', { name: 'حذف از لیست', exact: true }).click(); await page.waitForURL('**/customer/orders');
    fixture.reset(); await go(detailPath(3));
    await detail.getByRole('button', { name: 'ثبت نظر برای این خرید' }).click(); await dialog.waitFor();
    const beforeReview = fixture.writes.length;
    await dialog.getByRole('button', { name: 'ثبت نظر', exact: true }).click();
    await dialog.getByRole('alert').filter({ hasText: 'متن نظر الزامی است.' }).waitFor(); assert.equal(fixture.writes.length, beforeReview);
    await dialog.getByRole('button', { name: '3 ستاره' }).focus(); await page.keyboard.press('Enter');
    await page.waitForFunction(() => document.querySelector('.order-review-stars button[aria-label="3 ستاره"]')?.getAttribute('aria-pressed') === 'true');
    await dialog.getByLabel('عنوان (اختیاری)').fill('عنوان صرفاً آزمایشی'); await dialog.getByLabel('نظر شما', { exact: true }).fill('متن صرفاً آزمایشی؛ منتشر نمی‌شود.');
    await dialog.getByLabel('نظر شما', { exact: true }).press('Tab');
    await dialog.locator('#order-review-error').waitFor({ state: 'detached' });
    await page.screenshot({ path: resolve(output, 'order-review-402.png'), fullPage: true });
    fixture.setMode('order-pages-mutation-failure');
    await dialog.getByRole('button', { name: 'ثبت نظر', exact: true }).click();
    await dialog.getByRole('alert').filter({ hasText: 'عملیات آزمایشی پذیرفته نشد.' }).waitFor();
    fixture.setMode('order-pages');
    await dialog.getByRole('button', { name: 'ثبت نظر', exact: true }).click(); await dialog.waitFor({ state: 'detached' });
    assert.equal(fixture.writes.at(-1).body.Rating, 3); assert.equal(fixture.writes.at(-1).body.ProductId, orderGuid(12003));
    await detail.getByRole('link', { name: 'مشاهده نظر' }).waitFor();
    fixture.reset(); await go(detailPath(1)); fixture.setMode('order-pages-mutation-failure');
    await detail.getByRole('button', { name: 'تلاش مجدد پرداخت' }).click(); await page.getByText('عملیات آزمایشی پذیرفته نشد.', { exact: true }).waitFor();
    assert.equal(await detail.getByRole('button', { name: 'تلاش مجدد پرداخت' }).count(), 0);
    fixture.setMode('order-pages'); await go(detailPath(1)); await detail.getByRole('button', { name: 'تلاش مجدد پرداخت' }).click(); await page.waitForURL('**/payment/result?**');
    fixture.setMode('order-pages-gateway'); await go(detailPath(1)); await detail.getByRole('button', { name: 'تلاش مجدد پرداخت' }).click(); await page.waitForURL('**/shop?fixture-gateway=orders');
    report.checks.push('Details cancel/hide/error, keyboard review rating/required content/payload, payment retry failure/mock success/loopback gateway');
    fixture.setMode('order-pages-empty'); await go(listPath); await list.getByText('هنوز سفارشی ثبت نکرده‌اید', { exact: true }).waitFor();
    fixture.setMode('order-pages-failure'); await go(listPath); await list.getByText('دریافت سفارش‌ها انجام نشد.', { exact: true }).waitFor();
    fixture.setMode('order-pages'); await list.getByRole('button', { name: 'تلاش مجدد' }).click(); await list.locator('[data-testid=order-row]').first().waitFor();
    fixture.setMode('order-pages-missing'); await go(detailPath(1)); await detail.getByText('سفارش یافت نشد', { exact: true }).waitFor();
    fixture.setMode('order-pages'); await go(detailPath(3)); await page.evaluate(() => document.documentElement.dataset.theme = 'dark');
    assert.equal(await detail.locator('.order-summary').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(32, 39, 41)');
    await page.screenshot({ path: resolve(output, 'order-dark.png'), fullPage: true });
    await page.evaluate(() => document.documentElement.dataset.theme = 'light'); await page.context().clearCookies();
    console.log(JSON.stringify({ orderPages: report }, null, 2)); return report;
}
