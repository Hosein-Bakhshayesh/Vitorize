import assert from 'node:assert/strict';
import { resolve } from 'node:path';

export async function checkWalletPage(page, output, fixture) {
    const base = 'http://127.0.0.1:5088';
    const root = page.locator('.customer-wallet-page');
    const amount = root.locator('#wallet-amount');
    const submit = root.getByRole('button', { name: 'پرداخت و شارژ', exact: true });
    const feedback = root.locator('#wallet-feedback');
    const ready = () => page.waitForFunction(() => document.querySelector('.customer-wallet-page')?.getAttribute('aria-busy') === 'false');
    const go = async (query = '') => { await page.goto(base + '/customer/wallet' + query, { waitUntil: 'networkidle' }); await page.evaluate(() => document.fonts.ready); };
    const mode = async value => { fixture.setMode(value); await go(); await ready(); };
    const balance = () => root.getByTestId('wallet-balance').textContent();
    const noOverflow = async () => assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
    const report = { viewports: [], checks: [] };
    await page.context().clearCookies(); await go(); await page.waitForURL('**/login?**');
    assert.equal(await root.count(), 0);
    await page.locator('#pw-mobile').fill('09000000000');
    await page.locator('#pw-pass').fill('Fixture-only-123!');
    await page.locator('form[action="/auth/customer/login"] button[type=submit]').click();
    await page.waitForURL('**/customer/wallet'); await ready();
    for (const width of [1728, 1280, 1100, 1024, 900, 768, 402, 360, 320]) {
        await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 }); await go(); await ready();
        await noOverflow();
        assert.match(await balance(), /۹,۸۷۶,۵۴۳/);
        const rows = root.locator('[data-testid="wallet-transaction"]:visible');
        assert.equal(await rows.count(), 3);
        assert.match(await rows.first().textContent(), /پرداخت سفارش آزمایشی جدید/);
        assert.match(await rows.first().textContent(), /۳۰۱,۲۳۴/);
        assert.match(await rows.first().textContent(), /۹,۵۷۵,۳۰۹/);
        assert.match(await rows.nth(1).locator('.is-credit').textContent(), /\+۲۰۰,۱۲۳/);
        const geometry = await root.evaluate(el => ({ width: innerWidth, columns: getComputedStyle(el.querySelector('.wallet-overview')).gridTemplateColumns.split(' ').length, table: getComputedStyle(el.querySelector('.st-mobile-records-desktop')).display !== 'none' }));
        assert.equal(geometry.columns, width > 900 ? 2 : 1);
        assert.equal(geometry.table, width > 900);
        report.viewports.push(geometry);
        if ([1728, 402].includes(width)) await page.screenshot({ path: resolve(output, `wallet-${width}.png`), fullPage: true });
    }
    await page.setViewportSize({ width: 402, height: 874 });
    await mode('wallet-page-long'); await noOverflow();
    await mode('wallet-page-empty');
    assert.match(await balance(), /۰/);
    await root.getByText('تراکنشی ثبت نشده است.', { exact: true }).waitFor();
    for (const value of ['wallet-page-failure', 'wallet-page-null', 'wallet-page-balance-failure', 'wallet-page-tx-failure']) {
        await mode(value);
        assert.equal(await root.getByTestId('wallet-balance').count(), value === 'wallet-page-tx-failure' ? 1 : 0);
        assert.equal(await root.getByText('دریافت تراکنش‌ها انجام نشد.', { exact: true }).count(), value === 'wallet-page-balance-failure' ? 0 : 1);
        assert.equal(await root.getByText('تراکنشی ثبت نشده است.', { exact: true }).count(), 0);
    }
    fixture.setMode('wallet-page-slow'); await root.getByRole('button', { name: 'تلاش مجدد' }).click();
    await root.getByText('در حال دریافت اطلاعات کیف پول…', { exact: true }).waitFor(); await ready();
    assert.match(await balance(), /۹,۸۷۶,۵۴۳/);
    await mode('wallet-page');
    assert.equal(await amount.inputValue(), '500000');
    const presets = root.locator('.wallet-presets button');
    for (const [index, value] of [200000, 500000, 1000000, 2000000].entries()) {
        await presets.nth(index).click();
        await page.waitForFunction(value => document.querySelector('#wallet-amount')?.value === String(value), value);
        assert.equal(await presets.nth(index).getAttribute('aria-pressed'), 'true');
    }
    for (const value of ['0', '-1']) {
        await amount.fill(value); await submit.click();
        await feedback.getByText('مبلغ شارژ را وارد کنید.', { exact: true }).waitFor();
        assert.equal(fixture.writes.length, 0);
    }
    report.checks.push('Guest protection; nine widths; server balance/credit/debit amounts and newest-first transactions; long/empty/independent failed/null reads, retry/loading; unchanged presets and nonpositive validation');
    for (const value of ['wallet-page-start-failure', 'wallet-page-verify-failure', 'wallet-page-unpaid']) {
        fixture.reset(); await mode(value); await submit.click();
        await feedback.locator('xpath=self::*[@role="alert"]').waitFor(); await ready();
        assert.equal(fixture.writes.length, value === 'wallet-page-start-failure' ? 1 : 2);
        assert.match(await balance(), /۹,۸۷۶,۵۴۳/);
        assert.equal(await submit.isEnabled(), true);
    }
    fixture.reset(); await mode('wallet-page-busy');
    await amount.fill('123456'); await amount.press('Enter');
    await page.waitForFunction(() => document.querySelector('#wallet-amount')?.disabled);
    for (let i = 0; i < 4; i++) assert.equal(await presets.nth(i).isDisabled(), true);
    assert.equal(await submit.isDisabled(), true);
    await page.waitForFunction(() => document.querySelector('[data-testid="wallet-balance"]')?.textContent.includes('۱۲,۳۴۵,۶۷۸'));
    await ready(); assert.match(await feedback.textContent(), /۱۲۳,۴۵۶/);
    assert.deepEqual(fixture.writes, [
        { path: '/api/wallet/topup', body: { Amount: 123456 } },
        { path: '/api/wallet/topup/mock/verify/00000000-0000-0000-0000-000000021001', body: null },
    ]);
    await page.screenshot({ path: resolve(output, 'wallet-success-402.png'), fullPage: true });
    fixture.reset(); await mode('wallet-page-gateway'); await submit.click();
    await page.waitForURL('**/shop?fixture-gateway=wallet'); assert.equal(fixture.writes.length, 1);
    fixture.reset(); fixture.setMode('wallet-page');
    for (const outcome of ['1', '0']) {
        await go('?topup=' + outcome); await ready();
        assert.equal(await feedback.getAttribute('role'), outcome === '0' ? 'alert' : 'status');
        assert.match(await balance(), /۹,۸۷۶,۵۴۳/); assert.equal(fixture.writes.length, 0);
    }
    report.checks.push('Unchanged Amount payload, Enter submission, disabled busy controls, rejected start/verify/unpaid, mock success uses fresh server balance, loopback gateway navigation without mock verify, callback messages without balance mutation');
    await go(); await ready(); await page.evaluate(() => document.documentElement.dataset.theme = 'dark');
    assert.equal(await root.locator('.wallet-topup').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(32, 39, 41)');
    await page.waitForFunction(() => getComputedStyle(document.querySelector('[data-testid="wallet-balance"]')).color === 'rgb(238, 245, 243)');
    await noOverflow(); await page.screenshot({ path: resolve(output, 'wallet-dark-402.png'), fullPage: true });
    await page.evaluate(() => document.documentElement.dataset.theme = 'light');
    console.log(JSON.stringify({ walletPage: report }, null, 2));
    return report;
}
