import {
  test, expect, TAG, ProductBuilder, StorefrontProductPage,
  getProductState, expectCatalogIntegrity
} from '../framework/fixtures';

const invalidImage = 'D:\\Vitorize\\Vitorize\\tests\\Vitorize.E2E\\fixtures\\invalid-image.txt';

test.describe('Negative product configuration', () => {
  test.describe.configure({ timeout: 120_000 });

  test('required title, slug, and category are rejected by the real Admin form', {
    tag: [TAG.admin, TAG.product, TAG.negative, TAG.regression, TAG.release]
  }, async ({ loginAs, adminProduct, page }) => {
    await loginAs('SuperAdmin');
    await adminProduct.openCreate();
    await page.getByTestId('product-title').fill('');
    await page.getByTestId('product-slug').fill('');
    await page.getByTestId('product-category').selectOption('');
    await adminProduct.saveExpectingError();
    await expect(page).toHaveURL(/\/admin\/products\/create$/);
  });

  test('duplicate slug is rejected and only the original Product row remains', {
    tag: [TAG.admin, TAG.product, TAG.negative, TAG.regression, TAG.release]
  }, async ({ loginAs, adminProduct, request, page }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    const original = new ProductBuilder(`e2e-duplicate-${key}`, `Duplicate Original ${key}`).ofType(99).deliveredBy(2).build();
    const duplicate = new ProductBuilder(original.slug, `Duplicate Rejected ${key}`).ofType(99).deliveredBy(2).build();
    await loginAs('SuperAdmin');
    const originalId = await adminProduct.create(original);
    await adminProduct.openCreate();
    await adminProduct.fill(duplicate);
    await adminProduct.saveExpectingError();
    await expect(page).toHaveURL(/\/admin\/products\/create$/);
    const state = await getProductState(request, original.slug);
    expect(state.product.id).toBe(originalId);
    expect(state.product.title).toBe(original.title);
    expectCatalogIntegrity(state);
    await adminProduct.setActive(originalId, false);
  });

  test('invalid product pricing, quantity limits, and oversized SEO are rejected', {
    tag: [TAG.admin, TAG.product, TAG.negative, TAG.regression, TAG.release]
  }, async ({ loginAs, adminProduct, page }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    await loginAs('SuperAdmin');

    const discount = new ProductBuilder(`e2e-bad-discount-${key}`, `Bad Discount ${key}`).priced(100, 101).build();
    await adminProduct.openCreate();
    await adminProduct.fill(discount);
    await adminProduct.saveExpectingError();

    const negative = new ProductBuilder(`e2e-negative-price-${key}`, `Negative Price ${key}`).priced(-1).build();
    await adminProduct.openCreate();
    await adminProduct.fill(negative);
    await adminProduct.saveExpectingError();

    const quantities = new ProductBuilder(`e2e-bad-quantity-${key}`, `Bad Quantity ${key}`).quantities(5, 2).build();
    await adminProduct.openCreate();
    await adminProduct.fill(quantities);
    await adminProduct.saveExpectingError();

    const seo = new ProductBuilder(`e2e-long-seo-${key}`, `Long SEO ${key}`)
      .seo('S'.repeat(251), 'D'.repeat(501)).build();
    await adminProduct.openCreate();
    await adminProduct.fill(seo);
    await adminProduct.saveExpectingError();
    await expect(page).toHaveURL(/\/admin\/products\/create$/);
  });

  test('zero base price is a supported persisted state', {
    tag: [TAG.admin, TAG.product, TAG.catalog, TAG.regression]
  }, async ({ loginAs, adminProduct, request }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    const product = new ProductBuilder(`e2e-zero-price-${key}`, `Zero Price ${key}`)
      .ofType(99).deliveredBy(2).priced(0).build();
    await loginAs('SuperAdmin');
    const productId = await adminProduct.create(product);
    const state = await getProductState(request, product.slug);
    expect(state.product.basePrice).toBe(0);
    expectCatalogIntegrity(state);
    await adminProduct.setActive(productId, false);
  });

  test('instant product without GiftCode inventory is safely out of stock', {
    tag: [TAG.admin, TAG.product, TAG.instantDelivery, TAG.negative, TAG.regression, TAG.release]
  }, async ({ loginAs, adminProduct, storefrontProduct }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    const product = new ProductBuilder(`e2e-no-stock-${key}`, `No Stock ${key}`)
      .ofType(1).deliveredBy(1).build();
    await loginAs('SuperAdmin');
    const productId = await adminProduct.create(product);
    await storefrontProduct.open(product.slug);
    await storefrontProduct.expectOutOfStock();
    await adminProduct.setActive(productId, false);
  });

  test('invalid gallery file extension is rejected without a ProductImage row', {
    tag: [TAG.admin, TAG.product, TAG.catalog, TAG.negative, TAG.regression]
  }, async ({ loginAs, adminProduct, request, page }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    const product = new ProductBuilder(`e2e-invalid-image-${key}`, `Invalid Image ${key}`)
      .ofType(99).deliveredBy(2).build();
    await loginAs('SuperAdmin');
    const productId = await adminProduct.create(product);
    await page.goto(`/admin/products/${productId}/images`);
    await page.locator('input[type="file"]').first().setInputFiles(invalidImage);
    await expect(page.locator('.vz-toast.error, .vz-toast--error')).toBeVisible();
    const state = await getProductState(request, product.slug);
    expect(state.product.images).toHaveLength(0);
    expectCatalogIntegrity(state);
    await adminProduct.setActive(productId, false);
  });
});
