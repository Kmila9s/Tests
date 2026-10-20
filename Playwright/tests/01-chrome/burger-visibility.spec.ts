import { test, expect } from '@playwright/test';
import { TopBar } from '../../pageobjects/TopBar';

/**
 * Regression: the topbar burger button must be hidden on desktop and visible
 * on mobile. The bug was that pages.css's `.kmila-topbar__menu-btn { display: none }`
 * had the same specificity as buttons.css's `.kmila-btn-icon { display: inline-flex }`
 * and was loaded earlier, so the inline-flex won and the burger leaked to desktop —
 * clicking it then opened the mobile drawer and covered all content.
 *
 * Fix: chain the classes (`.kmila-btn-icon.kmila-topbar__menu-btn`) to bump
 * specificity above the base button rule independent of load order.
 */
test.describe('TopBar burger visibility', () => {
  test('hidden on desktop viewport (1440x900)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/');
    const topbar = new TopBar(page);
    await expect(topbar.root).toBeVisible();
    await expect(topbar.burger).toBeHidden();
  });

  test('hidden at the desktop breakpoint boundary (641x900)', async ({ page }) => {
    await page.setViewportSize({ width: 641, height: 900 });
    await page.goto('/');
    const topbar = new TopBar(page);
    await expect(topbar.burger).toBeHidden();
  });

  test('visible at mobile width (390x844, iPhone-ish)', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    const topbar = new TopBar(page);
    await expect(topbar.burger).toBeVisible();
  });

  test('visible at the mobile breakpoint boundary (640x900)', async ({ page }) => {
    await page.setViewportSize({ width: 640, height: 900 });
    await page.goto('/');
    const topbar = new TopBar(page);
    await expect(topbar.burger).toBeVisible();
  });
});
