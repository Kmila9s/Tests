import { Locator, Page } from '@playwright/test';

/**
 * TopBar — the sticky app header (Kmila.Shared/Layout/TopBar.razor).
 *
 * Source of truth for the burger button visibility rule:
 *   - desktop (≥ 641px): hidden
 *   - mobile  (≤ 640px): visible
 * Gated by `.kmila-btn-icon.kmila-topbar__menu-btn` in pages.css.
 */
export class TopBar {
  readonly root: Locator;
  readonly burger: Locator;
  readonly breadcrumbs: Locator;
  readonly profileMenuTrigger: Locator;

  constructor(readonly page: Page) {
    this.root = page.locator('header.kmila-topbar');
    this.burger = this.root.locator('.kmila-topbar__menu-btn');
    this.breadcrumbs = this.root.locator('.kmila-breadcrumbs');
    this.profileMenuTrigger = this.root.locator('.kmila-topbar__end').last();
  }
}
