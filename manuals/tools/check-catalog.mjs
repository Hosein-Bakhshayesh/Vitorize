// UI + API request contract checks. Only runs against check-home-reference's isolated fixture.
import assert from 'node:assert/strict';
import { resolve } from 'node:path';

export async function checkCatalog(page, output, fixture) {
    const base = 'http://127.0.0.1:5088';
    const report = { viewports: [], checks: [] };
    const catalog = page.locator('.catalog-page');
    const ready = async () => {
        await page.waitForFunction(() => {
            const results = document.querySelector('.catalog-results');
            return results && !results.querySelector('.catalog-skeleton');
        });
        await page.evaluate(() => document.fonts.ready);
        assert.equal(await page.locator('.catalog-results').getAttribute('aria-busy'), 'false');
    };
    const go = async path => { await page.goto(base + path, { waitUntil: 'networkidle' }); };
    const latestQuery = () => new URL(fixture.calls.filter(p => p.startsWith('/api/products?') && p.includes('pageSize=24')).at(-1), base).searchParams;
    const requestAfter = async action => {
        const count = fixture.calls.length;
        await action();
        for (let i = 0; i < 100 && !fixture.calls.slice(count).some(p => p.startsWith('/api/products?')); i++) await page.waitForTimeout(50);
        assert(fixture.calls.slice(count).some(p => p.startsWith('/api/products?')), 'Expected server-side listing request');
        await ready();
        return latestQuery();
    };
    for (const width of [1728, 1280, 1024, 960, 900, 768, 402, 360]) {
        await page.setViewportSize({ width, height: 1000 });
        await go('/shop');
        await ready();
        assert.equal(await catalog.locator('.st-pcard').count(), 24);
        const geometry = await page.evaluate(() => ({
            width: innerWidth,
            overflow: document.documentElement.scrollWidth > innerWidth,
            columns: getComputedStyle(document.querySelector('.st-lgrid')).gridTemplateColumns.split(' ').length,
            sidebarVisible: !!document.querySelector('.st-fsidebar').getClientRects().length,
            filterVisible: !!document.querySelector('#st-filter-trigger').getClientRects().length,
        }));
        assert.equal(geometry.overflow, false, `overflow at ${width}`);
        assert.equal(geometry.columns, width >= 1600 ? 4 : width > 1100 ? 3 : 2);
        assert.equal(geometry.sidebarVisible, width > 900);
        assert.equal(geometry.filterVisible, width <= 900);
        assert.equal(latestQuery().has('sort'), false, 'Preserve administrator default sort');
        report.viewports.push(geometry);
        if ([1728, 402].includes(width)) {
            await page.screenshot({ path: resolve(output, `catalog-${width}.png`), fullPage: true });
            await page.screenshot({ path: resolve(output, `catalog-${width}-viewport.png`) });
        }
    }
    await page.setViewportSize({ width: 1280, height: 1000 });
    await go('/shop');
    await ready();
    assert.equal(await catalog.locator('.st-pcard').first().getByTestId('product-oos-badge').count(), 1);
    assert.equal(await catalog.locator('.st-pcard').first().getByTitle('ناموجود', { exact: true }).isDisabled(), true);
    assert.equal(await catalog.locator('.st-pcard__media').nth(1).getAttribute('href'), '/product/redirect-target');
    let q = await requestAfter(() => catalog.getByRole('button', { name: 'صفحه بعد', exact: true }).click());
    assert.equal(q.get('page'), '2');
    assert.equal(await catalog.locator('.st-pager [aria-current="page"]').textContent(), '۲');
    assert.equal(await catalog.locator('.st-pcard__title').first().textContent(), 'اشتراک دیجیتال 25');
    q = await requestAfter(() => catalog.getByLabel('مرتب‌سازی محصولات').selectOption('oldest'));
    assert.equal(q.get('sort'), 'oldest');
    assert.equal(q.get('page'), '1');
    assert.equal(await catalog.locator('.st-pcard__title').first().textContent(), 'اشتراک دیجیتال 51');
    const side = catalog.locator('.st-fsidebar');
    q = await requestAfter(() => side.getByRole('button', { name: 'فقط کالاهای موجود', exact: true }).click());
    assert.equal(q.get('inStock'), 'true');
    q = await requestAfter(() => side.getByRole('button', { name: 'تحویل آنی', exact: true }).click());
    assert.equal(q.get('deliveryType'), '1');
    q = await requestAfter(() => side.getByRole('button', { name: '۲۵٪+', exact: true }).click());
    assert.equal(q.get('minDiscountPercent'), '25');
    assert.equal(q.get('hasDiscount'), 'true');
    await side.getByRole('button', { name: /^نوع محصول/ }).click();
    q = await requestAfter(() => side.getByRole('button', { name: 'اشتراک', exact: true }).click());
    assert.deepEqual(q.getAll('productTypes'), ['4']);
    q = await requestAfter(async () => { await side.getByLabel('حداقل قیمت به تومان').fill('1200.5'); await side.getByLabel('حداقل قیمت به تومان').press('Tab'); });
    assert.equal(q.get('minPrice'), '1200.5');
    q = await requestAfter(() => side.getByRole('button', { name: 'Claude', exact: true }).click());
    assert(q.get('brandId')?.endsWith('40'));
    q = await requestAfter(() => catalog.locator('.st-catpill').nth(2).click());
    assert(q.get('categoryId')?.endsWith('02'));
    assert.equal((await catalog.locator('.st-catpill').nth(2).getAttribute('aria-pressed')).toLowerCase(), 'true');
    q = await requestAfter(() => side.getByRole('button', { name: 'پاک کردن همه', exact: true }).click());
    for (const key of ['inStock', 'deliveryType', 'productTypes', 'minPrice', 'minDiscountPercent', 'brandId', 'categoryId']) assert.equal(q.has(key), false, key);
    report.checks.push('Server-side pagination, explicit/default sorting, category/brand/type/stock/delivery/discount/price filters and clearing; stock and redirect links preserved');

    await go('/category/category-0?sort=cheapest'); await ready();
    assert.equal(await catalog.locator('h1').textContent(), 'محصولات پریمیوم');
    assert(latestQuery().get('categoryId')?.endsWith('01'));
    assert.equal(latestQuery().get('sort'), 'cheapest');
    await go('/brand/brand-0'); await ready();
    assert.equal(await catalog.locator('h1').textContent(), 'Claude');
    assert(latestQuery().get('brandId')?.endsWith('40'));
    await go('/search');
    await catalog.getByLabel('عبارت جستجو').fill('اشتراک تست');
    await catalog.getByRole('button', { name: 'جستجو', exact: true }).click();
    await ready();
    assert.equal(latestQuery().get('search'), 'اشتراک تست');
    await go('/categories');
    assert.equal(await catalog.locator('.st-category-group').count(), 6);
    assert.equal(await catalog.locator('.st-cat[href="/category/child-category"]').count(), 1);
    await page.screenshot({ path: resolve(output, 'catalog-categories.png'), fullPage: true });
    report.checks.push('Category/brand/search routes and category hierarchy');

    await page.setViewportSize({ width: 402, height: 874 });
    await go('/shop'); await ready();
    await catalog.locator('#st-filter-trigger').click();
    const dialog = catalog.getByRole('dialog');
    await dialog.waitFor();
    await page.waitForFunction(() => document.activeElement?.id === 'st-filter-sheet-close');
    assert.equal(await page.locator('html').evaluate(el => el.classList.contains('st-filter-scroll-locked')), true);
    assert.equal(await page.locator('.st-header').evaluate(el => !!el.closest('[inert]')), true);
    await dialog.locator('button').last().focus();
    await page.keyboard.press('Tab');
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'st-filter-sheet-close');
    await page.keyboard.press('Shift+Tab');
    assert.equal(await dialog.locator('button').last().evaluate(el => el === document.activeElement), true);
    q = await requestAfter(() => dialog.getByRole('button', { name: 'فقط کالاهای موجود', exact: true }).click());
    assert.equal(q.get('inStock'), 'true');
    await page.screenshot({ path: resolve(output, 'catalog-filter-mobile.png'), fullPage: true });
    await page.keyboard.press('Escape');
    await dialog.waitFor({ state: 'detached' });
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'st-filter-trigger');
    assert.equal(await page.locator('[inert]').count(), 0);
    assert.equal(await page.locator('html').evaluate(el => el.classList.contains('st-filter-scroll-locked')), false);
    report.checks.push('Mobile dialog: filtering, scroll lock, inert background, Tab/Shift+Tab trap, Escape and focus restoration');

    fixture.setMode('catalog-failure');
    await go('/shop');
    await catalog.getByRole('alert').waitFor();
    assert.equal(await catalog.locator('.st-pcard').count(), 0);
    fixture.setMode('catalog');
    await catalog.getByRole('button', { name: 'تلاش دوباره', exact: true }).click();
    await ready();
    assert.equal(await catalog.locator('.st-pcard').count(), 24);
    fixture.setMode('catalog-empty');
    await go('/shop'); await ready();
    assert.equal(await catalog.locator('.st-pcard').count(), 0);
    assert.equal(await catalog.getByRole('alert').count(), 0);
    assert.equal(await catalog.getByText('محصولی با این فیلترها یافت نشد', { exact: true }).count(), 1);
    fixture.setMode('catalog-categories-failure');
    await go('/categories');
    await catalog.getByRole('alert').waitFor();
    fixture.setMode('catalog');
    await catalog.getByRole('button', { name: 'تلاش دوباره', exact: true }).click();
    await catalog.locator('.st-category-group').first().waitFor();
    report.checks.push('Separate API failure/empty UI and retries for products and categories');
    await go('/shop'); await ready();
    await page.evaluate(() => window.vzTheme.set('dark'));
    assert.equal(await catalog.locator('.st-pcard').first().evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(32, 39, 41)');
    await page.screenshot({ path: resolve(output, 'catalog-dark.png'), fullPage: true });
    await page.evaluate(() => window.vzTheme.set('light'));
    report.checks.push('Dark theme scoped tokens');
    console.log(JSON.stringify({ catalog: report }, null, 2));
    return report;
}
