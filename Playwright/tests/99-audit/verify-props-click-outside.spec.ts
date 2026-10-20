// Click-outside behavior for the Visual mode floating props card.
// User expectation: tapping the canvas background dismisses the props
// pane; tapping a different widget switches selection without leaving
// the pane closed.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

test.setTimeout(60_000);

test('Visual props: canvas-background click closes the props pane', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('kmila.tutorialSeen', '1');
    localStorage.setItem('kmila.tutorialSeen.projects', '1');
  });

  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject('Visual-Phase0-Passthrough');
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  if (!idMatch) throw new Error('No project id');
  const projectId = idMatch[1];

  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible();
  await page.waitForLoadState('load');

  const btnPaletteChip = page.getByTestId('visual-palette-button');
  const ledPaletteChip = page.getByTestId('visual-palette-led');
  await expect(btnPaletteChip).toBeEnabled({ timeout: 10_000 });

  await expect.poll(async () => {
    if ((await page.locator('.kviz-button').count()) === 0) await btnPaletteChip.click();
    if ((await page.locator('.kviz-led').count()) === 0) await ledPaletteChip.click();
    return (await page.locator('.kviz-button').count()) > 0
        && (await page.locator('.kviz-led').count()) > 0;
  }, { timeout: 10_000, intervals: [400, 700, 1000] }).toBe(true);

  const propsHeader = page.locator('.visual-page__props-header');
  const propsAside = page.locator('.visual-page__props');
  const canvas = page.locator('.kviz-canvas');

  // 1) Click button → props visible.
  await page.locator('.kviz-button').first().click({ force: true });
  await expect(propsHeader).toBeVisible();

  // 2) Click empty canvas area (bottom-right corner far from widgets) → props gone.
  //    Use the locator's click with an offset so Playwright routes it through
  //    the canvas element (dispatches pointerdown that our Blazor handler needs).
  const box = await canvas.boundingBox();
  if (!box) throw new Error('canvas has no box');
  await canvas.click({ position: { x: box.width - 40, y: box.height - 40 }, force: true });
  await expect(propsAside).toHaveCount(0);

  // 3) Click LED → props back (Output chip).
  await page.locator('.kviz-led').first().click({ force: true });
  await expect(propsHeader).toBeVisible();
  await expect(propsHeader.locator('.visual-page__props-dir--out')).toBeVisible();

  // 4) Click empty canvas again → props gone.
  await canvas.click({ position: { x: box.width - 40, y: box.height - 40 }, force: true });
  await expect(propsAside).toHaveCount(0);

  // 5) Regression from user report: clicks OUTSIDE .kviz-canvas but still
  //    visibly "outside the popover" must also close. Re-select the LED
  //    then click on the palette (which lives outside the canvas).
  await page.locator('.kviz-led').first().click({ force: true });
  await expect(propsHeader).toBeVisible();
  await page.locator('.visual-page__palette-title').click();
  await expect(propsAside).toHaveCount(0);

  // 6) Re-select LED, click on the workspace root well outside the canvas.
  await page.locator('.kviz-led').first().click({ force: true });
  await expect(propsHeader).toBeVisible();
  const workspace = page.locator('.visual-page__workspace');
  const wsBox = await workspace.boundingBox();
  if (!wsBox) throw new Error('workspace has no box');
  await page.mouse.click(wsBox.x + wsBox.width - 10, wsBox.y + wsBox.height - 10);
  await expect(propsAside).toHaveCount(0);
});
