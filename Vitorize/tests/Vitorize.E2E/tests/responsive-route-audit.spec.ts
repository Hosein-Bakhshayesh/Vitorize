import { test, expect } from '../framework/fixtures';
import {
  auditResponsivePage, fullInventoryProjects, installResponsiveMonitor, responsiveRoutes,
  type ResponsivePersona
} from '../framework/responsive';

test.describe('@responsive @rtl @overflow @release complete responsive route inventory', () => {
  test.describe.configure({ timeout: 240_000 });

  for (const persona of ['Anonymous', 'Customer', 'SuperAdmin'] as ResponsivePersona[]) {
    test(`${persona} implemented routes remain responsive @regression`, async ({ page, loginAs }, testInfo) => {
      const monitor = installResponsiveMonitor(page);
      if (persona === 'Customer') await loginAs('Customer');
      if (persona === 'SuperAdmin') await loginAs('SuperAdmin');

      const allForPersona = responsiveRoutes.filter(route => route.persona === persona || (persona === 'Anonymous' && route.persona === 'Admin'));
      const routes = fullInventoryProjects.has(testInfo.project.name)
        ? allForPersona
        : allForPersona.filter(route => route.highRisk);
      expect(routes.length, `No responsive routes selected for ${persona}`).toBeGreaterThan(0);

      for (const route of routes) {
        await test.step(`${route.route} — ${route.component}`, async () => {
          monitor.reset();
          const response = await page.goto(route.path, { waitUntil: 'domcontentloaded' });
          const status = response?.status() ?? 200;
          if (route.expectedStatus) expect.soft(status, `${route.route} returned an unexpected status`).toBe(route.expectedStatus);
          else expect.soft(status, `${route.route} returned a server failure`).toBeLessThan(500);
          await auditResponsivePage(page, testInfo, {
            route,
            viewport: `${page.viewportSize()?.width}x${page.viewportSize()?.height}`,
            theme: testInfo.project.use.colorScheme?.toString() ?? 'light'
          });
          const errors = monitor.read().filter(error => !(
            (route.expectedNotFound && error.includes('server responded with a status of 404')) ||
            (route.expectedStatus === 500 && (error.includes('http-500:') || error.includes('status of 500')))
          ));
          expect.soft(errors, `${route.route} browser/network errors`).toEqual([]);
        });
      }
    });
  }
});
