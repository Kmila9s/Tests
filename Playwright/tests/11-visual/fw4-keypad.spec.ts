// FW-4 keypad acceptance — spawn, render 4×4 default grid, verify props
// pane surfaces the two-chip direction display in raw-scan mode (first
// widget with mixed pin directions).

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

test.describe('FW-4 — matrix keypad widget', () => {
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

  test('spawns from palette + renders 4×4 default grid', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-keypad-matrix"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });

    await expect.poll(async () => {
      if ((await page.locator('.kviz-keypad').count()) === 0) await chip.click();
      return await page.locator('.kviz-keypad').count();
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBeGreaterThan(0);

    const widget = page.locator('.kviz-keypad').first();
    await expect(widget).toBeVisible();
    // Default: decoded mode, 4×4 grid, no key pressed.
    await expect(widget).toHaveAttribute('data-mode', 'decoded');
    await expect(widget).toHaveAttribute('data-rows', '4');
    await expect(widget).toHaveAttribute('data-cols', '4');
    await expect(widget).toHaveAttribute('data-key', '0000');
    await expect(widget).toHaveAttribute('data-pressed', '0');
    await expect(widget.locator('.kviz-keypad__key')).toHaveCount(16);
    await expect(widget.locator('.kviz-keypad__key--pressed')).toHaveCount(0);
  });

  test('key press emits label-hex value (5 → 0101, A → 1010)', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-keypad-matrix"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });
    await chip.click();
    const widget = page.locator('.kviz-keypad').first();
    await expect(widget).toBeVisible({ timeout: 5_000 });

    // Kick the coordinator into run mode so key presses register (edit
    // mode routes them to drag). Assert Stop appears (Run consumed).
    await page.locator('[data-testid="visual-run"]').click();
    await expect(page.locator('[data-testid="visual-stop"]')).toBeVisible({ timeout: 5_000 });

    // Key "5" is the second cell of the second row (col 1, row 1).
    // Label "5" should encode as 0101.
    const key5 = widget.locator('.kviz-keypad__key').nth(5);
    await key5.dispatchEvent('pointerdown');
    await expect(widget).toHaveAttribute('data-key', '0101');
    await expect(widget).toHaveAttribute('data-pressed', '1');
    await key5.dispatchEvent('pointerup');

    // Key "A" is at (row 0, col 3) — cell index 3 in the flat grid.
    // Label "A" should encode as 1010 (hex 0xA).
    const keyA = widget.locator('.kviz-keypad__key').nth(3);
    await keyA.dispatchEvent('pointerdown');
    await expect(widget).toHaveAttribute('data-key', '1010');
    await keyA.dispatchEvent('pointerup');
  });

  test('raw-scan mode shows both direction chips in the props pane', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-keypad-matrix"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });
    await chip.click();
    await expect(page.locator('.kviz-keypad')).toBeVisible({ timeout: 5_000 });

    // Open props by selecting the widget.
    await page.locator('.kviz-keypad').first().click();

    const props = page.locator('.visual-page__props');
    await expect(props).toBeVisible({ timeout: 5_000 });

    // Switch to raw-scan mode so directions actually split.
    await props.locator('select').first().selectOption('raw-scan');

    // Two chips: Input · 4 (rows) and Output · 4 (cols) for the default
    // 4×4 shape.
    await expect(props.locator('.visual-page__props-dir--in')).toBeVisible();
    await expect(props.locator('.visual-page__props-dir--out')).toBeVisible();
  });
});
