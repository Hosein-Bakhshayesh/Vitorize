import { expect, test } from '@playwright/test';

test('product is present in initial HTML with complete safe SEO metadata', async ({ request }) => {
  const response = await request.get('/product/e2e-seo-product');
  expect(response.status()).toBe(200);
  const html = decodeNumericEntities(await response.text());
  expect(html).toContain('E2E Dynamic Product');
  expect(html).toMatch(/<title>[^<]*E2E Product SEO[^<]*<\/title>/);
  expect(html).toMatch(/<meta name="description" content="E2E product meta description\."/);
  expect(html).toMatch(/<link rel="canonical" href="http:\/\/127\.0\.0\.1:5077\/product\/e2e-seo-product"/);
  expect(html).toMatch(/<meta property="og:title"/);
  expect(html).toMatch(/<meta property="og:description"/);
  expect(html).toMatch(/<meta name="twitter:card"/);
  expect(html).toContain('"@type":"Product"');
  expect(html).toContain('"@type":"AggregateRating"');
  expect(html).toContain('"@type":"Review"');
  expect(html).toContain('"@type":"BreadcrumbList"');
  expect(html).not.toContain('meta name="keywords"');
  expect(html).not.toMatch(/<script[^>]*>[^<]*(javascript:|onerror=)/i);
});

function decodeNumericEntities(value: string): string {
  return value
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#([0-9]+);/g, (_, decimal) => String.fromCodePoint(parseInt(decimal, 10)));
}

test('robots, sitemap, exact redirect, noindex and real 404 status are correct', async ({ request }) => {
  const robots = await request.get('/robots.txt');
  expect(robots.status()).toBe(200);
  expect(await robots.text()).toContain('Disallow: /');

  const sitemap = await request.get('/sitemap.xml');
  expect(sitemap.status()).toBe(200);
  expect(sitemap.headers()['content-type']).toContain('xml');

  const productSitemap = await request.get('/sitemaps/products-1.xml');
  expect(productSitemap.status()).toBe(200);
  expect(await productSitemap.text()).toContain('/product/e2e-seo-product');

  const redirect = await request.get('/e2e-old-product', { maxRedirects: 0 });
  expect(redirect.status()).toBe(301);
  expect(redirect.headers().location).toBe('/product/e2e-seo-product');

  const gone = await request.get('/e2e-gone-product', { maxRedirects: 0 });
  expect(gone.status()).toBe(410);
  expect(await gone.text()).toContain('noindex,nofollow');

  const search = await request.get('/search?q=test');
  expect(await search.text()).toContain('name="robots" content="noindex,follow"');

  const missing = await request.get('/route-that-does-not-exist-e2e');
  expect(missing.status()).toBe(404);
  expect(await missing.text()).toContain('noindex,nofollow');
});

test('security headers and public asset partitioning are applied', async ({ request }) => {
  const response = await request.get('/');
  expect(response.headers()['x-content-type-options']).toBe('nosniff');
  expect(response.headers()['x-frame-options']).toBeTruthy();
  expect(response.headers()['content-security-policy']).toBeTruthy();
  const html = await response.text();
  expect(html).not.toContain('quill.min.js');
  expect(html).not.toContain('admin.css');
});
