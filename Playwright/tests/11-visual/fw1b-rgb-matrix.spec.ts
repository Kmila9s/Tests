// FW-1b RGB LED matrix (3×3) acceptance — spawn from palette, render the
// 3×3 grid with all cells off, verify props pane exposes the planes/raw
// mode toggle and brightness slider with the Output direction chip.
//
// See Documentacion/CHANGELOG_visual_widget_FW1b_2026-07-05.md for the
// wider feature notes.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

test.describe('FW-1b — 3×3 RGB LED matrix widget', () => {
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

  test('spawns from palette + renders 3×3 grid, all planes zero at rest', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-matrix-3x3-rgb"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });

    await expect.poll(async () => {
      if ((await page.locator('.kviz-matrix-3x3-rgb').count()) === 0) await chip.click();
      return await page.locator('.kviz-matrix-3x3-rgb').count();
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBeGreaterThan(0);

    const widget = page.locator('.kviz-matrix-3x3-rgb').first();
    await expect(widget).toBeVisible();
    // Default config: mode=planes; all bits zero at rest.
    await expect(widget).toHaveAttribute('data-mode', 'planes');
    await expect(widget).toHaveAttribute('data-red',   '000000000');
    await expect(widget).toHaveAttribute('data-green', '000000000');
    await expect(widget).toHaveAttribute('data-blue',  '000000000');
    // 9 cells, none on at rest.
    await expect(widget.locator('.kviz-matrix-3x3-rgb__cell')).toHaveCount(9);
    await expect(widget.locator('.kviz-matrix-3x3-rgb__cell--on')).toHaveCount(0);
  });

  test('props pane shows planes/raw toggle + brightness with output direction chip', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-matrix-3x3-rgb"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });
    await chip.click();
    await expect(page.locator('.kviz-matrix-3x3-rgb')).toBeVisible({ timeout: 5_000 });

    await page.locator('.kviz-matrix-3x3-rgb').first().click();

    const props = page.locator('.visual-page__props');
    await expect(props).toBeVisible({ timeout: 5_000 });

    // Output direction chip.
    await expect(props.locator('.visual-page__props-dir--out')).toBeVisible();

    // Mode select + brightness range.
    await expect(props.locator('select')).toHaveCount(1);
    await expect(props.locator('input[type="range"]')).toHaveCount(1);
  });
});
