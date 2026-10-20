import { Locator, Page } from '@playwright/test';

/**
 * BlazorBootstrap ConfirmDialog appears as `.modal.show` with two action
 * buttons. The "Yes" (destructive) button carries the color passed into
 * `ShowAsync` — Danger → .btn-danger, Secondary → .btn-secondary, etc.
 *
 * Identifying by color class lets us match without depending on the UI
 * language (titles/labels vary by locale).
 */
export class ConfirmDialog {
  readonly root: Locator;
  readonly title: Locator;
  readonly body: Locator;
  readonly confirmDanger: Locator;
  readonly cancelSecondary: Locator;

  constructor(readonly page: Page) {
    this.root = page.locator('.modal.show').last();
    this.title = this.root.locator('.modal-title');
    this.body = this.root.locator('.modal-body');
    this.confirmDanger = this.root.locator('button.btn-danger');
    this.cancelSecondary = this.root.locator('button.btn-secondary');
  }
}
