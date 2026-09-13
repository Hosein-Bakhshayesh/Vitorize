// Runs inside the isolated fixture harness: node manuals/tools/check-home-reference.mjs --shell
import assert from 'node:assert/strict';
import { resolve } from 'node:path';

export async function checkSiteShell(page, output) {
    const results = [];
    for (const width of [1728, 1280, 768, 402, 360]) {
        await page.setViewportSize({ width, height: 900 });
        await page.goto('http://127.0.0.1:5088/about', { waitUntil: 'networkidle' });
        await page.locator('.site-shell .st-header[data-shell-ready=true]').waitFor();
        const measure = await page.evaluate(() => {
            const selectors = ['.st-header .st-logo', '.sf-cart', '.sf-account', '.sf-search-toggle', '.st-mobile-menu-toggle'];
            const boxes = selectors.map(selector => {
                const el = document.querySelector(selector), rect = el.getBoundingClientRect();
                return { selector, x: rect.x, y: rect.y, width: rect.width, height: rect.height, visible: !!el.getClientRects().length };
            }).filter(x => x.visible);
            return { width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth, boxes };
        });
        assert.equal(measure.overflow, false, 'shell overflow at ' + width);
        for (let i = 0; i < measure.boxes.length; i++) {
            const a = measure.boxes[i];
            assert.ok(a.x >= 0 && a.x + a.width <= width, a.selector + ' outside viewport: ' + JSON.stringify(measure));
            for (const b of measure.boxes.slice(i + 1)) {
                assert.ok(a.x + a.width <= b.x || b.x + b.width <= a.x || a.y + a.height <= b.y || b.y + b.height <= a.y, a.selector + ' overlaps ' + b.selector);
            }
        }
        assert.equal(await page.locator('.st-socials a:visible').count(), 2);
        assert.equal(await page.locator('.st-socials svg:visible').count(), 2);
        assert.equal(await page.locator('.st-footer__quick-contact a:visible').count(), 2);
        assert.equal(await page.locator('.st-bottomnav:visible').count(), width < 768 ? 1 : 0);
        if (width === 1728 || width === 402) await page.screenshot({ path: resolve(output, 'shell-' + width + '.png'), fullPage: true });

        await page.locator('.sf-search-toggle').click();
        const search = page.locator('.st-mobile-search');
        await search.waitFor();
        await search.locator('input').fill('محصول & تست');
        if (width === 402) await page.screenshot({ path: resolve(output, 'shell-mobile-search.png'), fullPage: true });
        await page.keyboard.press('Escape');
        await search.waitFor({ state: 'detached' });
        await page.waitForFunction(() => document.activeElement?.matches('.sf-search-toggle'));
        await page.locator('.sf-search-toggle').click();
        await search.locator('input').fill('محصول & تست');
        await search.locator('button[type=submit]').click();
        await page.waitForURL(url => url.pathname === '/search' && url.searchParams.get('q') === 'محصول & تست');
        await search.waitFor({ state: 'detached' });
        if (width < 768) {
            await page.waitForFunction(() => document.querySelector('.st-bottomnav a.active')?.getAttribute('href') === '/shop');
            assert.equal(await page.locator('.st-bottomnav a.active').count(), 1);
        }
        await page.goto('http://127.0.0.1:5088/about', { waitUntil: 'networkidle' });
        await page.locator('.st-header[data-shell-ready=true]').waitFor();
        if (width >= 768) {
            await page.locator('[data-testid=catmenu-trigger]').click();
            await page.locator('.st-catmenu__side').waitFor();
            await page.locator('.st-catmenu__item').first().waitFor();
            assert.ok(await page.locator('.st-catmenu__cat:visible').count() > 0);
            assert.ok(await page.locator('.st-catmenu__panel:visible').count() > 0);
            if (width === 1728) await page.screenshot({ path: resolve(output, 'shell-category-focus.png'), fullPage: true });
            await page.keyboard.press('Escape');
            await page.locator('.st-catmenu').waitFor({ state: 'detached' });
            await page.waitForFunction(() => document.activeElement?.matches('[data-testid=catmenu-trigger]'));
        } else {
            await page.locator('.st-mobile-menu-toggle').click();
            await page.locator('.st-mobile-nav__sheet').waitFor();
            assert.equal(await page.locator('.st-mobile-nav__sheet a[href^="/category/"]').count(), 6);
            await page.locator('.st-mobile-nav__sheet a').last().focus();
            await page.keyboard.press('Tab');
            assert.equal(await page.locator('.st-mobile-nav__sheet').evaluate(el => el.contains(document.activeElement)), true);
            await page.keyboard.press('Escape');
            await page.locator('.st-mobile-nav').waitFor({ state: 'detached' });
            await page.waitForFunction(() => document.activeElement?.matches('.st-mobile-menu-toggle'));
        }
        results.push({ width, passed: true });
    }
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('http://127.0.0.1:5088/login', { waitUntil: 'networkidle' });
    assert.equal(await page.locator('form[action="/auth/customer/login"]').count(), 1);
    const input = page.locator('#pw-mobile');
    await input.focus();
    assert.equal(await input.evaluate(el => getComputedStyle(el).borderRadius), '6px');
    await page.locator('.st-header__actions > .st-theme-toggle').click();
    await page.waitForFunction(() => document.documentElement.dataset.theme === 'dark');
    await page.waitForFunction(() => getComputedStyle(document.querySelector('.site-shell')).backgroundColor === 'rgb(23, 28, 30)');
    assert.equal(await page.locator('.site-shell').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(23, 28, 30)');
    await page.screenshot({ path: resolve(output, 'shell-dark-controls.png'), fullPage: true });
    await page.locator('.st-header__actions > .st-theme-toggle').click();
    await page.waitForFunction(() => document.documentElement.dataset.theme === 'light');
    await page.goto('http://127.0.0.1:5088/admin/login', { waitUntil: 'networkidle' });
    assert.equal(await page.locator('.site-shell, .home-shell').count(), 0);
    for (const width of [1728, 402]) {
        await page.setViewportSize({ width, height: 900 });
        await page.goto('http://127.0.0.1:5088/', { waitUntil: 'networkidle' });
        await page.evaluate(() => document.fonts.ready);
        await page.waitForFunction(() => !document.querySelector('.vz-splash:not(.is-done)') && document.querySelector('.hp-review-track').scrollLeft < 0);
        const snapshot = () => page.evaluate(() => [...document.querySelectorAll('.home-shell, .home-shell *')].map(el => {
            const cs = getComputedStyle(el), r = el.getBoundingClientRect();
            return { tag: el.tagName, cls: el.className?.baseVal ?? el.className, x: r.x, y: r.y, w: r.width, h: r.height,
                style: Object.fromEntries(['display','position','margin','padding','font-family','font-size','font-weight','line-height','color','background-color','border-radius','transform','filter','opacity','direction'].map(p => [p, cs.getPropertyValue(p)])) };
        }));
        const enabledStyles = await snapshot();
        await page.screenshot({ fullPage: true, path: resolve(output, 'home-isolation-enabled-' + width + '.png'), animations: 'disabled' });
        await page.evaluate(() => { document.querySelector('link[href*="site-shell.css"]').disabled = true; });
        const disabledStyles = await snapshot();
        assert.deepEqual(enabledStyles, disabledStyles, 'Shared stylesheet changed homepage computed styles/geometry at ' + width);
        await page.screenshot({ fullPage: true, path: resolve(output, 'home-isolation-disabled-' + width + '.png'), animations: 'disabled' });
    }
    return { viewports: results, search: true, categories: true, mobileNavigation: true, controls: true, darkTheme: true, adminScope: true, homeStyleAndGeometryIsolation: true, authenticatedCustomer: 'Not exercised; no real customer credentials used.' };
}
