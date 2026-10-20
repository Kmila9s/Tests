import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

const LIB_PATH = path.resolve(__dirname, '../../data/lib/test_lib.vhd');

/**
 * Phase 09 — bundles + library import/view/delete.
 *
 * Bundle export needs a project to pick; we create one in beforeAll-ish
 * fashion inside the export test. Library tests stand alone.
 */
test.describe('Settings — bundle export + library lifecycle', () => {
  test.setTimeout(180_000);

  test('bundle export downloads a .kmila JSON file', async ({ page }) => {
    // 1. Create a project to export.
    const projectName = `Bundle-${Date.now()}`;
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.createProject(projectName, 'Bundle export source.');

    // 2. Settings → bundle picker → export.
    await page.goto('/settings');
    await expect(page.locator('.settings-page')).toBeVisible({ timeout: 15_000 });

    // The bundle export select is the one inside .settings-card__actions whose
    // <option> list includes our project. Pick it by visible label.
    const exportSelect = page.locator('select.settings-card__select').first();
    await exportSelect.selectOption({ label: projectName });

    const exportBtn = page.locator('button.kmila-btn--primary', { hasText: /export|exportar/i }).first();
    await expect(exportBtn).toBeEnabled({ timeout: 5_000 });

    const [download] = await Promise.all([
      page.waitForEvent('download', { timeout: 30_000 }),
      exportBtn.click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/\.kmila$/i);

    const savePath = await download.path();
    expect(savePath).toBeTruthy();
    const text = fs.readFileSync(savePath!, 'utf8');
    // Content is JSON, parseable, and at least mentions the project name.
    const parsed = JSON.parse(text);
    expect(typeof parsed).toBe('object');
    expect(text).toContain(projectName);

    // Cleanup
    await projects.goto();
    await projects.removeProject(projectName, 'yes');
  });

  /**
   * Self-contained: import → view → delete in one flow. Test isolation
   * between import/view/delete is brittle because the user-assets directory
   * persists between tests, so we run the whole library lifecycle as a single
   * test that always starts from a known state.
   */
  test('library lifecycle: import → view source → delete', async ({ page }) => {
    await page.goto('/settings');
    await expect(page.locator('.settings-page')).toBeVisible({ timeout: 15_000 });

    const fileName = path.basename(LIB_PATH);
    const row = page.locator('.settings-list__item').filter({
      has: page.locator('.settings-list__name', { hasText: fileName }),
    });

    // Start from a clean slate: if the library is already present from a
    // previous run, remove it via the trash button.
    if (await row.isVisible().catch(() => false)) {
      await row.locator('button').filter({ has: page.locator('.bi-trash') }).first().click();
      const confirm = page.locator('.modal.show button.btn-danger').first();
      if (await confirm.isVisible({ timeout: 1_000 }).catch(() => false)) {
        await confirm.click();
      }
      await expect(row).toBeHidden({ timeout: 5_000 });
    }

    // Import — the library file input is the only one accepting .vhd files.
    const libInput = page.locator('input[type="file"][accept*=".vhd"]').first();
    await libInput.setInputFiles(LIB_PATH);
    await expect(row).toBeVisible({ timeout: 10_000 });

    // View source — eye icon opens a modal with `.library-source-view`.
    await row.locator('button').filter({ has: page.locator('.bi-eye') }).first().click();
    const modal = page.locator('.modal.show').last();
    await expect(modal).toBeVisible({ timeout: 5_000 });
    const view = modal.locator('.library-source-view, pre').first();
    await expect(view).toBeVisible();
    await expect(view).toContainText('kmila_test_lib');

    // Dismiss modal.
    await modal.locator('button.btn-close, button[aria-label="Close"]').first()
      .click({ timeout: 2_000 })
      .catch(async () => { await page.keyboard.press('Escape'); });
    await expect(page.locator('.modal.show')).toHaveCount(0, { timeout: 5_000 });

    // Delete via trash button + (optional) confirm dialog.
    await row.locator('button').filter({ has: page.locator('.bi-trash') }).first().click();
    const confirm = page.locator('.modal.show button.btn-danger').first();
    if (await confirm.isVisible({ timeout: 1_000 }).catch(() => false)) {
      await confirm.click();
    }
    await expect(row).toBeHidden({ timeout: 10_000 });
  });
});
