import {
  test, expect, TAG, ProductBuilder, StorefrontProductPage,
  getProductState, expectCatalogIntegrity, loginSeededCustomerWithEmptyCart, clearCustomerCart
} from '../framework/fixtures';

const galleryImage = 'D:\\Vitorize\\Vitorize\\Vitorize.Api\\wwwroot\\uploads\\products\\947c2fd1b9a84f2ea86a683008e7fdc0.jpg';

test.describe('Admin product edit and rich storefront projection', () => {
  test.describe.configure({ timeout: 120_000 });

  test('price entry preserves Persian digits and the caret while editing', {
    tag: [TAG.admin, TAG.product, TAG.regression, TAG.release]
  }, async ({ page, loginAs, adminProduct }) => {
    await loginAs('SuperAdmin');
    await adminProduct.openCreate();

    const price = page.getByTestId('product-base-price');
    await price.fill('۱۲۳۴۵');
    await expect(price).toHaveValue('۱۲۳۴۵');

    // Edit in the middle of the number. Re-rendering the value on every input
    // used to move the caret to the end and lose this edit.
    await price.press('ArrowLeft');
    await price.press('ArrowLeft');
    await price.press('۶');
    await expect(price).toHaveValue('۱۲۳۶۴۵');

    await price.press('Tab');
    await expect(price).toHaveValue('123,645');
  });

  test('all supported editable product metadata persists to DB', {
    tag: [TAG.admin, TAG.product, TAG.catalog, TAG.regression, TAG.release]
  }, async ({ page, request, loginAs, adminProduct }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    const original = new ProductBuilder(`e2e-edit-original-${key}`, `Edit Original ${key}`)
      .ofType(1).deliveredBy(2).withoutBrand().priced(100_000).build();
    const related = new ProductBuilder(`e2e-edit-related-${key}`, `Edit Related ${key}`)
      .ofType(99).deliveredBy(2).inCategory('E2E Child Category').priced(70_000).featured()
      // Share the strongest non-category ranking signal with the source so this
      // just-created candidate remains deterministic as prior repeat data grows.
      .tagged('آزمون مرورگر').build();
    const updated = new ProductBuilder(`e2e-edit-final-${key}`, `Edit Final ${key}`)
      .ofType(3).deliveredBy(3).inCategory('E2E Child Category').withBrand('E2E Brand')
      .priced(210_000, 180_000).inCurrency(1).quantities(2, 5).featured()
      .described('Edited deterministic short description.',
        '<h2>Safe Matrix HTML</h2><p><strong>Allowed content</strong></p><script>alert(1)</script><img src=x onerror=alert(2)>')
      .seo(`Edited SEO ${key}`, `Edited SEO description ${key}.`, 'edited matrix')
      .tagged('آزمون مرورگر')
      .withFeature('Platform', 'Cross-platform')
      .withFeature('Specification', 'Premium tier')
      .withDynamicField({ key: 'required_account', label: 'Required Account', fieldType: 1, required: true })
      .withDynamicField({ key: 'optional_note', label: 'Optional Note', fieldType: 5, required: false })
      .build();

    await loginAs('SuperAdmin');
    const productId = await adminProduct.create(original);
    const relatedId = await adminProduct.create(related);
    await adminProduct.openEdit(productId);
    await adminProduct.fill(updated);
    await adminProduct.saveEdit();
    await adminProduct.reload();
    await adminProduct.expectPersisted(updated);

    await adminProduct.openDetails(productId);
    await expect(page.locator('main, .vz-content')).toContainText(updated.title);
    await expect(page.locator('main, .vz-content')).toContainText('E2E Child Category');
    await expect(page.locator('main, .vz-content')).toContainText('E2E Brand');

    await adminProduct.uploadGalleryImage(productId, galleryImage);
    const state = await getProductState(request, updated.slug);
    expect(state.product).toMatchObject({
      id: productId,
      title: updated.title,
      slug: updated.slug,
      productType: 3,
      deliveryType: 3,
      basePrice: 210_000,
      discountPrice: 180_000,
      currencyType: 1,
      minOrderQuantity: 2,
      maxOrderQuantity: 5,
      isFeatured: true,
      isActive: true,
      seoTitle: updated.seoTitle,
      seoDescription: updated.seoDescription,
      focusKeyword: updated.focusKeyword
    });
    expect(state.product.brandId).not.toBeNull();
    expect(state.product.tags.map(t => t.title)).toContain('آزمون مرورگر');
    expect(state.product.features.map(f => [f.title, f.value])).toEqual([
      ['Platform', 'Cross-platform'], ['Specification', 'Premium tier']
    ]);
    expect(state.product.inputFields.map(f => [f.key, f.isRequired])).toEqual([
      ['required_account', true], ['optional_note', false]
    ]);
    expect(state.product.images).toHaveLength(1);
    expect(state.product.thumbnailImagePath).toBe(state.product.images[0].imagePath);
    expect(state.product.shortDescription).toBe(updated.shortDescription);
    expect(state.product.fullDescription).toContain('Safe Matrix HTML');
    expect(state.product.fullDescription).not.toMatch(/<script|onerror/i);
    expectCatalogIntegrity(state);

    await adminProduct.setActive(productId, false);
    await adminProduct.setActive(relatedId, false);
  });

  test('an edited product projects its rich content, features and required fields to the storefront', {
    tag: [TAG.admin, TAG.product, TAG.catalog, TAG.customer, TAG.supportDelivery, TAG.regression, TAG.release]
  }, async ({ page, browser, loginAs, adminProduct, adminVariant }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    const related = new ProductBuilder(`e2e-proj-related-${key}`, `Proj Related ${key}`)
      .ofType(99).deliveredBy(2).inCategory('E2E Child Category').priced(70_000).featured()
      .tagged('آزمون مرورگر').build();
    const updated = new ProductBuilder(`e2e-proj-final-${key}`, `Proj Final ${key}`)
      .ofType(3).deliveredBy(3).inCategory('E2E Child Category').withBrand('E2E Brand')
      .priced(210_000, 180_000).inCurrency(1).quantities(2, 5).featured()
      .described('Edited deterministic short description.',
        '<h2>Safe Matrix HTML</h2><p><strong>Allowed content</strong></p><script>alert(1)</script><img src=x onerror=alert(2)>')
      .seo(`Proj SEO ${key}`, `Proj SEO description ${key}.`, 'projected matrix')
      .tagged('آزمون مرورگر')
      .withFeature('Platform', 'Cross-platform')
      .withFeature('Specification', 'Premium tier')
      .withDynamicField({ key: 'required_account', label: 'Required Account', fieldType: 1, required: true })
      .withDynamicField({ key: 'optional_note', label: 'Optional Note', fieldType: 5, required: false })
      .build();

    await loginAs('SuperAdmin');
    const relatedId = await adminProduct.create(related);
    const productId = await adminProduct.create(updated);
    // A support-delivery product is created with one canonical SKU at zero stock, so it is not
    // purchasable until an administrator enters a quantity. The storefront assertions below reach
    // add-to-cart, so perform that step here exactly as an administrator would.
    await adminVariant.setStock('پیش‌فرض', 25);
    await adminProduct.uploadGalleryImage(productId, galleryImage);

    const customerContext = await browser.newContext();
    const customerPage = await customerContext.newPage();
    try {
      await loginSeededCustomerWithEmptyCart(customerPage);
      const storefront = new StorefrontProductPage(customerPage);
      await storefront.open(updated.slug);
      await storefront.expectProduct(updated);
      await expect(customerPage.locator('.st-rich-content')).toContainText('Safe Matrix HTML');
      await expect(customerPage.locator('.st-feature-card')).toHaveCount(2);
      await expect(customerPage.locator('.st-section')).toContainText(related.title);
      await expect(customerPage.locator('meta[name="description"]')).toHaveAttribute('content', updated.seoDescription!);
      // Adds the product and proves the projected required field stops the purchase at Checkout.
      // Product information is no longer collected in a product-page dialog, so there is nothing
      // to dismiss afterwards and no second add-to-cart to perform - the add already happened here.
      await storefront.expectRequiredFieldRejected('required_account');
      await clearCustomerCart(customerPage);
    } finally {
      await customerContext.close();
    }

    await adminProduct.setActive(productId, false);
    await adminProduct.setActive(relatedId, false);
  });

  test('inactive product stays manageable by Admin but is absent from listing and direct storefront URL', {
    tag: [TAG.admin, TAG.product, TAG.catalog, TAG.negative, TAG.regression, TAG.release]
  }, async ({ page, request, loginAs, adminProduct, storefrontProduct }) => {
    const key = `${Date.now().toString(36)}-${process.pid}`;
    const product = new ProductBuilder(`e2e-inactive-${key}`, `Inactive Matrix ${key}`)
      .ofType(99).deliveredBy(2).inactive().build();

    await loginAs('SuperAdmin');
    const productId = await adminProduct.create(product);
    await adminProduct.openDetails(productId);
    await expect(page.locator('.vz-content')).toContainText(product.title);
    const state = await getProductState(request, product.slug);
    expect(state.product.isActive).toBe(false);

    await page.goto(`/shop?q=${encodeURIComponent(product.title)}`);
    await expect(page.locator('.st-empty')).toBeVisible();
    await expect(page.locator('main')).not.toContainText(product.title);
    await storefrontProduct.expectNotPublic(product.slug);
  });
});
