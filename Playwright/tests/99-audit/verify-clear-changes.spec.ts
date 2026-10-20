// R3-6 Sub-task B — Clear-changes verification.
// Covers: revert to last-saved state on click, button disabled while
// clean, revert itself is undoable (push to undo stack before restore).

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

test.setTimeout(60_000);

test('R3-6 B: clear-changes reverts unsaved edits', async ({ page }) => {
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
  await expect(page.getByTestId('visual-palette-button')).toBeEnabled({ timeout: 15_000 });
  await page.waitForTimeout(400);

  const saveBtn = page.getByTestId('visual-save');
  const clearBtn = page.getByTestId('visual-clear-changes');
  const undoBtn = page.getByTestId('visual-undo');

  // Guarantee a saved baseline. If _dirty on load (Sanitize ran), save
  // first so we start clean. If _dirty stays true after the poll, that
  // means the fixture's on-disk state is out of sync and we should
  // ship the pending sanitize.
  if (!(await saveBtn.isDisabled())) {
    await saveBtn.click();
    await expect(saveBtn).toBeDisabled();
  }

  // Clear button is disabled while clean.
  await expect(clearBtn).toBeDisabled();

  // Add a widget → both Save and Clear enable.
  const btnCount0 = await page.locator('.kviz-button').count();
  await page.getByTestId('visual-palette-button').click();
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0 + 1);
  await expect(saveBtn).toBeEnabled();
  await expect(clearBtn).toBeEnabled();

  // Click Clear → widget removed, Save shows "Saved", Clear disabled.
  await clearBtn.click();
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0);
  await expect(saveBtn).toBeDisabled();
  await expect(clearBtn).toBeDisabled();

  // Toast appears.
  await expect(page.locator('.kmila-toast, .toast').filter({ hasText: /discarded|descartad/i })
    .first()).toBeVisible({ timeout: 5_000 });

  // Clear itself is undoable — Undo brings the widget back.
  await expect(undoBtn).toBeEnabled();
  await undoBtn.click();
  await expect.poll(() => page.locator('.kviz-button').count()).toBe(btnCount0 + 1);
});
