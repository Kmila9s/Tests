// Phase 3++ button-bounce verification.
//
// Confirms:
//   1. Adding a button widget to the canvas shows a "Simulate contact
//      bounce" checkbox in the properties pane.
//   2. Toggling the checkbox + saving persists "bounce": true into the
//      .kviz config (verified via the canvas's port-label state — when
//      the property is set, the props pane reflects it on re-select).
//
// Doesn't verify the actual chatter playback (that needs a wired-and-
// running canvas + waveform inspection — covered by SimulatorBench
// regression). The UI presence + persistence is what this spec asserts.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

test('§Bounce — checkbox visible + toggleable on a selected Button widget', async ({ page }) => {
  test.setTimeout(60_000);
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject(fixtureByName('Visual-Phase0-Passthrough').projectName);
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  if (!idMatch) throw new Error(`No id in ${page.url()}`);
  await page.goto(`/visual/${idMatch[1]}`);
  await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });

  // Add a button widget via the toolbar.
  const addBtn = page.locator('[data-testid="visual-add-button"]');
  await expect(addBtn).toBeEnabled({ timeout: 10_000 });
  await expect.poll(async () => {
    if ((await page.locator('.kviz-button').count()) === 0) await addBtn.click();
    return (await page.locator('.kviz-button').count()) > 0;
  }, { timeout: 10_000 }).toBe(true);

  // Click the button widget to select it (in edit mode the click goes
  // to the canvas's selection handler).
  await page.locator('.kviz-button').last().click();

  // Properties pane should now show the bounce checkbox (locale-agnostic
  // via the `.visual-page__props-checkbox` class on the wrapper label).
  const bounceLabel = page.locator('.visual-page__props-checkbox');
  await expect(bounceLabel).toBeVisible({ timeout: 5_000 });

  // Checkbox starts unchecked.
  const checkbox = bounceLabel.locator('input[type="checkbox"]');
  await expect(checkbox).not.toBeChecked();

  // Toggle on.
  await checkbox.check();
  await expect(checkbox).toBeChecked();

  // Save the kviz so the bounce flag persists.
  await page.locator('[data-testid="visual-save"]').click();
  await expect(page.locator('[data-testid="visual-save"]')).toBeEnabled({ timeout: 3_000 });

  // Reload the visual page — the bounce flag should hydrate from the
  // saved .kviz and the checkbox should come back checked.
  await page.reload();
  await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
  // The reloaded canvas auto-loads the saved button. Click it to select.
  await page.locator('.kviz-button').last().click();
  await expect(page.locator('.visual-page__props-checkbox input[type="checkbox"]')).toBeChecked({ timeout: 5_000 });
});
