import { expect, test } from '../framework/fixtures';

/**
 * FIX-16 (Client Issue #16). Blazor's <FocusOnNavigate Selector="h1" /> focuses the page heading
 * after every route change and stamps tabindex="-1" on it. The global keyboard focus ring in
 * storefront.css matched [tabindex], so once the browser was in keyboard modality every navigation
 * painted an amber (#f59e0b) outline around the page title.
 *
 * The regression is asserted on computed style rather than pixels, and in two directions:
 *   1. the programmatically focused heading must carry no outline;
 *   2. an element a user can actually Tab to must still get the amber outline.
 * Assertion (2) is what stops this being "fixed" by disabling focus rings generally.
 */

test.describe('FIX-16 navigation focus ring @fix16', () => {
  test.describe.configure({ timeout: 180_000 });

  // Runs in both desktop themes: the fix must hold whatever palette the outline would have used.
  test('storefront route changes focus the heading without outlining it', async ({ page, consoleGuard }, testInfo) => {
    test.skip(!testInfo.project.name.startsWith('desktop-'), 'Desktop themes only.');
    await page.setViewportSize({ width: 1440, height: 900 });

    await page.goto('/', { waitUntil: 'networkidle' });
    // Keyboard modality is required: without it :focus-visible never matches and the bug cannot
    // reproduce, so the assertions below would pass vacuously.
    await page.keyboard.press('Tab');
    expect(await focusVisible(page)).toBe(true);

    for (const href of ['/shop', '/faq', '/categories']) {
      await keyboardNavigate(page, href);
      await expectHeadingFocusedWithoutOutline(page, href);
    }

    consoleGuard.assertClean();
  });

  test('admin route changes focus the heading without outlining it', async ({ page, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Admin coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAs('SuperAdmin');

    await page.goto('/admin/dashboard', { waitUntil: 'networkidle' });
    await page.keyboard.press('Tab');

    for (const path of ['/admin/orders', '/admin/pages', '/admin/notifications']) {
      await keyboardNavigate(page, path);
      await expectHeadingFocusedWithoutOutline(page, path);
    }

    consoleGuard.assertClean();
  });

  test('a direct URL load leaves no outline on the focused heading', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Direct-load coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });

    for (const url of ['/shop', '/faq', '/cart']) {
      await page.goto(url, { waitUntil: 'networkidle' });
      const heading = await headingStyle(page);
      expect(heading, `${url} must render an h1`).not.toBeNull();
      expect(heading!.outlineStyle === 'none' || heading!.outlineWidth === 0,
        `${url} direct load must not outline the title`).toBe(true);
    }

    consoleGuard.assertClean();
  });

  test('keyboard-reachable controls still receive the focus ring', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Accessibility regression runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/shop', { waitUntil: 'networkidle' });
    await page.keyboard.press('Tab');

    // A plain anchor is matched by the very rule this fix edited, so it proves the ring still fires
    // for elements a user can Tab to — the guard against a blanket outline:none regression.
    const anchor = await page.evaluate(() => {
      const link = document.querySelector('footer a[href], header a[href]') as HTMLElement | null;
      if (!link) return null;
      link.focus();
      const cs = getComputedStyle(link);
      return {
        focusVisible: link.matches(':focus-visible'),
        outlineStyle: cs.outlineStyle,
        outlineWidth: parseFloat(cs.outlineWidth || '0') || 0,
        outlineColor: cs.outlineColor
      };
    });

    expect(anchor, 'the page must expose an anchor to focus').not.toBeNull();
    expect(anchor!.focusVisible, 'a keyboard-focused link must match :focus-visible').toBe(true);
    expect(anchor!.outlineStyle, 'a keyboard-focused link must keep its outline').toBe('solid');
    expect(anchor!.outlineWidth, 'the focus outline must be visible').toBeGreaterThan(0);

    // Tabbing forward and backward must keep landing on controls that advertise focus.
    for (const key of ['Tab', 'Tab', 'Shift+Tab']) {
      await page.keyboard.press(key);
      const info = await page.evaluate(() => {
        const el = document.activeElement as HTMLElement | null;
        if (!el || el === document.body) return null;
        const cs = getComputedStyle(el);
        return {
          tag: el.tagName,
          tabindex: el.getAttribute('tabindex'),
          focusVisible: el.matches(':focus-visible'),
          outlineWidth: parseFloat(cs.outlineWidth || '0') || 0,
          outlineStyle: cs.outlineStyle,
          boxShadow: cs.boxShadow
        };
      });
      if (!info || info.tabindex === '-1') continue;
      expect(info.focusVisible, `${key} landed on ${info.tag} which should match :focus-visible`).toBe(true);
      const ringed = (info.outlineStyle !== 'none' && info.outlineWidth > 0) ||
        (info.boxShadow !== 'none' && info.boxShadow.trim() !== '');
      expect(ringed, `${key} landed on ${info.tag} with no visible focus indicator`).toBe(true);
    }

    consoleGuard.assertClean();
  });

  test('an open dialog keeps its container unringed and its controls focusable', async ({ page, loginAs, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-light', 'Dialog coverage runs once.');
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAs('SuperAdmin');
    await page.goto('/admin/pages', { waitUntil: 'networkidle' });
    await page.keyboard.press('Tab');

    await page.getByTestId('page-create').click();
    await expect(page.getByTestId('page-title')).toBeVisible();

    // The dialog shell is tabindex="-1" for focus trapping and must not be outlined.
    const container = await page.evaluate(() => {
      const el = document.querySelector('[tabindex="-1"].vz-dialog, [tabindex="-1"].vz-slidepanel, .vz-dialog [tabindex="-1"]');
      if (!el) return null;
      const cs = getComputedStyle(el);
      return { outlineStyle: cs.outlineStyle, outlineWidth: parseFloat(cs.outlineWidth || '0') || 0 };
    });
    if (container) {
      expect(container.outlineStyle === 'none' || container.outlineWidth === 0,
        'a focus-trap container must not be outlined').toBe(true);
    }

    // Controls inside the dialog keep their focus styling.
    const input = await page.evaluate(() => {
      const el = document.querySelector('[data-testid="page-title"]') as HTMLElement | null;
      if (!el) return null;
      el.focus();
      const cs = getComputedStyle(el);
      return {
        outlineWidth: parseFloat(cs.outlineWidth || '0') || 0,
        outlineStyle: cs.outlineStyle,
        boxShadow: cs.boxShadow
      };
    });
    expect(input).not.toBeNull();
    const inputRinged = (input!.outlineStyle !== 'none' && input!.outlineWidth > 0) ||
      (input!.boxShadow !== 'none' && input!.boxShadow.trim() !== '');
    expect(inputRinged, 'a focused dialog input must show focus styling').toBe(true);

    consoleGuard.assertClean();
  });

  test('mobile 390x844 navigation leaves no outline on the page title', async ({ page, consoleGuard }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-light', 'One mobile smoke is sufficient.');
    await page.setViewportSize({ width: 390, height: 844 });

    await page.goto('/', { waitUntil: 'networkidle' });
    await page.keyboard.press('Tab');

    for (const href of ['/shop', '/faq']) {
      await keyboardNavigate(page, href);
      await expectHeadingFocusedWithoutOutline(page, href);
    }

    expect(await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);

    consoleGuard.assertClean();
  });
});

/**
 * Asserts the FIX-16 contract for one route: Blazor still moves focus to the heading (route
 * accessibility preserved) and the heading renders no outline.
 */
async function expectHeadingFocusedWithoutOutline(page: import('@playwright/test').Page, route: string) {
  // FocusOnNavigate runs just after the route renders, so poll rather than sampling once.
  await expect
    .poll(async () => page.evaluate(() => {
      const el = document.activeElement as HTMLElement | null;
      return el ? `${el.tagName}:${el.getAttribute('tabindex')}` : 'none';
    }), { message: `${route} should hand focus to the page heading`, timeout: 10_000 })
    .toBe('H1:-1');

  const heading = await headingStyle(page);
  expect(heading, `${route} must render an h1`).not.toBeNull();
  expect(heading!.isActive, `${route} heading must be the focused element`).toBe(true);
  expect(heading!.outlineStyle === 'none' || heading!.outlineWidth === 0,
    `${route} must not outline the page title (was ${heading!.outlineStyle} ${heading!.outlineWidth}px ${heading!.outlineColor})`).toBe(true);
}

async function headingStyle(page: import('@playwright/test').Page) {
  return page.evaluate(() => {
    const h1 = document.querySelector('h1') as HTMLElement | null;
    if (!h1) return null;
    const cs = getComputedStyle(h1);
    return {
      isActive: document.activeElement === h1,
      tabindex: h1.getAttribute('tabindex'),
      focusVisible: (() => { try { return h1.matches(':focus-visible'); } catch { return false; } })(),
      outlineStyle: cs.outlineStyle,
      outlineWidth: parseFloat(cs.outlineWidth || '0') || 0,
      outlineColor: cs.outlineColor
    };
  });
}

async function focusVisible(page: import('@playwright/test').Page) {
  return page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null;
    try { return !!el && el !== document.body && el.matches(':focus-visible'); } catch { return false; }
  });
}

/** Activates a link with the keyboard so the browser stays in keyboard modality across the route change. */
async function keyboardNavigate(page: import('@playwright/test').Page, path: string) {
  // Admin NavLinks render relative hrefs ("admin/orders"); storefront links are absolute ("/shop").
  // Only visible links are usable — at 390px the header nav collapses and the footer carries them.
  const link = page.locator(`a[href="${path}"], a[href="${path.replace(/^\//, '')}"]`)
    .locator('visible=true').first();
  await link.scrollIntoViewIfNeeded();
  await link.focus();
  await page.keyboard.press('Enter');
  await page.waitForURL(url => url.pathname === path, { timeout: 20_000 });
  await page.waitForLoadState('networkidle');
}
