export type ProductFeatureInput = {
  title: string;
  value: string;
  active?: boolean;
};

export type ProductDynamicFieldInput = {
  key: string;
  label: string;
  fieldType?: number;
  placeholder?: string;
  required?: boolean;
  active?: boolean;
  displayStage?: number;
};

export type ProductInput = {
  title: string;
  slug: string;
  category: string;
  brand?: string;
  productType: number;
  deliveryType: number;
  basePrice: number;
  discountPrice?: number;
  currencyType: number;
  minQuantity: number;
  maxQuantity?: number;
  active: boolean;
  featured: boolean;
  shortDescription?: string;
  htmlDescription?: string;
  seoTitle?: string;
  seoDescription?: string;
  focusKeyword?: string;
  thumbnailAlt?: string;
  tagTitles: string[];
  features: ProductFeatureInput[];
  dynamicFields: ProductDynamicFieldInput[];
};

/** Fluent test-data builder for products created through the real Admin editor. */
export class ProductBuilder {
  private readonly value: ProductInput;

  constructor(slug: string, title = `Matrix ${slug}`) {
    this.value = {
      title,
      slug,
      category: 'E2E Category',
      brand: 'E2E Brand',
      productType: 1,
      deliveryType: 2,
      basePrice: 120_000,
      currencyType: 2,
      minQuantity: 1,
      active: true,
      featured: false,
      shortDescription: `Deterministic storefront description for ${slug}.`,
      seoTitle: `${title} SEO`,
      seoDescription: `Deterministic SEO description for ${slug}.`,
      focusKeyword: 'matrix product',
      tagTitles: [],
      features: [],
      dynamicFields: []
    };
  }

  titled(title: string): this { this.value.title = title; return this; }
  inCategory(title: string): this { this.value.category = title; return this; }
  withoutBrand(): this { delete this.value.brand; return this; }
  withBrand(title: string): this { this.value.brand = title; return this; }
  ofType(productType: number): this { this.value.productType = productType; return this; }
  deliveredBy(deliveryType: number): this { this.value.deliveryType = deliveryType; return this; }
  priced(basePrice: number, discountPrice?: number): this {
    this.value.basePrice = basePrice;
    this.value.discountPrice = discountPrice;
    return this;
  }
  inCurrency(currencyType: number): this { this.value.currencyType = currencyType; return this; }
  quantities(min: number, max?: number): this { this.value.minQuantity = min; this.value.maxQuantity = max; return this; }
  inactive(): this { this.value.active = false; return this; }
  featured(): this { this.value.featured = true; return this; }
  described(shortDescription: string, htmlDescription?: string): this {
    this.value.shortDescription = shortDescription;
    this.value.htmlDescription = htmlDescription;
    return this;
  }
  seo(title: string, description: string, keyword = 'matrix product'): this {
    this.value.seoTitle = title;
    this.value.seoDescription = description;
    this.value.focusKeyword = keyword;
    return this;
  }
  tagged(...titles: string[]): this { this.value.tagTitles = titles; return this; }
  withFeature(title: string, value: string, active = true): this {
    this.value.features.push({ title, value, active });
    return this;
  }
  withDynamicField(field: ProductDynamicFieldInput): this {
    this.value.dynamicFields.push(field);
    return this;
  }
  build(): ProductInput {
    return structuredClone(this.value);
  }
}
