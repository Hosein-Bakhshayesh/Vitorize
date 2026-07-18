// Vitorize Phase 4 load suite (k6).
//
// One script, seven realistic MVP profiles selected with the PROFILE env var. Profiles are
// intentionally conservative because the MVP targets a resource-limited Plesk host: start at the
// baseline and only move up once a stage is stable (see tests/load/README.md).
//
//   k6 run -e PROFILE=baseline -e BASE_URL=http://localhost:5177 vitorize-load.js
//
// Auth/cart/checkout/admin profiles require the Testing environment (fake SMS + fake payment
// providers) and the deterministic seed. Supply credentials with:
//   -e LOGIN_MOBILE=09120000000 -e LOGIN_PASSWORD=... -e ADMIN_MOBILE=... -e ADMIN_PASSWORD=...
//   -e PRODUCT_SLUG=e2e-seo-product -e PRODUCT_ID=<guid>
//
// The fake SMS provider is deterministic, so OTP flows use OTP_CODE (default 11111) rather than a
// real message. Never point this at Production.

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5177').replace(/\/$/, '');
const PROFILE = __ENV.PROFILE || 'baseline';
const PRODUCT_SLUG = __ENV.PRODUCT_SLUG || 'e2e-seo-product';
const PRODUCT_ID = __ENV.PRODUCT_ID || '';
const LOGIN_MOBILE = __ENV.LOGIN_MOBILE || '';
const LOGIN_PASSWORD = __ENV.LOGIN_PASSWORD || '';
const ADMIN_MOBILE = __ENV.ADMIN_MOBILE || '';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || '';
const OTP_CODE = __ENV.OTP_CODE || '11111';

// Custom metrics let the orchestrator report per-class latency and error rates (Part 4).
const errorRate = new Rate('vitorize_errors');
const publicLatency = new Trend('vitorize_public_latency', true);
const authedLatency = new Trend('vitorize_authed_latency', true);
const cartLatency = new Trend('vitorize_cart_latency', true);
const checkoutLatency = new Trend('vitorize_checkout_latency', true);
const adminLatency = new Trend('vitorize_admin_latency', true);
const authFailures = new Counter('vitorize_auth_setup_failures');

// ---- Profiles (Part 2) -----------------------------------------------------------------------
const PROFILES = {
  smoke: { executor: 'constant-vus', vus: 2, duration: '30s', exec: 'publicBrowse' },
  baseline: { executor: 'constant-vus', vus: 3, duration: '5m', exec: 'publicBrowse' },
  normal: { executor: 'constant-vus', vus: 10, duration: '10m', exec: 'mixedBrowse' },
  busy: { executor: 'constant-vus', vus: 25, duration: '10m', exec: 'mixedBrowse' },
  peak: {
    executor: 'ramping-vus', startVUs: 5, exec: 'mixedBrowse',
    stages: [
      { duration: '1m', target: 50 },
      { duration: '3m', target: 50 },
      { duration: '1m', target: 5 },
    ],
  },
  checkout: { executor: 'constant-vus', vus: 15, duration: '3m', exec: 'checkoutFlow' },
  auth: { executor: 'constant-vus', vus: 12, duration: '3m', exec: 'authPressure' },
  admin: { executor: 'constant-vus', vus: 8, duration: '5m', exec: 'adminRead' },
  soak: { executor: 'constant-vus', vus: 8, duration: '25m', exec: 'mixedBrowse' },
};

if (!PROFILES[PROFILE]) {
  throw new Error(`Unknown PROFILE '${PROFILE}'. Valid: ${Object.keys(PROFILES).join(', ')}`);
}

export const options = {
  scenarios: { [PROFILE]: PROFILES[PROFILE] },
  thresholds: {
    // MVP acceptance targets (Part 4). Breaching a threshold fails the run - never widen these to
    // make a run pass; investigate the bottleneck instead.
    vitorize_errors: ['rate<0.01'],
    http_req_failed: ['rate<0.02'],
    vitorize_public_latency: ['p(95)<1000'],
    vitorize_authed_latency: ['p(95)<1500'],
    vitorize_cart_latency: ['p(95)<2000'],
    vitorize_checkout_latency: ['p(95)<3000'],
    vitorize_admin_latency: ['p(95)<1500'],
  },
};

// ---- Helpers ---------------------------------------------------------------------------------
function jsonHeaders(token) {
  const headers = { 'Content-Type': 'application/json', Accept: 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  return headers;
}

function login(mobile, password) {
  if (!mobile || !password) return null;
  const res = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ mobile, password }), {
    headers: jsonHeaders(), tags: { class: 'authed', endpoint: 'login' },
  });
  if (res.status !== 200) return null;
  try {
    const body = res.json();
    return (body && body.data && (body.data.accessToken || body.data.AccessToken)) || null;
  } catch (_) {
    return null;
  }
}

// setup() runs once and shares tokens with every VU so login is not re-measured on every iteration.
export function setup() {
  const customerToken = login(LOGIN_MOBILE, LOGIN_PASSWORD);
  const adminToken = login(ADMIN_MOBILE, ADMIN_PASSWORD);
  if ((LOGIN_MOBILE && !customerToken) || (ADMIN_MOBILE && !adminToken)) authFailures.add(1);
  return { customerToken, adminToken };
}

// ---- Scenario functions ----------------------------------------------------------------------
export function publicBrowse() {
  group('public', () => {
    const routes = [
      '/api/storefront/home',
      '/api/products?page=1&pageSize=20',
      '/api/products?search=game&page=1&pageSize=20',
      '/api/products/featured',
      '/api/products/categories',
      '/api/products/brands',
      `/api/products/slug/${PRODUCT_SLUG}`,
      '/api/settings/public',
      '/api/health',
    ];
    const route = routes[(__ITER + __VU) % routes.length];
    const res = http.get(`${BASE_URL}${route}`, { tags: { class: 'public', endpoint: route } });
    publicLatency.add(res.timings.duration);
    errorRate.add(!check(res, { 'public 2xx/3xx': (r) => r.status >= 200 && r.status < 400 }));
  });
  sleep(0.5);
}

export function mixedBrowse(data) {
  // Realistic 80/20 read/write blend: mostly public browsing with authenticated cart/account reads.
  const roll = (__ITER + __VU) % 10;
  if (roll < 7 || !data.customerToken) {
    publicBrowse();
    return;
  }
  group('authed', () => {
    const routes = ['/api/cart', '/api/auth/me', '/api/wallet', '/api/notifications/unread-count', '/api/orders'];
    const route = routes[(__ITER + __VU) % routes.length];
    const res = http.get(`${BASE_URL}${route}`, {
      headers: jsonHeaders(data.customerToken), tags: { class: 'authed', endpoint: route },
    });
    const trend = route === '/api/cart' ? cartLatency : authedLatency;
    trend.add(res.timings.duration);
    errorRate.add(!check(res, { 'authed 2xx': (r) => r.status >= 200 && r.status < 300 }));
  });
  sleep(0.5);
}

export function authPressure() {
  group('auth', () => {
    const roll = (__ITER + __VU) % 4;
    let res;
    if (roll === 0 && LOGIN_MOBILE) {
      res = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ mobile: LOGIN_MOBILE, password: LOGIN_PASSWORD }),
        { headers: jsonHeaders(), tags: { class: 'authed', endpoint: 'login' } });
      errorRate.add(!check(res, { 'valid login 200': (r) => r.status === 200 }));
    } else if (roll === 1) {
      res = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ mobile: LOGIN_MOBILE || '09120000000', password: 'wrong-password' }),
        { headers: jsonHeaders(), tags: { class: 'authed', endpoint: 'login-invalid' } });
      // Invalid credentials must be rejected (401) - a 200 here would be an auth defect.
      errorRate.add(!check(res, { 'invalid login rejected': (r) => r.status === 401 || r.status === 400 || r.status === 429 }));
    } else if (roll === 2) {
      res = http.post(`${BASE_URL}/api/auth/login/otp/request`, JSON.stringify({ mobile: LOGIN_MOBILE || '09120000000' }),
        { headers: jsonHeaders(), tags: { class: 'authed', endpoint: 'otp-request' } });
      errorRate.add(!check(res, { 'otp request handled': (r) => r.status === 200 || r.status === 429 }));
    } else {
      res = http.post(`${BASE_URL}/api/auth/login/otp/verify`, JSON.stringify({ mobile: LOGIN_MOBILE || '09120000000', code: OTP_CODE }),
        { headers: jsonHeaders(), tags: { class: 'authed', endpoint: 'otp-verify' } });
      errorRate.add(!check(res, { 'otp verify handled': (r) => r.status === 200 || r.status === 400 || r.status === 401 || r.status === 429 }));
    }
    authedLatency.add(res.timings.duration);
  });
  sleep(0.5);
}

export function checkoutFlow(data) {
  if (!data.customerToken || !PRODUCT_ID) {
    // Cannot exercise checkout without a token and a real product id; record and browse instead.
    authFailures.add(1);
    publicBrowse();
    return;
  }
  group('checkout', () => {
    const headers = jsonHeaders(data.customerToken);
    // Add to cart (contention on the same variant / limited stock), then attempt checkout.
    const add = http.post(`${BASE_URL}/api/cart/items`, JSON.stringify({ productId: PRODUCT_ID, quantity: 1 }),
      { headers, tags: { class: 'cart', endpoint: 'add-item' } });
    cartLatency.add(add.timings.duration);
    errorRate.add(!check(add, { 'add item ok': (r) => r.status >= 200 && r.status < 300 }));

    const checkout = http.post(`${BASE_URL}/api/checkout`, JSON.stringify({}),
      { headers, tags: { class: 'checkout', endpoint: 'checkout' } });
    checkoutLatency.add(checkout.timings.duration);
    // Under limited stock, a sold-out rejection (409/400) is a correct outcome, not an error.
    errorRate.add(!check(checkout, { 'checkout resolved': (r) => (r.status >= 200 && r.status < 300) || r.status === 400 || r.status === 409 }));
  });
  sleep(0.5);
}

export function adminRead(data) {
  if (!data.adminToken) {
    authFailures.add(1);
    publicBrowse();
    return;
  }
  group('admin', () => {
    const routes = ['/api/admin/dashboard', '/api/admin/orders', '/api/admin/products', '/api/admin/payments', '/api/admin/monitoring'];
    const route = routes[(__ITER + __VU) % routes.length];
    const res = http.get(`${BASE_URL}${route}`, {
      headers: jsonHeaders(data.adminToken), tags: { class: 'admin', endpoint: route },
    });
    adminLatency.add(res.timings.duration);
    errorRate.add(!check(res, { 'admin 2xx': (r) => r.status >= 200 && r.status < 300 }));
  });
  sleep(0.75);
}
