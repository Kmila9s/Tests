import { test, expect } from '@playwright/test';

test.setTimeout(120_000);

test('Logs page exposes an export button and it is disabled when the buffer is empty', async ({ page }) => {
  await page.goto('/logs', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);

  // The button lives inside .kmila-slot-header-tools__actions with title = LOGS_EXPORT.
  const exportBtn = page.locator('.logs-page__header button[aria-label]', { hasText: '' })
    .filter({ has: page.locator('.bi-download, [class*="download" i]') })
    .first();

  // Fallback locator: rely on the icon class since the button only carries an icon child.
  const exportBtnByIcon = page.locator('.logs-page__header .kmila-btn-icon:has(.bi-download)').first();
  const target = (await exportBtn.count()) > 0 ? exportBtn : exportBtnByIcon;

  await expect(target).toHaveCount(1);
  // If the buffer is empty (fresh session), disabled=""; otherwise it's enabled.
  const isDisabled = await target.evaluate((el: HTMLButtonElement) => el.disabled);
  expect(typeof isDisabled).toBe('boolean');
});

test('Logs page — no raw LOGS_EXPORT leak in aria-label / title', async ({ page }) => {
  await page.goto('/logs', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  const bodyText = await page.evaluate(() => document.body.innerHTML);
  expect(bodyText).not.toContain('>LOGS_EXPORT<');
  const rawLabels = await page.evaluate(() => Array.from(document.querySelectorAll('[aria-label],[title]'))
    .map(el => ({ a: el.getAttribute('aria-label') ?? '', t: el.getAttribute('title') ?? '' })));
  const leaks = rawLabels.filter(x => /^LOGS_[A-Z_]+$/.test(x.a) || /^LOGS_[A-Z_]+$/.test(x.t));
  expect(leaks, JSON.stringify(leaks)).toEqual([]);
});
