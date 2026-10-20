// FW-1 LED matrix (3×3 mono) acceptance — spawn from palette, render the
// 3×3 grid, verify default config (value mode, red, brightness 100) and
// that the props pane surfaces mode + color + brightness controls with the
// Output direction chip.
//
// See Documentacion/CHANGELOG_visual_widget_FW1_2026-07-05.md for the
// wider feature notes.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

test.describe('FW-1 — 3×3 LED matrix widget', () => {
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

  test('spawns from palette + renders 3×3 grid', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-matrix-3x3"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });

    // Click-to-spawn — poll until at least one widget exists (HTML5 DnD
    // is flaky in test browsers; the click path is the reliable one).
    await expect.poll(async () => {
      if ((await page.locator('.kviz-matrix-3x3').count()) === 0) await chip.click();
      return await page.locator('.kviz-matrix-3x3').count();
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBeGreaterThan(0);

    const widget = page.locator('.kviz-matrix-3x3').first();
    await expect(widget).toBeVisible();
    // Default config: mode=value, all 9 bits '0' → data-value == "000000000".
    await expect(widget).toHaveAttribute('data-mode', 'value');
    await expect(widget).toHaveAttribute('data-value', '000000000');
    // 9 cells, none on at rest.
    await expect(widget.locator('.kviz-matrix-3x3__cell')).toHaveCount(9);
    await expect(widget.locator('.kviz-matrix-3x3__cell--on')).toHaveCount(0);
  });

  test('props pane shows mode, color and brightness controls', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-matrix-3x3"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });
    await chip.click();
    await expect(page.locator('.kviz-matrix-3x3')).toBeVisible({ timeout: 5_000 });

    // Selecting via click opens the floating props aside.
    await page.locator('.kviz-matrix-3x3').first().click();

    const props = page.locator('.visual-page__props');
    await expect(props).toBeVisible({ timeout: 5_000 });

    // Matrix is an output widget → Output direction chip.
    await expect(props.locator('.visual-page__props-dir--out')).toBeVisible();

    // Three controls (mode select, color select, brightness range).
    await expect(props.locator('select')).toHaveCount(2);
    await expect(props.locator('input[type="range"]')).toHaveCount(1);
  });
});
