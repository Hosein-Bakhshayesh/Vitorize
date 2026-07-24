import { expect, type APIRequestContext } from '@playwright/test';
import { apiBaseUrl } from '../tests/support/app';

export type ProductState = {
  product: {
    id: string;
    categoryId: string;
    brandId: string | null;
    title: string;
    slug: string;
    shortDescription: string | null;
    fullDescription: string | null;
    productType: number;
    deliveryType: number;
    basePrice: number;
    discountPrice: number | null;
    currencyType: number;
    minOrderQuantity: number;
    maxOrderQuantity: number | null;
    isFeatured: boolean;
    isActive: boolean;
    seoTitle: string | null;
    seoDescription: string | null;
    focusKeyword: string | null;
    thumbnailImagePath: string | null;
    thumbnailAltText: string | null;
    tags: Array<{ id: string; title: string }>;
    variants: Array<{
      id: string; title: string; sku: string | null; price: number; discountPrice: number | null;
      value: string | null; stockMode: number; isDefault: boolean; isActive: boolean; sortOrder: number;
    }>;
    images: Array<{ id: string; imagePath: string; altText: string | null; sortOrder: number }>;
    features: Array<{ id: string; title: string; value: string; iconKey: string | null; isActive: boolean; sortOrder: number }>;
    inputFields: Array<{
      id: string; key: string; label: string; fieldType: number; isRequired: boolean;
      displayStage: number; isActive: boolean; sortOrder: number;
    }>;
  };
  integrity: {
    duplicateSkus: number;
    productsWithMultipleDefaults: number;
    invalidProductPricing: number;
    invalidVariantPricing: number;
    orphanVariants: number;
    orphanImages: number;
    orphanFeatures: number;
    orphanInputFields: number;
  };
};

export async function getProductState(request: APIRequestContext, slug: string): Promise<ProductState> {
  const response = await request.get(`${apiBaseUrl}/testing/product-state`, { params: { slug } });
  expect(response.ok(), `product-state ${slug}: ${response.status()}`).toBeTruthy();
  return response.json();
}

export function expectCatalogIntegrity(state: ProductState): void {
  expect(state.integrity).toEqual({
    duplicateSkus: 0,
    productsWithMultipleDefaults: 0,
    invalidProductPricing: 0,
    invalidVariantPricing: 0,
    orphanVariants: 0,
    orphanImages: 0,
    orphanFeatures: 0,
    orphanInputFields: 0
  });
}
