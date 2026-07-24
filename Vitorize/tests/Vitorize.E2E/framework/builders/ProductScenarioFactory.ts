import { ProductBuilder, type ProductInput } from './ProductBuilder';

export type ProductMatrixScenario = {
  key: string;
  product: ProductInput;
  requiresInventory: boolean;
  multipleVariants?: boolean;
};

/** The supported ProductType/DeliveryType configurations expressed as reusable scenario data. */
export class ProductScenarioFactory {
  static supported(runKey: string): ProductMatrixScenario[] {
    const make = (key: string, title: string) => new ProductBuilder(`e2e-matrix-${key}-${runKey}`, title);
    return [
      { key: 'gift-instant-base', requiresInventory: true,
        product: make('gift-instant-base', `Matrix Gift Instant Base ${runKey}`).ofType(1).deliveredBy(1).withoutBrand().priced(101_000).build() },
      { key: 'gift-instant-variants', requiresInventory: true, multipleVariants: true,
        product: make('gift-instant-variants', `Matrix Gift Instant Variants ${runKey}`).ofType(1).deliveredBy(1).priced(102_000, 99_000).build() },
      { key: 'gift-manual', requiresInventory: false,
        product: make('gift-manual', `Matrix Gift Manual ${runKey}`).ofType(1).deliveredBy(2).featured().priced(103_000, 97_000).build() },
      { key: 'account-manual', requiresInventory: false,
        product: make('account-manual', `Matrix Account Manual ${runKey}`).ofType(2).deliveredBy(2).quantities(2, 4).build() },
      { key: 'service-support', requiresInventory: false,
        product: make('service-support', `Matrix Service Support ${runKey}`).ofType(3).deliveredBy(3)
          .withDynamicField({ key: 'service_user', label: 'Service User', required: true }).build() },
      { key: 'subscription-instant', requiresInventory: true,
        product: make('subscription-instant', `Matrix Subscription Instant ${runKey}`).ofType(4).deliveredBy(1)
          .inCategory('E2E Child Category').withFeature('Period', 'One month').build() },
      { key: 'other-manual', requiresInventory: false,
        product: make('other-manual', `Matrix Other Manual ${runKey}`).ofType(99).deliveredBy(2)
          .withDynamicField({ key: 'optional_note', label: 'Optional Note', required: false }).build() }
    ];
  }
}
