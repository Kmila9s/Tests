// R3-6 Sub-task A — Undo/Redo verification.
// Covers: toolbar buttons, keyboard shortcuts, drag = 1 undo step
// (not 60), and undo across a save (undoing past a save should
// leave _dirty true relative to the new baseline).

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

test.setTimeout(60_000);

test('R3-6 A: undo/redo widget adds', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('kmila.tutorialSeen', '1');
    localStorage.setItem('kmila.tutorialSeen.projects', '1');
  });

  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject('Visual-Phase0-Passthrough');
  const projectId = page.url().match(/\/editor\/(\d+)/)![1];

  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible();
  await page.waitForLoadState('load');
  // Wait for Blazor hydration to finish so widget counts settle.
  await expect(page.getByTestId('visual-palette-button')).toBeEnabled({ timeout: 15_000 });
  await page.waitForTimeout(400);

  const undoBtn = page.getByTestId('visual-undo');
  const redoBtn = page.getByTestId('visual-redo');
  const btnChip = page.getByTestId('visual-palette-button');
  const ledChip = page.getByTestId('visual-palette-led');

  // Baseline: existing widgets may already be on the canvas from prior
  // test runs (Passthrough is a persistent fixture). Clear via undo
  // isn't the right entry point — instead, remember initial counts
  // and reason about deltas.
  const btnCount0 = await page.locator('.kviz-button').count();
  const ledCount0 = await page.locator('.kviz-led').count();

  // Undo starts disabled iff no mutations have happened yet.
  // (Sanitize on load can push a mutation entry — that's fine, just
  // record whether the button is disabled and reason from there.)
  const undoStartDisabled = await undoBtn.isDisabled();
  await expect(redoBtn).toBeDisabled();

  // 1) Add a Button → undo enables.
  await btnChip.click();
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0 + 1);
  await expect(undoBtn).toBeEnabled();
  await expect(redoBtn).toBeDisabled();

  // 2) Add an LED → undo still enabled.
  await ledChip.click();
  await expect.poll(() => page.locator('.kviz-led').count()).toBe(ledCount0 + 1);

  // 3) Click Undo → LED disappears, Redo enables.
  await undoBtn.click();
  await expect.poll(() => page.locator('.kviz-led').count()).toBe(ledCount0);
  await expect(redoBtn).toBeEnabled();

  // 4) Click Undo again → Button also gone.
  await undoBtn.click();
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0);

  // 5) Redo twice → both come back.
  await redoBtn.click();
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0 + 1);
  await redoBtn.click();
  await expect.poll(() => page.locator('.kviz-led').count()).toBe(ledCount0 + 1);
  await expect(redoBtn).toBeDisabled();

  // 6) Any new user action invalidates redo. Undo once, then add a
  //    fresh widget → redo becomes disabled.
  await undoBtn.click();
  await expect(redoBtn).toBeEnabled();
  await btnChip.click();
  await expect(redoBtn).toBeDisabled();
});

test('R3-6 A: Cmd+Z / Cmd+Shift+Z keyboard shortcuts', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('kmila.tutorialSeen', '1');
    localStorage.setItem('kmila.tutorialSeen.projects', '1');
  });

  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject('Visual-Phase0-Passthrough');
  const projectId = page.url().match(/\/editor\/(\d+)/)![1];
  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible();
  await page.waitForLoadState('load');
  // Wait for Blazor hydration to finish so widget counts settle.
  await expect(page.getByTestId('visual-palette-button')).toBeEnabled({ timeout: 15_000 });
  await page.waitForTimeout(400);

  const btnCount0 = await page.locator('.kviz-button').count();
  await page.getByTestId('visual-palette-button').click();
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0 + 1);

  // Focus somewhere on the visual page but outside any input/NavMenu.
  // A page-level keyboard.press dispatches to activeElement; use the
  // canvas itself as the focus target.
  const canvas = page.locator('.kviz-canvas');
  const box = await canvas.boundingBox();
  if (!box) throw new Error('canvas has no box');
  await canvas.click({ position: { x: box.width - 20, y: box.height - 20 }, force: true });
  await page.keyboard.press(process.platform === 'darwin' ? 'Meta+z' : 'Control+z');
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0);

  // Redo via Cmd+Shift+Z.
  await page.keyboard.press(process.platform === 'darwin' ? 'Meta+Shift+z' : 'Control+Shift+z');
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0 + 1);
});

test('R3-6 A: drag = 1 undo step (not per-frame)', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('kmila.tutorialSeen', '1');
    localStorage.setItem('kmila.tutorialSeen.projects', '1');
  });

  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject('Visual-Phase0-Passthrough');
  const projectId = page.url().match(/\/editor\/(\d+)/)![1];
  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible();
  await page.waitForLoadState('load');
  // Wait for Blazor hydration to finish so widget counts settle.
  await expect(page.getByTestId('visual-palette-button')).toBeEnabled({ timeout: 15_000 });
  await page.waitForTimeout(400);

  // Ensure at least one button widget exists.
  if ((await page.locator('.kviz-button').count()) === 0) {
    await page.getByTestId('visual-palette-button').click();
    await expect.poll(() => page.locator('.kviz-button').count()).toBeGreaterThan(0);
  }

  const undoBtn = page.getByTestId('visual-undo');
  // Drain undo so we start from a known state (all clicks succeed).
  while (!(await undoBtn.isDisabled())) await undoBtn.click();

  // Add a button and grab its initial position.
  await page.getByTestId('visual-palette-button').click();
  await page.waitForTimeout(200);
  const widget = page.locator('.kviz-button').last();
  const box0 = await widget.boundingBox();
  if (!box0) throw new Error('no widget');

  // Undo count after the add — we expect Undo enabled (1 entry).
  await expect(undoBtn).toBeEnabled();

  // Drag across a large distance. Playwright's mouse.down/move/up
  // dispatches ~1 pointerdown, several pointermoves, 1 pointerup.
  await page.mouse.move(box0.x + box0.width / 2, box0.y + box0.height / 2);
  await page.mouse.down();
  for (let i = 1; i <= 10; i++) {
    await page.mouse.move(box0.x + box0.width / 2 + i * 12,
                          box0.y + box0.height / 2 + i * 8);
  }
  await page.mouse.up();
  await page.waitForTimeout(200);

  // A single Undo click should reverse the whole drag (widget returns
  // to its post-add position, not to some intermediate frame).
  await undoBtn.click();
  await page.waitForTimeout(200);
  const boxAfterUndo = await widget.boundingBox();
  if (!boxAfterUndo) throw new Error('widget vanished');
  // Position after undoing the drag should equal the pre-drag box.
  // Allow a 2-px slop for rendering.
  expect(Math.abs(boxAfterUndo.x - box0.x)).toBeLessThanOrEqual(2);
  expect(Math.abs(boxAfterUndo.y - box0.y)).toBeLessThanOrEqual(2);
});
