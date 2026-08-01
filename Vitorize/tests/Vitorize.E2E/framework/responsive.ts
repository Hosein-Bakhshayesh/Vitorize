import { expect, type Page, type TestInfo } from '@playwright/test';

export type ResponsivePersona = 'Anonymous' | 'Customer' | 'Admin' | 'SuperAdmin';

export type ResponsiveRoute = {
  route: string;
  path: string;
  component: string;
  persona: ResponsivePersona;
  interactions: string[];
  highRisk?: boolean;
  expectedNotFound?: boolean;
  expectedStatus?: number;
};

const missingId = '31000000-0000-0000-0000-000000000099';
const productId = '31000000-0000-0000-0000-000000000002';

export const responsiveRoutes: ResponsiveRoute[] = [
  { route: '/', path: '/', component: 'Store/Home', persona: 'Anonymous', interactions: ['header', 'banners', 'catalog', 'footer'], highRisk: true },
  { route: '/{*PageRoute}', path: '/responsive-not-found', component: 'Store/NotFoundPage', persona: 'Anonymous', interactions: ['404 state'], expectedNotFound: true },
  { route: '/access-denied', path: '/access-denied', component: 'Store/AccessDenied', persona: 'Anonymous', interactions: ['denied state'] },
  { route: '/blog', path: '/blog', component: 'Store/Blog', persona: 'Anonymous', interactions: ['empty/list state'] },
  { route: '/blog/{Slug}', path: '/blog/responsive-missing', component: 'Store/BlogPost', persona: 'Anonymous', interactions: ['missing state', 'rich content'], expectedNotFound: true },
  { route: '/brand/{Slug}', path: '/brand/e2e-brand', component: 'Store/Brand', persona: 'Anonymous', interactions: ['catalog', 'filter', 'sort'] },
  { route: '/cart', path: '/cart', component: 'Store/Cart', persona: 'Customer', interactions: ['empty cart'], highRisk: true },
  { route: '/categories', path: '/categories', component: 'Store/Categories', persona: 'Anonymous', interactions: ['category cards'] },
  { route: '/category/{Slug}', path: '/category/e2e-category?sort=newest', component: 'Store/Category', persona: 'Anonymous', interactions: ['catalog', 'sort'] },
  { route: '/checkout', path: '/checkout?coupon=RESPONSIVE-LONG-COUPON', component: 'Store/Checkout', persona: 'Customer', interactions: ['empty/filled state', 'coupon', 'payment methods'], highRisk: true },
  { route: '/error', path: '/error', component: 'Store/Status', persona: 'Anonymous', interactions: ['default error'] },
  { route: '/error/{Code}', path: '/error/500', component: 'Store/Status', persona: 'Anonymous', interactions: ['500 error'], expectedStatus: 500 },
  { route: '/faq', path: '/faq', component: 'Store/Faq', persona: 'Anonymous', interactions: ['empty/list state'] },
  { route: '/forgot-password', path: '/forgot-password', component: 'Store/ForgotPassword', persona: 'Anonymous', interactions: ['form', 'validation'] },
  { route: '/login', path: '/login?returnUrl=%2Fcustomer%2Forders&error=responsive-long-validation-message', component: 'Store/Login', persona: 'Anonymous', interactions: ['password tab', 'OTP tab', 'validation'], highRisk: true },
  { route: '/page/{Slug}', path: '/page/about', component: 'Store/StaticPage', persona: 'Anonymous', interactions: ['missing/rich content state'], expectedNotFound: true },
  { route: '/payment/result', path: `/payment/result?orderId=${missingId}&paid=0`, component: 'Store/PaymentResult', persona: 'Anonymous', interactions: ['failed payment', 'recovery actions'] },
  { route: '/product/{Slug}', path: '/product/e2e-seo-product', component: 'Store/Product', persona: 'Anonymous', interactions: ['gallery', 'variants', 'features', 'rich content', 'purchase dialog'], highRisk: true },
  { route: '/register', path: '/register?returnUrl=%2Fcheckout&error=responsive-long-validation-message', component: 'Store/Register', persona: 'Anonymous', interactions: ['form', 'validation'] },
  { route: '/reset-password', path: '/reset-password?mobile=09123456789', component: 'Store/ResetPassword', persona: 'Anonymous', interactions: ['OTP form', 'validation'] },
  { route: '/search', path: '/search?q=E2E%20Dynamic%20Product%20with%20long%20English%20query', component: 'Store/Search', persona: 'Anonymous', interactions: ['results/empty state'] },
  { route: '/shop', path: '/shop?q=E2E%20Dynamic&sort=price-desc', component: 'Store/Shop', persona: 'Anonymous', interactions: ['filters', 'sort', 'catalog'], highRisk: true },

  { route: '/customer/dashboard', path: '/customer/dashboard', component: 'Customer/Dashboard', persona: 'Customer', interactions: ['account navigation', 'summary'], highRisk: true },
  { route: '/customer/gift-codes', path: '/customer/gift-codes', component: 'Customer/GiftCodes', persona: 'Customer', interactions: ['empty/list state', 'reveal/copy'], highRisk: true },
  { route: '/customer/notifications', path: '/customer/notifications', component: 'Customer/Notifications', persona: 'Customer', interactions: ['empty/list state'] },
  { route: '/customer/orders', path: '/customer/orders', component: 'Customer/Orders', persona: 'Customer', interactions: ['empty/list state', 'pagination'], highRisk: true },
  { route: '/customer/orders/{Id:guid}', path: `/customer/orders/${missingId}`, component: 'Customer/OrderDetails', persona: 'Customer', interactions: ['not-found/detail contract'], highRisk: true, expectedNotFound: true },
  { route: '/customer/profile', path: '/customer/profile', component: 'Customer/Profile', persona: 'Customer', interactions: ['form', 'validation'] },
  { route: '/customer/reviews', path: '/customer/reviews', component: 'Customer/Reviews', persona: 'Customer', interactions: ['empty/list state'] },
  { route: '/customer/tickets', path: '/customer/tickets', component: 'Customer/Tickets', persona: 'Customer', interactions: ['empty/list state', 'filters'] },
  { route: '/customer/tickets/{Id:guid}', path: `/customer/tickets/${missingId}`, component: 'Customer/TicketDetails', persona: 'Customer', interactions: ['not-found/detail contract'], highRisk: true, expectedNotFound: true },
  { route: '/customer/tickets/new', path: `/customer/tickets/new?orderId=${missingId}`, component: 'Customer/CreateTicket', persona: 'Customer', interactions: ['form', 'validation'] },
  { route: '/customer/verification', path: '/customer/verification', component: 'Customer/Verification', persona: 'Customer', interactions: ['upload', 'validation', 'status'] },
  { route: '/customer/wallet', path: '/customer/wallet', component: 'Customer/Wallet', persona: 'Customer', interactions: ['balance', 'top-up', 'transactions'] },
  { route: '/customer/wishlist', path: '/customer/wishlist', component: 'Customer/Wishlist', persona: 'Customer', interactions: ['empty/list state', 'product cards'] },

  { route: '/admin/login', path: '/admin/login?returnUrl=%2Fadmin%2Fproducts', component: 'Account/Login', persona: 'Anonymous', interactions: ['form', 'validation'], highRisk: true },
  { route: '/admin/access-denied', path: '/admin/access-denied', component: 'Account/AccessDenied', persona: 'Anonymous', interactions: ['denied state'] },
  { route: '/admin', path: '/admin', component: 'Admin/Dashboard', persona: 'SuperAdmin', interactions: ['alias', 'dashboard'], highRisk: true },
  { route: '/admin/dashboard', path: '/admin/dashboard', component: 'Admin/Dashboard', persona: 'SuperAdmin', interactions: ['cards', 'charts', 'table'], highRisk: true },
  { route: '/admin/audit-logs', path: '/admin/audit-logs', component: 'Admin/AuditLogs', persona: 'SuperAdmin', interactions: ['filters', 'table', 'pagination', 'detail panel'] },
  { route: '/admin/banners', path: '/admin/banners', component: 'Admin/Banners', persona: 'SuperAdmin', interactions: ['cards/table', 'form panel', 'image preview'] },
  { route: '/admin/brands', path: '/admin/brands', component: 'Admin/Brands', persona: 'SuperAdmin', interactions: ['filters', 'table', 'form panel'] },
  { route: '/admin/categories', path: '/admin/categories', component: 'Admin/Categories', persona: 'SuperAdmin', interactions: ['tree/table', 'form panel'] },
  { route: '/admin/coupons', path: '/admin/coupons', component: 'Admin/Coupons', persona: 'SuperAdmin', interactions: ['filters', 'table', 'form panel'] },
  { route: '/admin/error-logs', path: '/admin/error-logs', component: 'Admin/ErrorLogs', persona: 'SuperAdmin', interactions: ['filters', 'long table', 'detail panel'] },
  { route: '/admin/gift-codes', path: '/admin/gift-codes', component: 'Admin/GiftCodes', persona: 'SuperAdmin', interactions: ['tabs', 'tables', 'import dialog'], highRisk: true },
  { route: '/admin/monitoring', path: '/admin/monitoring', component: 'Admin/Monitoring', persona: 'SuperAdmin', interactions: ['health cards', 'links'] },
  { route: '/admin/notifications', path: '/admin/notifications', component: 'Admin/Notifications', persona: 'SuperAdmin', interactions: ['filters', 'table', 'send/detail'] },
  { route: '/admin/orders', path: '/admin/orders', component: 'Admin/Orders', persona: 'SuperAdmin', interactions: ['filters', 'table', 'pagination', 'details'], highRisk: true },
  { route: '/admin/payments', path: '/admin/payments', component: 'Admin/Payments', persona: 'SuperAdmin', interactions: ['filters', 'table', 'detail/refund'], highRisk: true },
  { route: '/admin/products', path: '/admin/products', component: 'Admin/Products', persona: 'SuperAdmin', interactions: ['filters', 'table', 'pagination', 'context menu'], highRisk: true },
  { route: '/admin/products/create', path: '/admin/products/create', component: 'Admin/ProductEdit', persona: 'SuperAdmin', interactions: ['validation', 'CKEditor', 'icon picker'], highRisk: true },
  { route: '/admin/products/{Id:guid}', path: `/admin/products/${productId}`, component: 'Admin/ProductEdit', persona: 'SuperAdmin', interactions: ['all editor sections', 'CKEditor', 'icon picker'], highRisk: true },
  { route: '/admin/products/{Id:guid}/details', path: `/admin/products/${productId}/details`, component: 'Admin/ProductDetails', persona: 'SuperAdmin', interactions: ['details', 'variants', 'features', 'dialogs'], highRisk: true },
  { route: '/admin/products/{Id:guid}/images', path: `/admin/products/${productId}/images`, component: 'Admin/ProductImages', persona: 'SuperAdmin', interactions: ['upload', 'gallery', 'pagination'] },
  { route: '/admin/product-tags', path: '/admin/product-tags', component: 'Admin/ProductTags', persona: 'SuperAdmin', interactions: ['filters', 'table', 'form dialog'] },
  { route: '/admin/reports', path: '/admin/reports', component: 'Admin/Reports', persona: 'SuperAdmin', interactions: ['tabs', 'date range', 'tables'] },
  { route: '/admin/reviews', path: '/admin/reviews', component: 'Admin/Reviews', persona: 'SuperAdmin', interactions: ['filters', 'table', 'moderation'] },
  { route: '/admin/roles', path: '/admin/roles', component: 'Admin/Roles', persona: 'SuperAdmin', interactions: ['roles', 'permissions'] },
  { route: '/admin/security-logs', path: '/admin/security-logs', component: 'Admin/SecurityLogs', persona: 'SuperAdmin', interactions: ['filters', 'long table', 'detail panel'] },
  { route: '/admin/settings', path: '/admin/settings', component: 'Admin/Settings', persona: 'SuperAdmin', interactions: ['17 tabs', '182 settings', 'search', 'icon/upload/color controls'], highRisk: true },
  { route: '/admin/sms', path: '/admin/sms', component: 'Admin/Sms', persona: 'SuperAdmin', interactions: ['statistics', 'table', 'details/send'] },
  { route: '/admin/tickets', path: '/admin/tickets', component: 'Admin/Tickets', persona: 'SuperAdmin', interactions: ['filters', 'table', 'reply/status'], highRisk: true },
  { route: '/admin/tools', path: '/admin/tools', component: 'Admin/Tools', persona: 'SuperAdmin', interactions: ['diagnostics', 'confirm dialog'] },
  { route: '/admin/users', path: '/admin/users', component: 'Admin/Users', persona: 'SuperAdmin', interactions: ['filters', 'table', 'context menu', 'detail panel'] },
  { route: '/admin/verifications', path: '/admin/verifications', component: 'Admin/Verifications', persona: 'SuperAdmin', interactions: ['filters', 'table', 'document/review panel'] },
  { route: '/admin/wallets', path: '/admin/wallets', component: 'Admin/Wallets', persona: 'SuperAdmin', interactions: ['search', 'table', 'transactions/adjustment panel'] }
];

export const fullInventoryProjects = new Set([
  'phone-320-light', 'phone-390-dark', 'tablet-768-light',
  'tablet-820-dark', 'desktop-1440-light', 'desktop-1440-dark'
]);

export const localScrollAllowList = [
  '.vz-table-wrap', '.vz-tabs', '.vz-settabs', '.vz-icon-picker__collections',
  '.vz-icon-picker__categories', '.vz-icon-picker__results', '.ck-toolbar',
  '.st-table-wrap', '.st-catrail', '.st-marquee', '.st-hslider', '.st-trustchips'
];

type AuditContext = { route: ResponsiveRoute; viewport: string; theme: string };

export function installResponsiveMonitor(page: Page) {
  let errors: string[] = [];
  page.on('pageerror', error => errors.push(`pageerror: ${error.message}`));
  page.on('console', message => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });
  page.on('requestfailed', request => {
    const reason = request.failure()?.errorText ?? 'unknown';
    if (!reason.includes('ERR_ABORTED')) errors.push(`requestfailed: ${request.url()} (${reason})`);
  });
  page.on('response', response => {
    if (response.status() >= 500 && new URL(response.url()).hostname === '127.0.0.1') {
      errors.push(`http-${response.status()}: ${response.url()}`);
    }
  });
  return {
    reset: () => { errors = []; },
    read: () => [...errors]
  };
}

export async function auditResponsivePage(page: Page, testInfo: TestInfo, context: AuditContext): Promise<void> {
  const splash = page.locator('#vz-initial-loader');
  if (await splash.count()) await splash.waitFor({ state: 'hidden', timeout: 15_000 });
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');

  const result = await page.evaluate(({ localScrollAllowList }) => {
    const tolerance = 1;
    const root = document.documentElement;
    const locallyScrollable = localScrollAllowList.join(',');
    const ignored = [
      '.vz-sidebar:not(.open)', '[aria-hidden="true"]', '.vz-splash',
      '[class*="aurora"]', '.st-gal__glow', '.vz-chart__tooltip'
    ].join(',');
    const visible = (element: HTMLElement) => {
      const rect = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return rect.width > 0 && rect.height > 0 && style.display !== 'none' &&
        style.visibility !== 'hidden' && style.opacity !== '0' && element.getClientRects().length > 0;
    };
    const selectorFor = (element: HTMLElement) => `${element.tagName.toLowerCase()}${element.id ? `#${element.id}` : ''}${
      element.classList.length ? `.${Array.from(element.classList).join('.')}` : ''}`;
    const elements = Array.from(document.querySelectorAll<HTMLElement>('body *')).filter(visible);
    const offenders = elements.filter(element => {
      if (element.closest(ignored) || element.closest(locallyScrollable)) return false;
      const rect = element.getBoundingClientRect();
      return rect.left < -tolerance || rect.right > window.innerWidth + tolerance;
    }).slice(0, 12).map(element => {
      const rect = element.getBoundingClientRect();
      return { selector: selectorFor(element), left: Math.round(rect.left), right: Math.round(rect.right), width: Math.round(rect.width) };
    });
    const criticalSelector = 'h1,h2,h3,button,label,[role="tab"],[role="dialog"]';
    const clipped = Array.from(document.querySelectorAll<HTMLElement>(criticalSelector)).filter(visible).filter(element => {
      if (element.closest(locallyScrollable) || element.closest(ignored)) return false;
      const style = getComputedStyle(element);
      const clips = ['hidden', 'clip'].includes(style.overflow) || ['hidden', 'clip'].includes(style.overflowX) || ['hidden', 'clip'].includes(style.overflowY);
      return clips && (element.scrollWidth > element.clientWidth + tolerance || element.scrollHeight > element.clientHeight + tolerance);
    }).slice(0, 12).map(element => ({ selector: selectorFor(element), text: (element.innerText || '').trim().slice(0, 80) }));
    const dialogs = Array.from(document.querySelectorAll<HTMLElement>('[role="dialog"]')).filter(visible).map(element => {
      const rect = element.getBoundingClientRect();
      return { selector: selectorFor(element), left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom };
    }).filter(rect => rect.left < -tolerance || rect.right > window.innerWidth + tolerance || rect.top < -tolerance || rect.bottom > window.innerHeight + tolerance);
    return {
      clientWidth: root.clientWidth,
      scrollWidth: root.scrollWidth,
      documentOverflow: root.scrollWidth - root.clientWidth,
      offenders,
      clipped,
      dialogs
    };
  }, { localScrollAllowList });

  const failed = result.documentOverflow > 1 || result.offenders.length > 0 || result.clipped.length > 0 || result.dialogs.length > 0;
  if (failed) {
    await testInfo.attach(`responsive-${context.route.component.replaceAll('/', '-')}-${context.viewport}-${context.theme}`, {
      body: await page.screenshot({ fullPage: true }), contentType: 'image/png'
    });
  }
  const evidence = JSON.stringify({ ...context, result });
  expect.soft(result.documentOverflow, evidence).toBeLessThanOrEqual(1);
  expect.soft(result.offenders, evidence).toEqual([]);
  expect.soft(result.clipped, evidence).toEqual([]);
  expect.soft(result.dialogs, evidence).toEqual([]);
}
