import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { productGuid } from './product-fixture.mjs';

export async function checkProduct(page, output, fixture) {
    const root = page.locator('.product-page');
    const go = async slug => {
        await page.goto('http://127.0.0.1:5088/product/' + slug, { waitUntil: 'networkidle' });
        await page.waitForFunction(() => document.querySelector('.product-page')?.getAttribute('aria-busy') === 'false');
        await page.evaluate(() => document.fonts.ready);
    };
    const report = { viewports: [], checks: [] };
    for (const width of [1728, 1280, 1024, 1000, 768, 402, 360, 320]) {
        await page.setViewportSize({ width, height: width > 767 ? 1000 : 874 });
        await go('standard');
        const geometry = await page.evaluate(() => {
            const bounds = s => { const r = document.querySelector(s).getBoundingClientRect(); return { top: r.top, bottom: r.bottom, width: r.width, height: r.height }; };
            return {
                width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth,
                gallery: bounds('.st-gal__main'), buy: bounds('.st-buy'), bar: bounds('.st-buybar'), nav: bounds('.st-bottomnav'),
                columns: getComputedStyle(document.querySelector('.product-related .st-lgrid')).gridTemplateColumns.split(' ').length,
            };
        });
        assert.equal(geometry.overflow, false, `overflow at ${width}`);
        assert(Math.abs(geometry.gallery.width - geometry.gallery.height) < 2, 'square gallery');
        assert.equal(geometry.bar.height > 0, width <= 1000);
        if (width <= 767) assert(Math.abs(geometry.bar.bottom - geometry.nav.top) < 2, 'buy bar clears bottom navigation');
        if (width <= 1000) assert(geometry.buy.top >= geometry.gallery.bottom, 'mobile gallery before buy box');
        assert.equal(await root.getByTestId('product-variant').count(), 3);
        assert.equal(await root.getByTestId('product-variant').first().isDisabled(), true);
        assert.equal(await root.getByTestId('product-variant').nth(1).getAttribute('aria-pressed'), 'true');
        assert.equal(await root.locator('.st-buy__now').textContent(), '۱۸۰,۰۰۰');
        assert.equal(await root.locator('.st-gal__badge').count(), 1, 'variant discount shown even without product-level discount');
        assert.equal(await root.getByText('FEATURE_MUST_STAY_HIDDEN').count(), 0);
        assert.equal(await root.locator('.st-feature-card').count(), 2);
        assert.equal(await root.locator('.st-gal__thumb').count(), 2, 'deduplicated gallery');
        if ([1728, 402].includes(width)) {
            await page.screenshot({ path: resolve(output, `product-${width}.png`), fullPage: true });
            await page.screenshot({ path: resolve(output, `product-${width}-viewport.png`) });
        }
        report.viewports.push(geometry);
    }
    await page.setViewportSize({ width: 1280, height: 1000 });
    await go('standard');
    await root.locator('.st-gal__thumb').nth(1).focus();
    await page.keyboard.press('Enter');
    await page.waitForFunction(() => document.querySelector('.st-gal__main img')?.src.includes('gallery=1'));
    assert.equal(await root.locator('.st-gal__main img').getAttribute('alt'), 'تصویر دوم محصول');
    await root.getByTestId('product-variant').nth(2).click();
    await page.waitForFunction(() => document.querySelector('.st-buy__now')?.textContent === '۳۰۰,۰۰۰');
    assert.equal(await root.locator('.st-gal__badge').count(), 0);
    assert.equal(await root.getByLabel('تعداد محصول', { exact: true }).inputValue(), '2');
    assert.equal(await root.getByLabel('کاهش تعداد', { exact: true }).isDisabled(), true);
    await root.getByLabel('افزایش تعداد', { exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.st-qty__input')?.value === '3');
    await root.getByLabel('افزایش تعداد', { exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.st-qty__input')?.value === '4');
    assert.equal(await root.getByLabel('تعداد محصول', { exact: true }).inputValue(), '4');
    assert.equal(await root.getByLabel('افزایش تعداد', { exact: true }).isDisabled(), true);
    const add = root.locator('.st-buy__card').getByRole('button', { name: 'افزودن به سبد خرید', exact: true });
    const waitWrite = async action => {
        const before = fixture.writes.length;
        await action();
        for (let i = 0; i < 100 && fixture.writes.length === before; i++) await page.waitForTimeout(50);
        assert.equal(fixture.writes.length, before + 1);
        return Object.fromEntries(Object.entries(fixture.writes.at(-1)).map(([key, value]) => [key.toLowerCase(), value]));
    };
    let write = await waitWrite(() => add.click());
    assert.deepEqual(write, { productid: productGuid(500), productvariantid: productGuid(503), quantity: 4 });
    fixture.setMode('product-cart-failure');
    await waitWrite(() => add.click());
    await page.getByText('خطای آزمایشی سبد خرید', { exact: true }).waitFor();
    assert.equal(await add.isEnabled(), true);
    fixture.setMode('product');
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write'], { origin: 'http://127.0.0.1:5088' });
    await root.getByRole('button', { name: 'کپی لینک محصول', exact: true }).click();
    await page.getByText('لینک محصول کپی شد.', { exact: true }).waitFor();
    assert.equal(await page.evaluate(() => navigator.clipboard.readText()), 'http://127.0.0.1:5088/product/standard');
    await root.getByTestId('product-faq-tab').click();
    await root.getByTestId('product-faq-list').getByRole('button').click();
    await root.getByTestId('faq-answer').waitFor();
    assert.equal(await root.getByTestId('faq-answer').textContent(), 'پاسخ آزمایشی اختصاصی این محصول.');
    await root.locator('.st-ptabs').getByRole('button', { name: /^نظرات/ }).click();
    await root.getByText('نظر تأییدشده آزمایشی برای بررسی صفحه محصول.', { exact: true }).waitFor();
    assert.equal(await root.getByRole('link', { name: 'برای ثبت نظر وارد شوید', exact: true }).count(), 1);
    await page.setViewportSize({ width: 360, height: 874 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'reviews mobile overflow');
    await page.screenshot({ path: resolve(output, 'product-reviews-mobile.png'), fullPage: true });
    report.checks.push('Keyboard gallery, real image/alt, default and selected SKU prices, quantity limits, isolated cart payload/error recovery, product FAQ and public reviews');

    await go('many');
    assert.equal(await root.getByTestId('product-variant').count(), 12);
    assert.equal(await root.locator('.st-vcards--bounded').evaluate(el => el.scrollHeight > el.clientHeight), true);
    await root.getByTestId('product-variant').last().click();
    await page.waitForFunction(() => document.querySelector('.st-vsummary')?.textContent.includes('نسخه 12'));
    assert.match(await root.getByTestId('product-variant-summary').textContent(), /نسخه 12/);
    write = await waitWrite(() => root.getByTestId('sticky-add-to-cart').click());
    assert.equal(write.productvariantid, productGuid(512));
    assert.equal(write.quantity, 2);
    await go('standard?variant=' + productGuid(503));
    assert.equal(await root.getByTestId('product-variant').nth(2).getAttribute('aria-pressed'), 'true');
    await go('standard?variant=' + productGuid(501));
    assert.equal(await root.getByTestId('sticky-add-to-cart').isDisabled(), true);
    await go('out');
    assert.equal(await root.getByTestId('product-oos-badge').count(), 1);
    assert.equal(await root.getByTestId('sticky-add-to-cart').isDisabled(), true);
    assert.equal(await root.getByTestId('sticky-buy-now').isDisabled(), true);
    assert.equal(await root.getByTestId('product-variant').evaluateAll(els => els.every(el => el.disabled)), true);
    await go('implicit');
    assert.equal(await root.getByTestId('product-variant').count(), 0);
    await go('single');
    assert.equal(await root.getByTestId('product-variant').count(), 0);
    write = await waitWrite(() => root.getByTestId('sticky-add-to-cart').click());
    assert.equal(write.productvariantid, null);
    await go('rial');
    assert.equal(await root.locator('.st-buy__unit').textContent(), 'ریال');
    await page.evaluate(() => window.vzTheme.set('dark'));
    assert.equal(await root.locator('.st-buy__card').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(32, 39, 41)');
    await page.screenshot({ path: resolve(output, 'product-dark.png'), fullPage: true });
    await page.evaluate(() => window.vzTheme.set('light'));
    await go('missing');
    await root.getByText('محصول یافت نشد', { exact: true }).waitFor();
    assert.equal(await root.getByTestId('product-sticky-buy').count(), 0);
    await page.goto('http://127.0.0.1:5088/product/redirect', { waitUntil: 'networkidle' });
    await page.waitForURL('**/shop');
    await go('standard');
    await waitWrite(() => root.getByTestId('sticky-buy-now').click());
    await page.waitForURL(/\/(checkout|login)/);
    report.checks.push('Many/implicit/no variants, SKU deep links, sold-out purchase gates, currency, dark theme, missing and redirected products, buy-now navigation');
    console.log(JSON.stringify({ product: report }, null, 2));
    return report;
}
