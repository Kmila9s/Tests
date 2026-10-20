// One-shot helper: opens the passthrough fixture, places LED + Button,
// wires both to btn/led, and saves the .kviz sidecar. Subsequent tablet
// visits to /visual/386 load this state directly so the user (or tablet
// adb tap sequence) can skip the dropdown-picker dance.
//
// Run on demand:
//   npx playwright test tests/11-visual/wire_and_save.spec.ts

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

test('seed wired+saved passthrough canvas', async ({ page }) => {
  test.setTimeout(60_000);

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
  await page.waitForLoadState('networkidle');

  // Defensive add. If a prior wired+saved run persisted widgets, this is
  // idempotent — Wire modal will simply rebind the existing pair.
  const ledBtn = page.locator('[data-testid="visual-palette-led"]');
  const btnBtn = page.locator('[data-testid="visual-palette-button"]');
  await expect(ledBtn).toBeEnabled({ timeout: 10_000 });
  await expect.poll(async () => {
    if ((await page.locator('.kviz-led').count()) === 0) await ledBtn.click();
    if ((await page.locator('.kviz-button').count()) === 0) await btnBtn.click();
    return (await page.locator('.kviz-led').count()) > 0
      && (await page.locator('.kviz-button').count()) > 0;
  }, { timeout: 10_000, intervals: [300, 500, 800] }).toBe(true);

  // Open Wire modal.
  const wireBtn = page.locator('[data-testid="visual-wire"]');
  const modal = page.locator('.kviz-wiring-modal');
  await expect.poll(async () => {
    if (!(await modal.isVisible().catch(() => false))) await wireBtn.click();
    return modal.isVisible();
  }, { timeout: 10_000, intervals: [300, 500, 800] }).toBe(true);

  // Bind both pins, close, then Save.
  const rows = modal.locator('.kviz-wiring-table tbody tr');
  await rows.filter({ has: page.locator('code', { hasText: 'out' }) }).last().locator('select').selectOption('btn');
  await rows.filter({ has: page.locator('code', { hasText: 'anode' }) }).last().locator('select').selectOption('led');
  await page.locator('[data-testid="visual-wiring-done"]').click();
  await expect(modal).toBeHidden();

  // Confirm canvas labels updated, then click Save.
  await expect(page.locator('.kviz-button-port').last()).toHaveText('btn');
  await expect(page.locator('.kviz-led-label').last()).toHaveText('led');

  await page.locator('[data-testid="visual-save"]').click();
  // Save toggles to "Saved" (no asterisk) on success.
  await expect(page.locator('[data-testid="visual-save"]')).toBeDisabled({ timeout: 5_000 });
});
