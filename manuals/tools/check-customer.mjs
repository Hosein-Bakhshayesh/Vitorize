import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { customerGuid } from './customer-fixture.mjs';

export async function checkCustomer(page, output, fixture) {
    const base = 'http://127.0.0.1:5088';
    const root = page.locator('.customer-dashboard');
    const sidebar = page.locator('.customer-shell .st-acc__side');
    const nav = sidebar.locator('nav');
    const toggle = sidebar.locator('.st-acc__mobile-toggle');
    const expanded = value => page.waitForFunction(value => document.querySelector('.st-acc__mobile-toggle')?.getAttribute('aria-expanded') === value, String(value));
    const go = async (path = '/customer/dashboard') => { await page.goto(base + path, { waitUntil: 'networkidle' }); await page.evaluate(() => document.fonts.ready); };
    const ready = () => page.waitForFunction(() => document.querySelector('.customer-dashboard')?.getAttribute('aria-busy') === 'false');
    const report = { viewports: [], checks: [] };
    await page.context().clearCookies();
    await go(); await page.waitForURL('**/login?**');
    assert.equal(await root.count(), 0, 'guest cannot view dashboard');
    await page.locator('#pw-mobile').fill('09000000000');
    await page.locator('#pw-pass').fill('Fixture-only-123!');
    await page.locator('form[action="/auth/customer/login"] button[type=submit]').click();
    await page.waitForURL('**/customer/dashboard'); await ready();
    assert.match(await sidebar.locator('.customer-name').textContent(), /مشتری آزمایشی/);
    assert.equal(await nav.locator('a').count(), 10);
    assert.equal(await nav.locator('a.active').getAttribute('href'), '/customer/dashboard');
    for (const width of [1728, 1280, 1100, 1024, 768, 767, 402, 360, 320]) {
        await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 });
        await go(); await ready();
        const geometry = await page.evaluate(() => ({ width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth,
            columns: getComputedStyle(document.querySelector('.customer-layout')).gridTemplateColumns.split(' ').length,
            stats: getComputedStyle(document.querySelector('.dashboard-stats')).gridTemplateColumns.split(' ').length,
            navVisible: getComputedStyle(document.querySelector('#customer-account-nav')).display !== 'none',
            tableVisible: getComputedStyle(document.querySelector('.st-mobile-records-desktop')).display !== 'none',
        }));
        assert.equal(geometry.overflow, false, `overflow at ${width}`);
        assert.equal(geometry.columns, width > 1100 ? 2 : 1);
        assert.equal(geometry.stats, width > 1280 ? 4 : 2);
        assert.equal(geometry.navVisible, width > 1100);
        assert.equal(geometry.tableVisible, width > 767);
        assert.match(await root.getByTestId('dashboard-wallet').textContent(), /۹,۸۷۶,۵۴۳/);
        assert.equal((await root.getByTestId('dashboard-orders').locator('.st-stat__v').textContent()).trim(), '۸');
        assert.equal((await root.getByTestId('dashboard-tickets').locator('.st-stat__v').textContent()).trim(), '۳');
        assert.match(await root.getByTestId('dashboard-verification').textContent(), /تأیید شده/);
        const records = width > 767 ? root.locator('tbody tr') : root.locator('.st-mobile-record');
        assert.equal(await records.count(), 6);
        assert.match(await records.first().textContent(), /VZ-2026-000008/);
        assert.equal(await records.first().locator('a').getAttribute('href'), '/customer/orders/' + customerGuid(9507));
        assert.match(await records.first().locator('a').getAttribute('aria-label'), /000008/);
        if ([1728, 402].includes(width)) {
            await page.screenshot({ path: resolve(output, `customer-dashboard-${width}.png`), fullPage: true });
            await page.screenshot({ path: resolve(output, `customer-dashboard-${width}-viewport.png`) });
        }
        report.viewports.push(geometry);
    }
    await page.setViewportSize({ width: 402, height: 874 }); await go(); await ready();
    await toggle.focus(); await page.keyboard.press('Enter');
    await expanded(true);
    assert.equal(await toggle.getAttribute('aria-expanded'), 'true');
    await nav.locator('a').last().focus(); await page.keyboard.press('Escape');
    await expanded(false);
    assert.equal(await toggle.getAttribute('aria-expanded'), 'false');
    assert.equal(await toggle.evaluate(el => el === document.activeElement), true);
    await toggle.click(); await expanded(true); await page.screenshot({ path: resolve(output, 'customer-menu-402.png'), fullPage: true });
    await nav.locator('a[href="/customer/wallet"]').click(); await page.waitForURL('**/customer/wallet');
    await expanded(false);
    assert.equal(await toggle.getAttribute('aria-expanded'), 'false', 'menu closes after customer navigation');
    await toggle.click(); await expanded(true);
    assert.equal(await nav.locator('a.active').getAttribute('href'), '/customer/wallet');
    await nav.locator('a[href="/customer/dashboard"]').click(); await ready();
    await page.setViewportSize({ width: 1280, height: 600 });
    const logout = sidebar.locator('button[type=submit]'); await logout.scrollIntoViewIfNeeded();
    const logoutBounds = await logout.boundingBox();
    assert(logoutBounds.y >= 0 && logoutBounds.y + logoutBounds.height <= 600, 'short desktop sidebar logout reachable');
    report.checks.push('Guest protection, synthetic authenticated identity, account navigation/active links, keyboard disclosure/Escape focus, close on navigation and short-desktop sidebar');

    await page.setViewportSize({ width: 402, height: 874 });
    fixture.setMode('customer-long'); await go(); await ready();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'long name and balance');
    fixture.setMode('customer-empty'); await go(); await ready();
    await root.getByText('هنوز سفارشی ثبت نکرده‌اید.', { exact: true }).waitFor();
    assert.equal(await root.locator('.dashboard-error').count(), 0);
    assert.match(await root.getByTestId('dashboard-wallet').textContent(), /۰/);
    await page.screenshot({ path: resolve(output, 'customer-empty.png'), fullPage: true });
    for (const mode of ['customer-failure', 'customer-null', 'customer-wallet-failure']) {
        fixture.setMode(mode); await go(); await ready();
        await root.locator('.dashboard-error').waitFor();
        assert.match(await root.getByTestId('dashboard-wallet').textContent(), /در دسترس نیست/);
        assert.equal(await root.getByText('هنوز سفارشی ثبت نکرده‌اید.', { exact: true }).count(), 0, 'failed response is not empty success');
        if (mode === 'customer-wallet-failure') assert.equal(await root.locator('.st-mobile-record').count(), 6);
        else {
            assert.match(await root.getByTestId('dashboard-orders').textContent(), /در دسترس نیست/);
            assert.match(await root.getByTestId('dashboard-tickets').textContent(), /در دسترس نیست/);
            assert.match(await root.getByTestId('dashboard-verification').textContent(), /در دسترس نیست/);
            await root.getByText('دریافت سفارش‌ها انجام نشد.', { exact: true }).waitFor();
        }
    }
    await page.screenshot({ path: resolve(output, 'customer-partial-error.png'), fullPage: true });
    fixture.setMode('customer-slow');
    await root.getByRole('button', { name: 'تلاش مجدد' }).click();
    await root.getByText('در حال دریافت اطلاعات حساب…', { exact: true }).waitFor();
    assert.equal(await root.getAttribute('aria-busy'), 'true');
    await ready(); assert.equal(await root.locator('.dashboard-error').count(), 0);
    assert.match(await root.getByTestId('dashboard-wallet').textContent(), /۹,۸۷۶,۵۴۳/);
    fixture.setMode('customer');
    report.checks.push('Server balance, total order count, six latest sorted orders, non-closed ticket count and supplied verification status; empty/partial/full/null failures and retry/loading');
    await page.evaluate(() => document.documentElement.dataset.theme = 'dark');
    assert.equal(await sidebar.evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(32, 39, 41)');
    await page.screenshot({ path: resolve(output, 'customer-dark.png'), fullPage: true });
    await page.evaluate(() => document.documentElement.dataset.theme = 'light');
    await go('/admin/login'); assert.equal(await page.locator('.customer-shell, .site-shell').count(), 0);
    await go(); await ready(); await toggle.click(); await expanded(true); await logout.click();
    await page.waitForURL(base + '/');
    assert(!(await page.context().cookies()).some(c => c.name === 'Vitorize.Customer.Auth'));
    assert.equal(await page.locator('.customer-shell').count(), 0);
    report.checks.push('Scoped dark theme, admin exclusion and existing logout POST clears local customer session');
    console.log(JSON.stringify({ customer: report }, null, 2));
    return report;
}
