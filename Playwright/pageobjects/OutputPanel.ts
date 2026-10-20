import { expect, Locator, Page } from '@playwright/test';

export class OutputPanel {
  readonly root: Locator;
  readonly rows: Locator;
  readonly successRows: Locator;
  readonly dangerRows: Locator;
  readonly warningRows: Locator;
  readonly infoRows: Locator;

  constructor(readonly page: Page) {
    this.root = page.locator('.output-panel');
    this.rows = this.root.locator('li.output-row');
    this.successRows = this.root.locator('li.output-row--success');
    this.dangerRows = this.root.locator('li.output-row--danger');
    this.warningRows = this.root.locator('li.output-row--warning');
    this.infoRows = this.root.locator('li.output-row--info');
  }

  /**
   * Build-end can race with Editor.razor's auto-tab-switch (success → Entities,
   * error → Output). Two-step approach:
   *   1. Wait for the dock's active panel class to be the same two consecutive
   *      reads ~500ms apart — i.e. the build-end SetActive has landed.
   *   2. Click Output and poll for the desired row.
   */
  private async pollFor(variantClass: string, timeoutMs: number) {
    const readActive = () =>
      this.page.evaluate(() => {
        const panel = document.querySelector('.kmila-dock__panel');
        return panel
          ? Array.from(panel.classList).find((c) => c.startsWith('kmila-dock__panel--')) ?? null
          : null;
      });

    await expect
      .poll(
        async () => {
          const a = await readActive();
          await this.page.waitForTimeout(400);
          const b = await readActive();
          return a !== null && a === b;
        },
        { timeout: Math.min(timeoutMs, 10_000), intervals: [200, 400, 800] },
      )
      .toBe(true);

    await expect
      .poll(
        async () => {
          await this.page
            .locator('#dock-tab-Output')
            .dispatchEvent('click')
            .catch(() => 0);
          await this.page.waitForTimeout(250);
          return await this.page.evaluate((cls) => {
            return document.querySelectorAll(`li.${cls}`).length;
          }, variantClass);
        },
        { timeout: timeoutMs, intervals: [500, 1000, 2000] },
      )
      .toBeGreaterThan(0);
  }

  async waitForSuccess(timeoutMs = 60_000) {
    await this.pollFor('output-row--success', timeoutMs);
  }

  async waitForError(timeoutMs = 60_000) {
    await this.pollFor('output-row--danger', timeoutMs);
  }
}
