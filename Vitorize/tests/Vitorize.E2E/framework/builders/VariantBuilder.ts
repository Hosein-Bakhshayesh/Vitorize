export type VariantInput = {
  title: string;
  sku?: string;
  value?: string;
  price: number;
  discountPrice?: number;
  stockMode: number;
  isDefault: boolean;
  active: boolean;
  sortOrder: number;
};

/** Fluent test-data builder for variants created through the Admin product editor. */
export class VariantBuilder {
  private readonly value: VariantInput;

  constructor(title: string, sku?: string) {
    this.value = {
      title,
      sku,
      value: title.toLowerCase().replaceAll(' ', '-'),
      price: 150_000,
      stockMode: 3,
      isDefault: false,
      active: true,
      sortOrder: 10
    };
  }

  priced(price: number, discountPrice?: number): this { this.value.price = price; this.value.discountPrice = discountPrice; return this; }
  default(): this { this.value.isDefault = true; return this; }
  inactive(): this { this.value.active = false; return this; }
  sorted(sortOrder: number): this { this.value.sortOrder = sortOrder; return this; }
  stockMode(stockMode: number): this { this.value.stockMode = stockMode; return this; }
  valued(value: string): this { this.value.value = value; return this; }
  build(): VariantInput { return structuredClone(this.value); }
}
