import { expect, test, expectRtlAndNoOverflow } from '../framework/fixtures';
import { monitorBrowser } from './support/app';
import { createHash } from 'node:crypto';
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

const documentTypeId = '31000000000000000000000000000044';
const requiredScenarios = {
  'desktop-light': { mobile: '09120000031', item: '38000000-0000-0000-0000-000000000071', owner: '33000000000000000000000000000071' },
  'desktop-dark': { mobile: '09120000032', item: '38000000-0000-0000-0000-000000000072', owner: '33000000000000000000000000000072' },
  'mobile-light': { mobile: '09120000033', item: '38000000-0000-0000-0000-000000000073', owner: '33000000000000000000000000000073' },
  'mobile-dark': { mobile: '09120000034', item: '38000000-0000-0000-0000-000000000074', owner: '33000000000000000000000000000074' }
} as const;

test.describe('FIX-09 Phase 3A client-side document redaction @fix09p3a', () => {
  test.describe.configure({ timeout: 120_000 });

  test('Required mode keeps the source local, stores the generated PNG, and masks the intended source pixels', async ({ page, consoleGuard }, testInfo) => {
    const monitor = monitorBrowser(page);
    const scenario = requiredScenarios[testInfo.project.name as keyof typeof requiredScenarios];
    await page.setViewportSize(testInfo.project.name.startsWith('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
    await login(page, scenario.mobile);
    await page.goto(`/customer/verification?orderItem=${scenario.item}`, { waitUntil: 'networkidle' });
    const source = page.getByTestId(`redaction-source-${documentTypeId}`);
    await expect(source).toBeAttached();
    const original = await deterministicExifJpeg(page);
    await armGeneratedFileCapture(page);
    const ownerStorage = `D:\\Vitorize\\Vitorize\\Vitorize.Api\\App_Data\\PrivateDocuments\\${scenario.owner}`;
    const filesBefore = new Set(await readdir(ownerStorage));

    // This picker is deliberately not an InputFile/Blazor upload control. Until
    // Confirm dispatches a generated File to the hidden InputFile, count stays 0.
    await selectForRedaction(page, original.base64);
    await expect(page.getByRole('dialog', { name: 'ویرایش و پوشاندن اطلاعات حساس مدرک' })).toBeVisible();
    await expectGeneratedCount(page, 0);
    await page.getByRole('button', { name: 'انصراف بدون آپلود' }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(source).toHaveValue('');
    await expectGeneratedCount(page, 0);

    await selectForRedaction(page, original.base64);
    const canvas = page.locator('.vz-redaction-modal__canvas');
    await expect(canvas).toBeVisible();
    const box = await canvas.boundingBox();
    expect(box).not.toBeNull();
    // The source target is x=520..1080/y=220..760. Drawing uses visual canvas
    // coordinates; stored-pixel assertions below use source coordinates.
    await page.mouse.move(box!.x + box!.width * .32, box!.y + box!.height * .18);
    await page.mouse.down();
    await page.mouse.move(box!.x + box!.width * .72, box!.y + box!.height * .78);
    await page.mouse.up();
    await expect(page.getByRole('button', { name: 'حذف ناحیه پوشانده‌شده 1' })).toBeVisible();
    await page.getByRole('button', { name: 'تأیید و آپلود نسخه پوشانده‌شده' }).click();
    await expectGeneratedCount(page, 1);
    await expect.poll(async () => (await readdir(ownerStorage)).filter(file => !filesBefore.has(file)).length).toBe(1);
    const newFile = (await readdir(ownerStorage)).find(file => !filesBefore.has(file))!;
    const storedBytes = await readFile(join(ownerStorage, newFile));
    const storedHash = createHash('sha256').update(storedBytes).digest('hex');
    const evidence = await page.evaluate(async (encoded) => {
      const captured = (window as any).__fix09RedactionCapture;
      const stored = Uint8Array.from(atob(encoded), c => c.charCodeAt(0));
      const blob = new Blob([stored], { type: 'image/png' });
      const bitmap = await createImageBitmap(blob);
      const output = document.createElement('canvas'); output.width = bitmap.width; output.height = bitmap.height;
      const context = output.getContext('2d')!; context.drawImage(bitmap, 0, 0);
      const pixel = (x: number, y: number) => Array.from(context.getImageData(x, y, 1, 1).data);
      return {
        generatedCount: captured.count, generatedMagic: captured.magic, generatedHash: captured.hash,
        storedMagic: Array.from(stored.slice(0, 8)),
        masked: pixel(800, 500), control: pixel(120, 120), width: bitmap.width, height: bitmap.height,
        containsExif: new TextDecoder('latin1').decode(stored).includes('Exif'),
        containsGpsCanary: new TextDecoder('latin1').decode(stored).includes('FIX09-FAKE-GPS-CANARY')
      };
    }, storedBytes.toString('base64'));

    expect(evidence.generatedCount).toBe(1);
    expect(evidence.generatedMagic).toEqual([137, 80, 78, 71, 13, 10, 26, 10]);
    expect(evidence.storedMagic).toEqual([137, 80, 78, 71, 13, 10, 26, 10]);
    expect(storedHash).toBe(evidence.generatedHash);
    expect(storedHash).not.toBe(original.hash);
    expect(evidence.masked).toEqual([17, 24, 39, 255]); // opaque #111827 export mask
    expect(evidence.control[1]).toBeGreaterThan(80);
    expect(evidence.control[0]).toBeLessThan(110);
    expect(evidence.control[2]).toBeLessThan(110);
    expect(evidence.width).toBe(1600); expect(evidence.height).toBe(1000);
    expect(evidence.containsExif).toBeFalsy(); expect(evidence.containsGpsCanary).toBeFalsy();
    await expectRtlAndNoOverflow(page);
    monitor.assertClean();
    consoleGuard.assertClean();
  });
});

async function login(page: import('@playwright/test').Page, mobile: string) {
  const password = process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';
  await page.goto('/login');
  await page.locator('#pw-mobile').fill(mobile);
  await page.locator('#pw-pass').fill(password);
  await Promise.all([page.waitForURL(/\/customer\/dashboard/), page.locator('form[action="/auth/customer/login"] button[type="submit"]').click()]);
}

async function deterministicExifJpeg(page: import('@playwright/test').Page): Promise<{ base64: string; hash: string }> {
  return page.evaluate(async () => {
    const canvas = document.createElement('canvas'); canvas.width = 1600; canvas.height = 1000;
    const context = canvas.getContext('2d')!;
    context.fillStyle = '#16a34a'; context.fillRect(0, 0, 360, 300); // unaffected control
    context.fillStyle = '#dc2626'; context.fillRect(520, 220, 560, 540); // known target
    context.fillStyle = '#f8fafc'; context.font = 'bold 42px sans-serif'; context.fillText('FIX09-UNREDACTED-CANARY', 570, 500);
    const jpeg = new Uint8Array(await (await new Promise<Blob>(resolve => canvas.toBlob(blob => resolve(blob!), 'image/jpeg', .95))).arrayBuffer());
    const exif = new TextEncoder().encode('Exif\0\0FAKE-CAMERA=FIX09;GPS=FIX09-FAKE-GPS-CANARY');
    const result = new Uint8Array(jpeg.length + exif.length + 4);
    result.set(jpeg.slice(0, 2)); result[2] = 0xff; result[3] = 0xe1;
    const length = exif.length + 2; result[4] = length >> 8; result[5] = length & 255;
    result.set(exif, 6); result.set(jpeg.slice(2), 6 + exif.length);
    const hash = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', result))).map(x => x.toString(16).padStart(2, '0')).join('');
    return { base64: btoa(String.fromCharCode(...result)), hash };
  });
}

async function armGeneratedFileCapture(page: import('@playwright/test').Page) {
  await page.evaluate(() => {
    const input = document.querySelector<HTMLInputElement>('[data-testid^="redaction-flattened-"]')!;
    (window as any).__fix09RedactionCapture = { count: 0, hash: '', magic: [] };
    input.addEventListener('change', async () => {
      const bytes = new Uint8Array(await input.files![0].arrayBuffer());
      const hash = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))).map(x => x.toString(16).padStart(2, '0')).join('');
      (window as any).__fix09RedactionCapture = { count: (window as any).__fix09RedactionCapture.count + 1, hash, magic: Array.from(bytes.slice(0, 8)) };
    });
  });
}

async function expectGeneratedCount(page: import('@playwright/test').Page, count: number) {
  await expect.poll(() => page.evaluate(() => (window as any).__fix09RedactionCapture.count)).toBe(count);
}

async function selectForRedaction(page: import('@playwright/test').Page, base64: string) {
  const fileChooser = page.waitForEvent('filechooser');
  await page.getByTestId(`redaction-open-${documentTypeId}`).click();
  await (await fileChooser).setFiles({ name: 'fix09-exif-source.jpg', mimeType: 'image/jpeg', buffer: Buffer.from(base64, 'base64') });
}
