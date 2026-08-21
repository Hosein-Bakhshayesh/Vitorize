import {
  test, expect, TAG, ProductBuilder, VariantBuilder,
  StorefrontProductPage, getProductState, expectCatalogIntegrity,
  loginSeededCustomerWithEmptyCart, clearCustomerCart
} from '../framework/fixtures';

test.describe('Product variant matrix', () => {
  test.describe.configure({ timeout: 120_000 });

  test('create/edit/default/sort/active/price/SKU/cart separation/delete and fallback are deterministic', {
    tag: [TAG.admin, TAG.product, TAG.variant, TAG.catalog, TAG.customer, TAG.regression, TAG.release]
  }, async ({ page, browser, request, loginAs, adminProduct, adminVariant }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    const product = new ProductBuilder(`e2e-variant-matrix-${key}`, `Variant Matrix ${key}`)
      .ofType(1).deliveredBy(2).priced(80_000).build();

    await loginAs('SuperAdmin');
    await adminProduct.create(product);
    await adminVariant.create(new VariantBuilder('Variant Alpha', `VAR-A-${key}`).priced(120_000, 110_000).default().sorted(20).build());
    await adminVariant.create(new VariantBuilder('Variant Beta', `VAR-B-${key}`).priced(150_000, 130_000).sorted(10).build());
    await adminVariant.create(new VariantBuilder('Variant Inactive', `VAR-X-${key}`).priced(90_000).inactive().sorted(5).build());

    // Inventory is SKU-scoped, so creating a managed product also creates its canonical implicit
    // SKU (title «پیش‌فرض», sort order 0). It is part of the catalogue and therefore of every count
    // below; the storefront hides it from the customer, which the card assertions cover.
    const CANONICAL_SKU = 'پیش‌فرض';

    let state = await getProductState(request, product.slug);
    expect(state.product.variants.map(v => v.title)).toEqual([CANONICAL_SKU, 'Variant Inactive', 'Variant Beta', 'Variant Alpha']);
    expect(state.product.variants.filter(v => v.isDefault).map(v => v.title)).toEqual(['Variant Alpha']);
    expect(state.product.variants.filter(v => v.isActive)).toHaveLength(3);
    expectCatalogIntegrity(state);

    await adminVariant.createExpectingError(
      new VariantBuilder('Duplicate SKU', `VAR-A-${key}`).priced(99_000).build()
    );
    await page.getByRole('dialog').locator('button.vz-btn--outline').click();
    await expect(page.getByTestId('variant-form')).toHaveCount(0);
    state = await getProductState(request, product.slug);
    expect(state.product.variants).toHaveLength(4);

    await adminVariant.edit('Variant Beta',
      new VariantBuilder('Variant Beta Edited', `VAR-B2-${key}`).priced(155_000, 125_000).sorted(10).build());
    state = await getProductState(request, product.slug);
    expect(state.product.variants.find(v => v.title === 'Variant Beta Edited')).toMatchObject({
      sku: `VAR-B2-${key}`, price: 155_000, discountPrice: 125_000, sortOrder: 10, isActive: true
    });

    const customerContext = await browser.newContext();
    const customerPage = await customerContext.newPage();
    try {
      await loginSeededCustomerWithEmptyCart(customerPage);
      const storefront = new StorefrontProductPage(customerPage);
      await storefront.open(product.slug);
      // Only the administrator's real variants are offered; the implicit SKU stays hidden.
      await expect(customerPage.locator('.st-vcard')).toHaveCount(2);
      await expect(customerPage.locator('.st-vcard.active')).toContainText('Variant Alpha');

      await storefront.selectVariant('Variant Beta Edited');
      expect(await storefront.currentPrice()).toBe(125_000);
      await storefront.addToCart();
      await storefront.open(product.slug);
      await storefront.selectVariant('Variant Alpha');
      await storefront.addToCart();
      await customerPage.goto('/cart');
      const productLines = customerPage.locator('.st-stack > .st-card').filter({ hasText: product.title });
      await expect(productLines).toHaveCount(2);
      await expect(productLines.filter({ hasText: 'Variant Beta Edited' })).toHaveCount(1);
      await expect(productLines.filter({ hasText: 'Variant Alpha' })).toHaveCount(1);
      await clearCustomerCart(customerPage);
    } finally {
      await customerContext.close();
    }

    // An inactive default is hidden publicly; the first active variant by sort order is the safe fallback.
    await adminVariant.edit('Variant Inactive',
      new VariantBuilder('Variant Inactive', `VAR-X-${key}`).priced(90_000).inactive().default().sorted(5).build());
    state = await getProductState(request, product.slug);
    expect(state.product.variants.filter(v => v.isDefault).map(v => v.title)).toEqual(['Variant Inactive']);
    expectCatalogIntegrity(state);

    await page.goto(`/product/${product.slug}`);
    await expect(page.locator('.st-vcard.active')).toContainText('Variant Beta Edited');
    await expect(page.locator('.st-vcard')).toHaveCount(2);

    // Deleting an unreferenced inactive variant leaves no orphan and does not disturb active variants.
    await adminProduct.openEdit(state.product.id);
    await adminVariant.delete('Variant Inactive');
    state = await getProductState(request, product.slug);
    expect(state.product.variants).toHaveLength(3);
    expectCatalogIntegrity(state);

    // With every configured variant inactive, the storefront safely falls back to the base product.
    await adminVariant.edit('Variant Alpha',
      new VariantBuilder('Variant Alpha', `VAR-A-${key}`).priced(120_000, 110_000).inactive().sorted(20).build());
    await adminVariant.edit('Variant Beta Edited',
      new VariantBuilder('Variant Beta Edited', `VAR-B2-${key}`).priced(155_000, 125_000).inactive().sorted(10).build());
    state = await getProductState(request, product.slug);
    // The canonical SKU stays active on purpose: a managed product must never be left with no
    // purchasable SKU at all, which is exactly what F1 exists to prevent.
    expect(state.product.variants.filter(v => v.isActive).map(v => v.title)).toEqual([CANONICAL_SKU]);
    await page.goto(`/product/${product.slug}`);
    // A lone implicit SKU is not a choice, so the selector disappears for the customer.
    await expect(page.locator('.st-vcard')).toHaveCount(0);
    expect(await new StorefrontProductPage(page).currentPrice()).toBe(product.basePrice);
    await expect(page.locator('.st-buy__card')).toBeVisible();
    await adminProduct.setActive(state.product.id, false);
  });
});
