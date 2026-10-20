// FW-5 piezo buzzer acceptance — spawn from palette, render the speaker
// body + freq label + port label, verify default config (freq mode) and
// that the props pane exposes the mode toggle with the Output direction
// chip.
//
// See Documentacion/CHANGELOG_visual_widget_FW5_2026-07-05.md for the
// wider feature notes.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

test.describe('FW-5 — piezo buzzer widget', () => {
  test.setTimeout(90_000);

  async function openVisual(page: any, projectName: string) {
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.openProject(projectName);
    const m = page.url().match(/\/editor\/(\d+)/);
    if (!m) throw new Error(`No project id in ${page.url()}`);
    await page.goto(`/visual/${m[1]}`);
    await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
  }

  test('spawns from palette + defaults to freq mode with silent buzzer', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-buzzer-piezo"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });

    await expect.poll(async () => {
      if ((await page.locator('.kviz-buzzer').count()) === 0) await chip.click();
      return await page.locator('.kviz-buzzer').count();
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBeGreaterThan(0);

    const widget = page.locator('.kviz-buzzer').first();
    await expect(widget).toBeVisible();
    await expect(widget).toHaveAttribute('data-mode', 'freq');
    // Default config: no bindings → freq = 0 → inactive.
    await expect(widget).toHaveAttribute('data-active', '0');
    await expect(widget).toHaveAttribute('data-freq', '0');
    // No active class → no pulse rings animating.
    await expect(widget).not.toHaveClass(/kviz-buzzer--active/);
  });

  test('props pane shows mode toggle with output direction chip', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-buzzer-piezo"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });
    await chip.click();
    await expect(page.locator('.kviz-buzzer')).toBeVisible({ timeout: 5_000 });

    await page.locator('.kviz-buzzer').first().click();

    const props = page.locator('.visual-page__props');
    await expect(props).toBeVisible({ timeout: 5_000 });

    // Output direction chip.
    await expect(props.locator('.visual-page__props-dir--out')).toBeVisible();
    // Single mode select (freq/signal).
    await expect(props.locator('select')).toHaveCount(1);
  });
});
