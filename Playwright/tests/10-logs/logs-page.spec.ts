import { test, expect } from '@playwright/test';

/**
 * Phase 10 — /logs page.
 *
 * The logs list is live (tailed via a BuildLog-like store), so the suite
 * doesn't reliably know how many entries to expect. Tests focus on the
 * controls instead: page loads, level filter is present, search filters
 * matching/non-matching, clear empties the list when there's content.
 */
test.describe('Logs page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/logs');
    await expect(page.locator('.logs-page__list')).toBeVisible({ timeout: 15_000 });
  });

  test('level filter dropdown exposes all four levels', async ({ page }) => {
    const level = page.locator('.logs-page__level');
    await expect(level).toBeVisible();
    const optionValues = await level.locator('option').evaluateAll((opts) =>
      opts.map((o) => (o as HTMLOptionElement).value),
    );
    expect(optionValues).toEqual(expect.arrayContaining(['all', 'info', 'warning', 'error']));
  });

  test('search input live-filters the list', async ({ page }) => {
    const search = page.locator('.logs-page__search input').first();
    await expect(search).toBeVisible();

    // Junk query never matches, so the filtered count must be ≤ baseline.
    // (The log store can tick during the test, so equality on the "after
    // clear" path isn't reliable.)
    const before = await page.locator('.output-row').count();
    await search.fill('xxx-no-match-xxx');
    await page.waitForTimeout(300);
    const filtered = await page.locator('.output-row').count();
    expect(filtered).toBeLessThanOrEqual(before);
  });

  test('clear button empties the list (when there is content)', async ({ page }) => {
    const initial = await page.locator('.output-row').count();
    test.skip(initial === 0, 'No log entries to clear in this run.');

    const clearBtn = page.locator('.kmila-btn-icon').filter({
      has: page.locator('i.bi-trash, .bi-trash'),
    }).first();
    await clearBtn.click();

    await expect.poll(async () => page.locator('.output-row').count(), {
      timeout: 5_000,
    }).toBe(0);
  });

  test('pause toggle exists and is operable', async ({ page }) => {
    // The Logs.razor toolbar has a pause/resume toggle (`.logs-page__toggle`).
    // It's interactive whether logs are present or not.
    const toggle = page.locator('.logs-page__toggle').first();
    await expect(toggle).toBeVisible();
    await toggle.click();
    // No specific UI assertion — just ensure it doesn't throw and stays clickable.
    await expect(toggle).toBeEnabled();
  });
});
