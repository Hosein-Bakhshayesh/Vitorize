import {
  test, expect, TAG, ProductScenarioFactory, VariantBuilder,
  AdminProductPage, StorefrontProductPage,
  getProductState, expectCatalogIntegrity, loginSeededCustomerWithEmptyCart, clearCustomerCart
} from '../framework/fixtures';

const runKey = `${Date.now().toString(36)}-${process.pid}`;
const scenarios = ProductScenarioFactory.supported(runKey);

test.describe('Product Type / Delivery Type matrix', () => {
  test.describe.configure({ timeout: 120_000 });

  for (const scenario of scenarios) {
    test(`${scenario.key}: Admin create persists and the storefront can add the supported configuration`, {
      tag: [
        TAG.admin, TAG.product, TAG.catalog, TAG.regression, TAG.release,
        scenario.product.deliveryType === 1 ? TAG.instantDelivery :
          scenario.product.deliveryType === 2 ? TAG.manualDelivery : TAG.supportDelivery
      ]
    }, async ({ page, browser, request, loginAs, adminProduct, adminVariant }) => {
      await loginAs('SuperAdmin');
      const productId = await adminProduct.create(scenario.product);

      // Managed-stock products are created with one canonical SKU holding zero stock, so the
      // administrator's next step is entering a quantity. Instant products skip this: their
      // inventory is the gift-code pool, imported further down.
      if (scenario.product.deliveryType !== 1) {
        await adminVariant.setStock('پیش‌فرض', 25);
      }

      if (scenario.multipleVariants) {
        await adminVariant.create(new VariantBuilder('Matrix Standard', `MATRIX-STD-${runKey}`).priced(150_000, 140_000).default().sorted(20).stockMode(1).build());
        await adminVariant.create(new VariantBuilder('Matrix Deluxe', `MATRIX-DLX-${runKey}`).priced(175_000, 160_000).sorted(10).stockMode(1).build());
        await adminVariant.create(new VariantBuilder('Matrix Retired', `MATRIX-OFF-${runKey}`).priced(90_000).inactive().sorted(5).stockMode(1).build());
      }

      let state = await getProductState(request, scenario.product.slug);
      expect(state.product).toMatchObject({
        id: productId,
        title: scenario.product.title,
        slug: scenario.product.slug,
        productType: scenario.product.productType,
        deliveryType: scenario.product.deliveryType,
        basePrice: scenario.product.basePrice,
        discountPrice: scenario.product.discountPrice ?? null,
        minOrderQuantity: scenario.product.minQuantity,
        maxOrderQuantity: scenario.product.maxQuantity ?? null,
        isFeatured: scenario.product.featured,
        isActive: true
      });
      expect(state.product.brandId === null).toBe(!scenario.product.brand);
      expect(state.product.features).toHaveLength(scenario.product.features.length);
      expect(state.product.inputFields).toHaveLength(scenario.product.dynamicFields.length);
      expectCatalogIntegrity(state);

      if (scenario.requiresInventory) {
        if (scenario.multipleVariants) {
          for (const variant of state.product.variants.filter(v => v.isActive)) {
            await adminProduct.importGiftCodes(productId, [`CODE-${variant.sku}-${Date.now()}`], variant.id);
          }
        } else {
          await adminProduct.importGiftCodes(productId, [`CODE-${scenario.key}-${Date.now()}`]);
        }
        state = await getProductState(request, scenario.product.slug);
      }

      const customerContext = await browser.newContext();
      const customerPage = await customerContext.newPage();
      try {
        await loginSeededCustomerWithEmptyCart(customerPage);
        const storefront = new StorefrontProductPage(customerPage);
        await storefront.open(scenario.product.slug);
        await storefront.expectProduct(scenario.product, scenario.multipleVariants ? 140_000 : undefined);

        if (scenario.multipleVariants) {
          await expect(customerPage.locator('.st-vcard')).toHaveCount(2);
          await expect(customerPage.locator('.st-vcard.active')).toContainText('Matrix Standard');
          await storefront.selectVariant('Matrix Deluxe');
          expect(await storefront.currentPrice()).toBe(160_000);
        }

        const dynamicValues = Object.fromEntries(
          scenario.product.dynamicFields
            .filter(field => field.required)
            .map(field => [field.key, 'matrix-value'])
        );
        await storefront.addToCart(dynamicValues, scenario.product.dynamicFields.length > 0);
        await customerPage.goto('/cart');
        const cartRow = customerPage.locator('.st-stack > .st-card').filter({ hasText: scenario.product.title });
        await expect(cartRow).toHaveCount(1);
        if (scenario.multipleVariants) await expect(cartRow).toContainText('Matrix Deluxe');
        await clearCustomerCart(customerPage);
      } finally {
        await customerContext.close();
      }
      await adminProduct.setActive(productId, false);
    });
  }
});
