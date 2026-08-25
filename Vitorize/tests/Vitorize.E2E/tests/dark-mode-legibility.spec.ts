import { expect, test } from '../framework/fixtures';
import { apiBaseUrl, adminMobile, adminPassword } from './support/app';
import type { APIRequestContext, Page } from '@playwright/test';

/**
 * Dark mode must never render text that cannot be read.
 *
 * Two mechanisms are guarded here. First, the UI chrome itself: every text token has to flip with
 * the theme, and instead of trusting that by eyeball, the sweep below walks the rendered pages and
 * fails on any visible text whose computed colour is near-black on a near-black background. Second,
 * author-supplied colours: the sanitizer deliberately keeps the style attribute, so black text
 * chosen in the editor used to survive into product descriptions, CMS pages and blog posts and
 * vanish in dark mode. Those colours are now neutralised in dark only - unless the author also set
 * a background, in which case the pair was designed together and is left alone.
 */
test.describe('Dark mode legibility @ui @regression', () => {
  test.describe.configure({ timeout: 180_000 });

  async function useDarkTheme(page: Page) {
    await page.context().addCookies([{ name: 'vitorize-theme', value: 'dark', url: 'http://localhost:5077' }]);
  }

  /**
   * Every visible text node on the page, checked for dark-on-dark.
   *
   * "Effective background" walks up the tree to the first non-transparent backgroundColor, because
   * most elements paint no background of their own. Elements that are invisible, empty, or tiny are
   * skipped - they carry no reading.
   */
  async function sweepForInvisibleText(page: Page): Promise<string[]> {
    return page.evaluate(() => {
      const luminance = (rgb: string): number | null => {
        const match = rgb.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)/);
        if (!match) return null;
        if (match[4] !== undefined && parseFloat(match[4]) === 0) return null; // transparent
        return (0.2126 * +match[1] + 0.7152 * +match[2] + 0.0722 * +match[3]) / 255;
      };

      const effectiveBackground = (start: Element): number | null => {
        let node: Element | null = start;
        while (node) {
          const style = getComputedStyle(node);
          // A gradient or image surface has no single luminance; the accent buttons paint a bright
          // teal gradient under deliberately dark text. Unknowable surfaces are assumed legible -
          // this sweep exists to catch dark-on-dark, not to grade designed pairings.
          if (style.backgroundImage !== 'none') return null;
          const paint = luminance(style.backgroundColor);
          if (paint !== null) return paint;
          node = node.parentElement;
        }
        return 1; // nothing painted anywhere: the light default
      };

      const offenders: string[] = [];
      for (const element of Array.from(document.querySelectorAll<HTMLElement>('body *'))) {
        const hasOwnText = Array.from(element.childNodes)
          .some(n => n.nodeType === Node.TEXT_NODE && (n.textContent ?? '').trim().length > 1);
        if (!hasOwnText) continue;

        const rect = element.getBoundingClientRect();
        if (rect.width < 4 || rect.height < 4) continue;

        const style = getComputedStyle(element);
        if (style.visibility === 'hidden' || style.display === 'none' || parseFloat(style.opacity) < 0.05) continue;

        const text = luminance(style.color);
        if (text === null || text >= 0.25) continue;             // the text itself is not dark
        const surface = effectiveBackground(element);
        if (surface === null || surface >= 0.25) continue;       // light or unknowable surface is fine

        offenders.push(
          `<${element.tagName.toLowerCase()} class="${element.className}"> ` +
          `"${(element.textContent ?? '').trim().slice(0, 40)}" color=${style.color}`);
        if (offenders.length >= 10) break;
      }
      return offenders;
    });
  }

  async function adminToken(request: APIRequestContext): Promise<string> {
    const response = await request.post(`${apiBaseUrl}/auth/login`, {
      data: { mobile: adminMobile, password: adminPassword }
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    return (await response.json()).data.accessToken as string;
  }

  /** A product whose description carries exactly the author colours that used to disappear. */
  async function createBlackTextProduct(request: APIRequestContext): Promise<string> {
    const token = await adminToken(request);
    const headers = { Authorization: `Bearer ${token}` };
    const key = `${Date.now().toString(36)}`;

    const categories = await request.get(`${apiBaseUrl}/admin/categories`, { headers });
    const categoryId = (await categories.json()).data[0].id;

    const created = await request.post(`${apiBaseUrl}/admin/products`, {
      headers,
      data: {
        // 150000, not cheaper: the visual baseline snapshots this category sorted by price, and its
        // first page is full below this point - the same price the CTA-geometry fixture uses, which
        // the approved baselines already prove stays off the captured page.
        title: `Dark Legibility ${key}`, slug: `dark-legibility-${key}`, categoryId,
        productType: 1, deliveryType: 2, basePrice: 150000, currencyType: 2,
        minOrderQuantity: 1, isActive: true, shortDescription: 'Dark-mode fixture.',
        fullDescription:
          '<h2 style="color:#000000">عنوان مشکی</h2>' +
          '<p style="color:hsl(0, 0%, 0%);">این متن در ادیتور مشکی انتخاب شده است.</p>' +
          '<p style="background-color:#fde047;color:#000000">این متن هایلایت زرد دارد.</p>',
        features: [], inputFields: [], tagIds: []
      }
    });
    expect(created.ok(), await created.text()).toBeTruthy();
    const productId = (await created.json()).data.id as string;

    const variant = await request.post(`${apiBaseUrl}/admin/products/${productId}/variants`, {
      headers,
      data: {
        title: 'نسخه تیره', sku: `DARK-${key}`, price: 150000, value: `DARK-${key}`,
        stockMode: 1, stockQuantity: 10, isDefault: true, isActive: true, sortOrder: 1
      }
    });
    expect(variant.ok(), await variant.text()).toBeTruthy();
    return `dark-legibility-${key}`;
  }

  test('legacy author-picked black text in rich content is readable in dark mode', async ({ page, request }) => {
    // The CURRENT sanitizer strips the color property, so nothing saved today can carry it. But the
    // production database holds content saved by older builds, rendered exactly as stored - so the
    // legacy shape is injected into the real container and judged by the real stylesheet.
    const slug = await createBlackTextProduct(request);
    await useDarkTheme(page);
    await page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await expect(page.locator('.st-rich-content')).toBeVisible();

    const colours = await page.evaluate(() => {
      const rich = document.querySelector('.st-rich-content')!;
      rich.insertAdjacentHTML('beforeend',
        '<h2 style="color:#000000" data-probe="h">عنوان مشکی قدیمی</h2>' +
        '<p style="color:rgb(0,0,0);" data-probe="p">متن مشکی قدیمی</p>' +
        '<p style="background-color:#fde047;color:#000000" data-probe="hl">هایلایت طراحی‌شده</p>');
      const channel = (selector: string) => {
        const el = rich.querySelector<HTMLElement>(selector);
        if (!el) return null;
        const m = getComputedStyle(el).color.match(/\d+/g);
        return m ? (+m[0] + +m[1] + +m[2]) / 3 : null;
      };
      return {
        heading: channel('[data-probe="h"]'),
        paragraph: channel('[data-probe="p"]'),
        highlighted: channel('[data-probe="hl"]')
      };
    });

    // The author's black is ignored on a dark surface...
    expect(colours.heading).not.toBeNull();
    expect(colours.heading!).toBeGreaterThan(120);
    expect(colours.paragraph!).toBeGreaterThan(120);
    // ...but a highlight keeps its designed pair: black on the author's yellow stays black.
    expect(colours.highlighted!).toBeLessThan(80);
  });

  test('no page renders dark text on a dark surface', async ({ page, request }) => {
    const slug = await createBlackTextProduct(request);
    await useDarkTheme(page);

    for (const path of ['/', '/shop', `/product/${slug}`, '/cart', '/about', '/faq']) {
      await page.goto(path, { waitUntil: 'networkidle' });
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
      const offenders = await sweepForInvisibleText(page);
      expect(offenders, `${path} renders unreadable dark-on-dark text:\n${offenders.join('\n')}`).toEqual([]);
    }
  });

  test('the variant cards are legible in dark mode', async ({ page, request }) => {
    // Reported explicitly: on the old build the variant text vanished in dark. The cards are pure
    // token chrome, so this pins the tokens actually flipping rather than trusting the palette.
    const slug = await createBlackTextProduct(request);
    await useDarkTheme(page);
    await page.goto(`/product/${slug}`, { waitUntil: 'networkidle' });

    const card = page.locator('.st-vcard').first();
    await expect(card).toBeVisible();
    const readable = await card.evaluate((element) => {
      const bright = (el: Element) => {
        const m = getComputedStyle(el).color.match(/\d+/g);
        return m ? (+m[0] + +m[1] + +m[2]) / 3 : 0;
      };
      const title = element.querySelector('.st-vcard__t');
      const price = element.querySelector('.st-vcard__p');
      const stock = element.querySelector('.st-vcard__stock');
      return {
        title: title ? bright(title) : null,
        price: price ? bright(price) : null,
        stock: stock ? bright(stock) : null
      };
    });

    expect(readable.title!).toBeGreaterThan(120);
    expect(readable.price!).toBeGreaterThan(100);
    expect(readable.stock!).toBeGreaterThan(100);
  });
});
