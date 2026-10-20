// Phase 1 one-shot helper: pre-wire and save the ToggleLed and Counter4
// canvases so the tablet visual walk doesn't have to drive the native
// dropdown picker on Android.
//
// Usage:  npx playwright test tests/11-visual/p1_wire_and_save.spec.ts

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

async function openVisualByName(page: any, projectName: string) {
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject(projectName);
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  if (!idMatch) throw new Error('No project id');
  const projectId = idMatch[1];
  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
  await page.waitForLoadState('networkidle');
  return projectId;
}

test('seed Counter4: LED array wired to count, saved', async ({ page }) => {
  test.setTimeout(60_000);
  await page.addInitScript(() => {
    localStorage.setItem('kmila.tutorialSeen', '1');
    localStorage.setItem('kmila.tutorialSeen.projects', '1');
  });

  await openVisualByName(page, 'Visual-Phase1-Counter4');

  // Session 30 (2026-07-24) — wait for the project load to settle
  // before clicking the palette chip. Previously the click could fire
  // before LoadProjectAsync's snapshot capture completed, so the
  // spawn's MarkDirty() was overwritten by the load's post-load
  // snapshot include-spawned-widget path → _dirty=false → Save
  // stayed disabled. See STATUS §11 bug 3.
  await page.waitForLoadState('networkidle');
  const addBtn = page.locator('[data-testid="visual-palette-led-array"]');
  await expect(addBtn).toBeEnabled({ timeout: 10_000 });
  await addBtn.click();
  await expect(page.locator('.kviz-led-array')).toHaveCount(1, { timeout: 5_000 });

  // Wire array.anodes → count, then Save.
  const wireBtn = page.locator('[data-testid="visual-wire"]');
  const modal = page.locator('.kviz-wiring-modal');
  await expect.poll(async () => {
    if (!(await modal.isVisible().catch(() => false))) await wireBtn.click();
    return modal.isVisible();
  }, { timeout: 10_000 }).toBe(true);
  await modal.locator('.kviz-wiring-table tbody tr').filter({
    has: page.locator('code', { hasText: 'anodes' }),
  }).last().locator('select').selectOption('count');
  await page.locator('[data-testid="visual-wiring-done"]').click();
  await expect(modal).toBeHidden();

  await page.locator('[data-testid="visual-save"]').click();
  await expect(page.locator('[data-testid="visual-save"]')).toBeDisabled({ timeout: 5_000 });
});

test('seed ToggleLed: Switch + LED, wired and saved', async ({ page }) => {
  test.setTimeout(60_000);
  await page.addInitScript(() => {
    localStorage.setItem('kmila.tutorialSeen', '1');
    localStorage.setItem('kmila.tutorialSeen.projects', '1');
  });

  await openVisualByName(page, 'Visual-Phase1-ToggleLed');

  const swBtn = page.locator('[data-testid="visual-palette-switch"]');
  const ledBtn = page.locator('[data-testid="visual-palette-led"]');
  await expect(swBtn).toBeEnabled({ timeout: 10_000 });

  await expect.poll(async () => {
    if ((await page.locator('.kviz-switch').count()) === 0) await swBtn.click();
    if ((await page.locator('.kviz-led').count()) === 0) await ledBtn.click();
    return (await page.locator('.kviz-switch').count()) > 0
      && (await page.locator('.kviz-led').count()) > 0;
  }, { timeout: 10_000, intervals: [300, 500, 800] }).toBe(true);

  const wireBtn = page.locator('[data-testid="visual-wire"]');
  const modal = page.locator('.kviz-wiring-modal');
  await expect.poll(async () => {
    if (!(await modal.isVisible().catch(() => false))) await wireBtn.click();
    return modal.isVisible();
  }, { timeout: 10_000 }).toBe(true);

  const rows = modal.locator('.kviz-wiring-table tbody tr');
  await rows.filter({ has: page.locator('code', { hasText: 'out' }) }).last().locator('select').selectOption('sw');
  await rows.filter({ has: page.locator('code', { hasText: 'anode' }) }).last().locator('select').selectOption('lamp');
  await page.locator('[data-testid="visual-wiring-done"]').click();
  await expect(modal).toBeHidden();

  await expect(page.locator('.kviz-switch-port').last()).toHaveText('sw');
  await expect(page.locator('.kviz-led-label').last()).toHaveText('lamp');

  await page.locator('[data-testid="visual-save"]').click();
  await expect(page.locator('[data-testid="visual-save"]')).toBeDisabled({ timeout: 5_000 });
});
