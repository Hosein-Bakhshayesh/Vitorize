import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const base = __ENV.BASE_URL || 'http://127.0.0.1:5077';
const failures = new Rate('vitorize_failures');
const routeLatency = new Trend('vitorize_route_latency', true);

export const options = {
  scenarios: {
    public_browse: { executor: 'constant-vus', vus: 8, duration: '30s' },
    sitemap_burst: { executor: 'constant-arrival-rate', rate: 3, timeUnit: '1s', duration: '20s', preAllocatedVUs: 3, maxVUs: 8, startTime: '5s' }
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    vitorize_failures: ['rate<0.01'],
    http_req_duration: ['p(95)<1500', 'p(99)<3000']
  }
};

const routes = ['/', '/shop', '/product/e2e-seo-product', '/search?q=e2e', '/sitemaps/products-1.xml'];

export default function () {
  const route = routes[(__ITER + __VU) % routes.length];
  const response = http.get(`${base}${route}`, { redirects: 0, tags: { route } });
  const ok = check(response, { 'status is successful': r => r.status >= 200 && r.status < 400 });
  failures.add(!ok);
  routeLatency.add(response.timings.duration, { route });
  sleep(0.25);
}
