// Canvas locator + zoom-to-fit — §6 Item 4.
//
// Verifies the two new toolbar affordances:
//   1) Fit-to-view button calls kmilaPanZoomFit(id) which drives the
//      pan-zoom lib's internal fitToView.
//   2) Locator popover lists placed widgets; clicking Focus centers the
//      pan-zoom transform on that widget's position via kmilaPanZoomFocus.
//
// Full-view assertion: the pan-zoom transform on .visual-page__canvas-wrap
// must change after each action (initial identity vs. post-fit vs.
// post-focus). Exact matrix values are engine-dependent so we only guard
// against the "no-op" regression.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

async function openVisual(page: any, projectName: string) {
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject(projectName);
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  if (!idMatch) throw new Error(`No id in ${page.url()}`);
  const projectId = idMatch[1];
  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
  await page.waitForLoadState('networkidle');
  return projectId;
}

async function wrapTransform(page: any): Promise<string> {
  return await page.locator('.visual-page__canvas-wrap').first().evaluate((el: HTMLElement) =>
    el.style.transform || '');
}

test.describe('Visual canvas locator + fit', () => {
  test.setTimeout(120_000);

  test('kmilaPanZoomFit + kmilaPanZoomFocus are exposed', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const exposed = await page.evaluate(() => ({
      fit:   typeof (window as any).kmilaPanZoomFit === 'function',
      focus: typeof (window as any).kmilaPanZoomFocus === 'function',
    }));
    expect(exposed.fit).toBe(true);
    expect(exposed.focus).toBe(true);
  });

  test('Fit-to-view changes the wrap transform', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase1-ToggleLed').projectName);

    const swBtn = page.locator('[data-testid="visual-palette-switch"]');
    await expect(swBtn).toBeEnabled({ timeout: 10_000 });
    await swBtn.click();

    // Zoom in first so fit is a visible change from a non-identity transform.
    const zoomIn = page.locator('[data-testid="visual-zoom-in"]');
    await zoomIn.click();
    await zoomIn.click();
    const before = await wrapTransform(page);

    const fitBtn = page.locator('[data-testid="visual-zoom-fit"]');
    await expect(fitBtn).toBeVisible();
    await fitBtn.click();

    // Poll for the transform to change — the JS call is via JSInterop
    // and the render commit is async.
    await expect.poll(async () => (await wrapTransform(page)), {
      timeout: 5_000, intervals: [200, 400],
    }).not.toBe(before);
  });

  test('Locator popover opens and lists widgets; Focus updates transform', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase1-ToggleLed').projectName);

    const swBtn = page.locator('[data-testid="visual-palette-switch"]');
    await expect(swBtn).toBeEnabled({ timeout: 10_000 });
    await swBtn.click();

    const toggle = page.locator('[data-testid="visual-locator-toggle"]');
    await expect(toggle).toBeVisible();
    await toggle.click();

    const panel = page.locator('[data-testid="visual-locator-panel"]');
    await expect(panel).toBeVisible({ timeout: 5_000 });

    const item = panel.locator('[data-testid^="visual-locator-focus-"]').first();
    await expect(item).toBeVisible();

    const before = await wrapTransform(page);
    await item.click();

    // Focus dispatches a JSInterop call which mutates the transform.
    await expect.poll(async () => (await wrapTransform(page)), {
      timeout: 5_000, intervals: [200, 400],
    }).not.toBe(before);
  });
});
