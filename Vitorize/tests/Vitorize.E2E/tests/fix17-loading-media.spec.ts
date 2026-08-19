import { expect, test } from '../framework/fixtures';

/**
 * FIX-17 — the administrator-configurable initial loading image/GIF.
 *
 * Two contracts are covered:
 *   1. the configured medium replaces the built-in visual, and removing it restores the default;
 *   2. the default visual is actually *rendered correctly* on the routes that emit it.
 *
 * (2) matters because the loader's CSS used to live only in admin.css, which is not linked on
 * /login, /cart or /checkout — so the "loader" there was an unstyled full-size logo sitting in
 * normal document flow. These assertions are on computed style, not pixels.
 *
 * The loader is designed to disappear the moment the app shell renders, which makes it racy to
 * observe. Where its appearance is under test, js/initial-loader.js is blocked so the overlay
 * stays put; the release behaviour itself is then tested separately with the script allowed.
 */

const BROKEN_ROUTES = ['/login', '/cart', '/checkout'];
const PRERENDERED_ROUTES = ['/', '/shop'];

// A 1x1 GIF89a — the smallest thing the endpoint will accept as animated media.
const GIF_BASE64 = 'R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';

test.describe('FIX-17 configurable initial loading media @fix17', () => {
  test.describe.configure({ timeout: 240_000 });

  test('the default loader renders as a real overlay on the routes that emit it', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Styling contract runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await blockLoaderRelease(page);

    for (const route of BROKEN_ROUTES) {
      await page.goto(route, { waitUntil: 'domcontentloaded' });
      const loader = await loaderStyle(page);

      expect(loader, `${route} must emit the boot loader`).not.toBeNull();
      // The regression: these were position:static, in-flow, and pushed the page down.
      expect(loader!.position, `${route} loader must be a fixed overlay`).toBe('fixed');
      expect(loader!.zIndex, `${route} loader must sit above the page`).toBe('9999');
      expect(loader!.opaque, `${route} loader must have its own background`).toBe(true);
      // It must cover the viewport rather than occupy flow at the top of the document.
      expect(loader!.height).toBeGreaterThan(800);

      // The logo was previously unconstrained and rendered at its natural 512x512.
      expect(loader!.markImage, `${route} logo must be constrained`).not.toBeNull();
      expect(loader!.markImage!.width).toBeLessThanOrEqual(64);
      expect(loader!.markImage!.height).toBeLessThanOrEqual(64);

      // The spinner was a 0px-tall block with no animation.
      expect(loader!.spinner, `${route} must render the default ring`).not.toBeNull();
      expect(loader!.spinner!.height).toBeGreaterThan(20);
      expect(loader!.spinner!.animationName).not.toBe('none');

      expect(loader!.role).toBe('status');
      expect(loader!.srText).toContain('در حال بارگذاری');
    }
  });

  test('prerendered public routes emit no loader at all', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Prerender contract runs once.');
    await blockLoaderRelease(page);

    for (const route of PRERENDERED_ROUTES) {
      await page.goto(route, { waitUntil: 'domcontentloaded' });
      // Server-rendered content is already useful; covering it would only hurt perceived speed.
      expect(await page.locator('#vz-initial-loader').count(), `${route} must not be covered`).toBe(0);
    }
  });

  test('the loader releases itself promptly and leaves nothing behind', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Release contract runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });

    await page.goto('/login', { waitUntil: 'networkidle' });

    // Removed from the DOM entirely — no invisible overlay left swallowing clicks.
    await expect(page.locator('#vz-initial-loader')).toHaveCount(0, { timeout: 15_000 });
    await expect(page.locator('.st-shell, .vz-shell, .vz-blank').first()).toBeVisible();

    // The login form must be genuinely interactive, which a stuck overlay would prevent.
    const mobile = page.locator('#pw-mobile');
    await expect(mobile).toBeVisible();
    await mobile.click();
    await mobile.fill('09120000013');
    await expect(mobile).toHaveValue('09120000013');

    consoleGuard.assertClean();
  });

  test('an uploaded GIF replaces the default visual, and removing it restores the default', async ({ page, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Admin journey runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAs('SuperAdmin');

    await page.goto('/admin/settings', { waitUntil: 'networkidle' });
    // Only the active tab's settings are rendered; the loading medium lives under "لوگو و تصاویر".
    await openLogosTab(page);
    const field = page.getByTestId('setting-LoadingMediaPath');
    await field.scrollIntoViewIfNeeded();
    await expect(field).toBeVisible();

    // Guidance must tell the admin that empty means "default".
    await expect(field).toContainText('GIF');
    await expect(field).toContainText('پیش‌فرض');

    // Upload — the control only accepts animated media on this one field.
    await expect(field.locator('input[type=file]')).toHaveAttribute('accept', /image\/gif/);
    await field.locator('input[type=file]').setInputFiles({
      name: 'vitorize-loader.gif', mimeType: 'image/gif', buffer: Buffer.from(GIF_BASE64, 'base64')
    });

    // Preview appears once stored, and the value is persisted immediately.
    const preview = field.locator('.vz-upload__preview img');
    await expect(preview).toBeVisible({ timeout: 30_000 });
    const previewSrc = await preview.getAttribute('src');
    expect(previewSrc).toMatch(/\/uploads\/settings\/[a-f0-9]{32}\.gif/);
    const storedFile = previewSrc!.match(/([a-f0-9]{32}\.gif)/)![1];

    // The boot loader now shows the configured medium instead of the built-in mark + ring.
    const configured = await readLoaderOnFreshPage(page, '/login');
    expect(configured.present, 'the loader must still be emitted').toBe(true);
    expect(configured.mediaSrc, 'the configured GIF must be used').toContain(storedFile);
    expect(configured.hasDefaultMark, 'the default mark must be replaced').toBe(false);
    expect(configured.hasDefaultSpinner, 'the default ring must be replaced').toBe(false);
    // Even a large GIF must stay inside the viewport rather than reproducing the old 512px bug.
    expect(configured.mediaWidth!).toBeLessThanOrEqual(240);
    expect(configured.role).toBe('status');

    // Remove / reset.
    await page.goto('/admin/settings', { waitUntil: 'networkidle' });
    await openLogosTab(page);
    const field2 = page.getByTestId('setting-LoadingMediaPath');
    await field2.scrollIntoViewIfNeeded();
    await field2.locator('.vz-upload__preview button').click();
    await expect(field2.locator('input[type=file]')).toBeAttached({ timeout: 30_000 });

    // Back to the built-in Vitorize loader.
    const restored = await readLoaderOnFreshPage(page, '/login');
    expect(restored.present).toBe(true);
    expect(restored.mediaSrc, 'the custom medium must be gone').toBeNull();
    expect(restored.hasDefaultMark, 'the default mark must return').toBe(true);
    expect(restored.hasDefaultSpinner, 'the default ring must return').toBe(true);

    consoleGuard.assertClean();
  });

  test('reduced motion keeps the loader legible without animating it', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Reduced-motion contract runs once.');
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await blockLoaderRelease(page);
    await page.goto('/login', { waitUntil: 'domcontentloaded' });

    const loader = await loaderStyle(page);
    expect(loader).not.toBeNull();
    expect(loader!.spinner!.animationName, 'the ring must not spin').toBe('none');
    // The state must still be conveyed: the mark is visible and the status text remains.
    expect(loader!.markImage).not.toBeNull();
    expect(loader!.srText).toContain('در حال بارگذاری');
  });

  test('the loader is correct in dark theme and at 390x844', async ({ page }, testInfo) => {
    test.skip(!['desktop-dark', 'mobile-light'].includes(testInfo.project.name), 'Theme/mobile smoke.');
    const mobile = testInfo.project.name === 'mobile-light';
    if (mobile) await page.setViewportSize({ width: 390, height: 844 });
    await blockLoaderRelease(page);
    await page.goto('/login', { waitUntil: 'domcontentloaded' });

    const loader = await loaderStyle(page);
    expect(loader).not.toBeNull();
    expect(loader!.position).toBe('fixed');
    expect(loader!.opaque, 'the overlay must not be transparent in either theme').toBe(true);
    expect(loader!.markImage!.width).toBeLessThanOrEqual(64);

    if (mobile) {
      // The loader must not introduce a horizontal scrollbar on a small screen.
      expect(await page.evaluate(() =>
        document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
    }
  });
});

/** Settings renders one tab at a time; the loading medium sits on the logos/images tab. */
async function openLogosTab(page: import('@playwright/test').Page) {
  await page.locator('.vz-settab', { hasText: 'لوگو و تصاویر' }).first().click();
  await expect(page.getByTestId('setting-LoadingMediaPath')).toBeVisible({ timeout: 20_000 });
}

/** Prevents the overlay from being released so its appearance can be measured deterministically. */
async function blockLoaderRelease(page: import('@playwright/test').Page) {
  await page.route('**/js/initial-loader.js*', route => route.abort());
}

async function loaderStyle(page: import('@playwright/test').Page) {
  return page.evaluate(() => {
    const el = document.getElementById('vz-initial-loader');
    if (!el) return null;
    const cs = getComputedStyle(el);
    const rect = el.getBoundingClientRect();
    const markImg = el.querySelector('.vz-splash-mark img') as HTMLElement | null;
    const spinner = el.querySelector('.vz-spinner') as HTMLElement | null;
    const px = (v: string) => parseFloat(v || '0') || 0;
    const bg = cs.backgroundColor;
    return {
      position: cs.position,
      zIndex: cs.zIndex,
      height: Math.round(rect.height),
      // "Transparent" would let the unstyled page show through, which is the old bug.
      opaque: bg !== 'transparent' && bg !== 'rgba(0, 0, 0, 0)',
      role: el.getAttribute('role'),
      srText: el.textContent?.trim() ?? '',
      markImage: markImg
        ? { width: Math.round(markImg.getBoundingClientRect().width),
            height: Math.round(markImg.getBoundingClientRect().height) }
        : null,
      spinner: spinner
        ? { height: px(getComputedStyle(spinner).height),
            animationName: getComputedStyle(spinner).animationName }
        : null
    };
  });
}

/**
 * Opens a route in a fresh context page with the release script blocked, reads the loader, and
 * closes it. A separate page is used so the admin session page is left untouched.
 */
async function readLoaderOnFreshPage(page: import('@playwright/test').Page, route: string) {
  const fresh = await page.context().newPage();
  try {
    await fresh.route('**/js/initial-loader.js*', r => r.abort());
    await fresh.goto(route, { waitUntil: 'domcontentloaded' });
    return await fresh.evaluate(() => {
      const el = document.getElementById('vz-initial-loader');
      if (!el) return { present: false, mediaSrc: null as string | null, mediaWidth: null as number | null,
                        hasDefaultMark: false, hasDefaultSpinner: false, role: null as string | null };
      const media = el.querySelector('.vz-splash__media') as HTMLImageElement | null;
      // A configured medium still emits the built-in mark and ring inside a hidden fallback, so a
      // broken or missing file degrades to the default instead of a broken-image icon. What matters
      // is therefore whether the default is SHOWN, not whether it exists in the DOM.
      const isShown = (selector: string) => {
        const node = el.querySelector(selector);
        return !!node && node.getClientRects().length > 0;
      };
      return {
        present: true,
        mediaSrc: media ? media.getAttribute('src') : null,
        mediaWidth: media ? Math.round(media.getBoundingClientRect().width) : null,
        hasDefaultMark: isShown('.vz-splash-mark'),
        hasDefaultSpinner: isShown('.vz-spinner'),
        role: el.getAttribute('role')
      };
    });
  } finally {
    await fresh.close();
  }
}
