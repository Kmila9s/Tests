import { test } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const OUT = '/tmp/kmila_ui_audit';
fs.mkdirSync(OUT, { recursive: true });

const TOUR_KEYS = [
  'kmila.tutorialSeen',
  'kmila.tutorialSeen.home',
  'kmila.tutorialSeen.projects',
  'kmila.tutorialSeen.editor',
  'kmila.tutorialSeen.learn',
  'kmila.tutorialSeen.concepts',
  'kmila.tutorialSeen.practice',
  'kmila.tutorialSeen.settings',
];

async function seedDismissedTour(page) {
  await page.addInitScript((keys) => {
    try { for (const k of keys) localStorage.setItem(k, '1'); } catch {}
  }, TOUR_KEYS);
}

const WIDTHS = [
  { label: 'wide',   w: 1440, h: 900 },
  { label: 'medium', w:  900, h: 800 },
  { label: 'mobile', w:  375, h: 812 },
];

test.describe.configure({ mode: 'serial' });

for (const dim of WIDTHS) {
  test(`editor empty @ ${dim.label}`, async ({ page }) => {
    await page.setViewportSize({ width: dim.w, height: dim.h });
    await seedDismissedTour(page);
    await page.goto('/projects', { waitUntil: 'networkidle' });
    await page.waitForTimeout(600);
    // Click the first project tile
    const firstTile = page.locator('.project-card, .kmila-project-card, a[href^="/editor/"]').first();
    if (await firstTile.count() > 0) {
      await firstTile.click();
      await page.waitForURL(/\/editor\//);
      await page.waitForTimeout(1500); // monaco bootstrap
    }
    await page.screenshot({ path: path.join(OUT, `AUDIT_editor_${dim.label}.png`), fullPage: false });
  });

  test(`editor DARK @ ${dim.label}`, async ({ page }) => {
    await page.setViewportSize({ width: dim.w, height: dim.h });
    await seedDismissedTour(page);
    await page.addInitScript(() => {
      try { localStorage.setItem('kmila-theme', 'dark'); } catch {}
    });
    await page.goto('/projects', { waitUntil: 'networkidle' });
    await page.waitForTimeout(600);
    const firstTile = page.locator('.project-card, .kmila-project-card, a[href^="/editor/"]').first();
    if (await firstTile.count() > 0) {
      await firstTile.click();
      await page.waitForURL(/\/editor\//);
      await page.waitForTimeout(1500);
    }
    await page.screenshot({ path: path.join(OUT, `AUDIT_DARK_editor_${dim.label}.png`), fullPage: false });
  });
}

// Drawer + profile-menu open states
test('drawer open @ mobile', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await seedDismissedTour(page);
  await page.goto('/', { waitUntil: 'networkidle' });
  await page.waitForTimeout(600);
  const burger = page.locator('.kmila-topbar__menu-btn').first();
  await burger.click();
  await page.waitForTimeout(400);
  await page.screenshot({ path: path.join(OUT, 'AUDIT_drawer_mobile.png'), fullPage: false });
});

test('profile menu open @ wide', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await seedDismissedTour(page);
  await page.goto('/', { waitUntil: 'networkidle' });
  await page.waitForTimeout(600);
  const avatar = page.locator('.kmila-profile, .profile-menu, button[aria-label*="profile" i]').first();
  if (await avatar.count() > 0) {
    await avatar.click();
    await page.waitForTimeout(400);
  }
  await page.screenshot({ path: path.join(OUT, 'AUDIT_profile_wide.png'), fullPage: false });
});

// Create File modal
test('create file modal @ wide', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await seedDismissedTour(page);
  await page.goto('/projects', { waitUntil: 'networkidle' });
  await page.waitForTimeout(600);
  const createBtn = page.getByRole('button', { name: /crear nuevo proyecto|create new project|nuevo proyecto/i }).first();
  if (await createBtn.count() > 0) {
    await createBtn.click();
    await page.waitForTimeout(500);
  }
  await page.screenshot({ path: path.join(OUT, 'AUDIT_modal_create_project_wide.png'), fullPage: false });
});
