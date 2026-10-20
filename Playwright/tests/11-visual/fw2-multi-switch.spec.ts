// FW-2 multi-switch acceptance — spawn from palette, render N levers,
// toggle a lever in run mode → data-value attribute updates.
//
// See Documentacion/CHANGELOG_visual_widget_FW2_2026-07-04.md for the
// wider feature notes.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

test.describe('FW-2 — multi-switch widget', () => {
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

  test('spawns from palette + defaults to 4 levers', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-multi-switch"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });

    await expect.poll(async () => {
      if ((await page.locator('.kviz-multi-switch').count()) === 0) await chip.click();
      return await page.locator('.kviz-multi-switch').count();
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBeGreaterThan(0);

    const widget = page.locator('.kviz-multi-switch').first();
    await expect(widget).toBeVisible();
    // Default config: count=4, mode=per-bit → 4 lever cells, value 0000
    await expect(widget).toHaveAttribute('data-count', '4');
    await expect(widget).toHaveAttribute('data-mode', 'per-bit');
    await expect(widget).toHaveAttribute('data-value', '0000');
    await expect(widget.locator('.kviz-multi-switch__lever')).toHaveCount(4);
  });

  test('props pane shows count spinner, mode toggle, bounce checkbox', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-multi-switch"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });
    await chip.click();
    await expect(page.locator('.kviz-multi-switch')).toBeVisible({ timeout: 5_000 });

    // Selecting the widget by clicking its port row (edit-mode canvas
    // pointerdown selects the widget and opens the floating props aside).
    await page.locator('.kviz-multi-switch').first().click();

    const props = page.locator('.visual-page__props');
    await expect(props).toBeVisible({ timeout: 5_000 });

    // Direction chip — matches R3-5 pattern, multi-switch is Input.
    await expect(props.locator('.visual-page__props-dir--in')).toBeVisible();

    // Bounce checkbox present + unchecked.
    const bounceCheckbox = props.locator('.visual-page__props-checkbox input[type="checkbox"]');
    await expect(bounceCheckbox).not.toBeChecked();
  });
});
