// Central tag vocabulary. Apply with Playwright's per-test `tag` option and select with --grep.
//   test('...', { tag: [TAG.smoke, TAG.admin] }, async ({ ... }) => { ... });
//   npx playwright test --grep @smoke
export const TAG = {
  smoke: '@smoke',
  regression: '@regression',
  release: '@release',
  admin: '@admin',
  customer: '@customer',
  business: '@business',
  security: '@security',
  ticket: '@ticket',
  supportDelivery: '@support-delivery',
  instantDelivery: '@instant-delivery',
  manualDelivery: '@manual-delivery',
  wallet: '@wallet',
  coupon: '@coupon',
  seo: '@seo',
  a11y: '@a11y',
  performance: '@performance',
  ui: '@ui'
} as const;

export type Tag = (typeof TAG)[keyof typeof TAG];
