import { test, expect } from '@playwright/test';

/**
 * Phase 08 — settings smoke.
 *
 * Theme, text-size and language live in Settings → Apariencia. The two
 * pickers (theme, language) are HTML <details> elements; the test forces
 * .open=true via DOM so the click on an inner option doesn't race with the
 * native disclosure toggle.
 */
test.describe('Settings — Appearance smoke', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/settings');
    await expect(page.locator('.settings-page')).toBeVisible({ timeout: 15_000 });
  });

  test('theme switcher cycles data-bs-theme', async ({ page }) => {
    // The picker is a <details> popover; the 3 option buttons live inside.
    // Blazor mounts/re-mounts the buttons after _theme changes, so wait for
    // them to appear in the DOM before each click.
    await expect
      .poll(async () => page.locator('.kmila-theme-picker__option').count(), {
        timeout: 15_000,
      })
      .toBeGreaterThanOrEqual(3);

    const options = page.locator('.kmila-theme-picker__option');
    await options.nth(1).dispatchEvent('click');
    await expect.poll(
      async () => page.evaluate(() => document.documentElement.getAttribute('data-bs-theme')),
      { timeout: 5_000 },
    ).toBe('dark');

    // After the theme change re-render, re-resolve the locator and click again.
    await expect
      .poll(async () => page.locator('.kmila-theme-picker__option').count(), {
        timeout: 5_000,
      })
      .toBeGreaterThanOrEqual(3);
    await page.locator('.kmila-theme-picker__option').first().dispatchEvent('click');
    await expect.poll(
      async () => page.evaluate(() => document.documentElement.getAttribute('data-bs-theme')),
      { timeout: 5_000 },
    ).toBe('light');
  });

  test('text-size switcher updates --user-text-scale', async ({ page }) => {
    const row = page.locator('.kmila-text-size-row').first();
    await expect(row).toBeVisible();

    const tabs = row.locator('button.kmila-btn-tab');
    // Pick the last one (xl scale = 1.20).
    await tabs.last().click();
    await expect.poll(
      async () =>
        page.evaluate(() =>
          getComputedStyle(document.documentElement).getPropertyValue('--user-text-scale').trim(),
        ),
      { timeout: 5_000 },
    ).toMatch(/^1\.(1|2)/);

    // Reset to md (middle tab).
    const count = await tabs.count();
    await tabs.nth(Math.floor(count / 2)).click();
    await expect.poll(
      async () =>
        page.evaluate(() =>
          getComputedStyle(document.documentElement).getPropertyValue('--user-text-scale').trim(),
        ),
      { timeout: 5_000 },
    ).toMatch(/^(1|0\.9)/);
  });

  test('language picker flips a canary string', async ({ page }) => {
    const projectsLink = page.locator('a[href="/projects"]').first();
    const before = (await projectsLink.innerText()).trim();

    const options = page.locator('.kmila-lang-picker__option');
    // Read badge codes — order matches the rendered list of languages.
    const codes = await page.evaluate(() => {
      return Array.from(
        document.querySelectorAll<HTMLElement>('.kmila-lang-picker__option .kmila-lang-picker__badge'),
      ).map((el) => (el.textContent ?? '').trim().toUpperCase());
    });
    const currentIsEn = before.toLowerCase().includes('project');
    const target = currentIsEn ? 'ES' : 'EN';
    const targetIdx = codes.indexOf(target);
    expect(targetIdx).toBeGreaterThanOrEqual(0);

    await options.nth(targetIdx).dispatchEvent('click');

    await expect
      .poll(async () => (await projectsLink.innerText()).trim(), { timeout: 5_000 })
      .not.toBe(before);
  });
});
