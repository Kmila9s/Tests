import { expect, Locator, Page } from '@playwright/test';
import { FilesCard } from './FilesCard';

export class EditorPage {
  readonly root: Locator;
  readonly workbench: Locator;
  readonly files: FilesCard;
  readonly dockTabs: Locator;

  constructor(readonly page: Page) {
    this.root = page.locator('.editor-page');
    this.workbench = page.locator('.editor-workbench');
    this.files = new FilesCard(page);
    this.dockTabs = page.locator('.kmila-dock__tab');
  }

  async waitForReady() {
    await expect(this.root).toBeVisible({ timeout: 15_000 });
    await expect(this.workbench).toBeVisible();
  }

  async openDockTab(tabId: 'Entities' | 'Schematic' | 'Flow' | 'Ports' | 'Waveforms' | 'Runs' | 'Files' | 'Output') {
    const tab = this.page.locator(`#dock-tab-${tabId}`);
    const panel = this.page.locator(`.kmila-dock__panel--${tabId.toLowerCase()}`);
    // Editor.razor auto-switches the dock during/after build (success →
    // Entities, error → Output). waitForBuildIdle can return BEFORE that
    // auto-switch lands, so a single click on our target may be undone.
    // Poll: keep re-dispatching the click until the tab stays .is-active
    // for two consecutive checks (~600ms separation).
    await expect
      .poll(
        async () => {
          await tab.dispatchEvent('click').catch(() => 0);
          await this.page.waitForTimeout(200);
          const a = await tab.evaluate((el) => el.classList.contains('is-active'));
          await this.page.waitForTimeout(400);
          const b = await tab.evaluate((el) => el.classList.contains('is-active'));
          return a && b;
        },
        { timeout: 15_000, intervals: [500, 800] },
      )
      .toBe(true);
    await expect(panel).toBeVisible({ timeout: 5_000 });
  }
}
