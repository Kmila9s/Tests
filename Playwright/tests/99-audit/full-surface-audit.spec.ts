import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const OUT = '/tmp/kmila_ui_audit';
fs.mkdirSync(OUT, { recursive: true });

const WIDTHS = [
  { label: 'wide',   w: 1440, h: 900 },
  { label: 'medium', w:  900, h: 800 },
  { label: 'tablet', w:  640, h: 900 },
  { label: 'mobile', w:  375, h: 812 },
];

const PAGES = [
  { route: '/',         name: 'home' },
  { route: '/projects', name: 'projects' },
  { route: '/learn',    name: 'learn' },
  { route: '/concepts', name: 'concepts' },
  { route: '/practice', name: 'practice' },
  { route: '/settings', name: 'settings' },
  { route: '/privacy',  name: 'privacy' },
  { route: '/logs',     name: 'logs' },
];

test.describe.configure({ mode: 'serial' });

// Pre-mark every tour key as seen so the welcome coachmark doesn't cover
// every screenshot. The keys mirror kmila-tutorial.js's known route list.
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

for (const dim of WIDTHS) {
  for (const p of PAGES) {
    test(`${p.name} @ ${dim.label} (${dim.w}x${dim.h})`, async ({ page }) => {
      await page.setViewportSize({ width: dim.w, height: dim.h });
      await seedDismissedTour(page);
      await page.goto(p.route, { waitUntil: 'networkidle' });
      await page.waitForTimeout(800); // let blazor settle / fonts render
      const file = path.join(OUT, `AUDIT_${p.name}_${dim.label}.png`);
      await page.screenshot({ path: file, fullPage: false });
    });
  }
}

// Dark theme pass — only mobile + wide to keep scope tight
const DARK_WIDTHS = [
  { label: 'wide',   w: 1440, h: 900 },
  { label: 'mobile', w:  375, h: 812 },
];

for (const dim of DARK_WIDTHS) {
  for (const p of PAGES) {
    test(`DARK ${p.name} @ ${dim.label}`, async ({ page }) => {
      await page.setViewportSize({ width: dim.w, height: dim.h });
      await seedDismissedTour(page);
      // Set theme before first paint to avoid FOUC
      await page.addInitScript(() => {
        try { localStorage.setItem('kmila-theme', 'dark'); } catch {}
      });
      await page.goto(p.route, { waitUntil: 'networkidle' });
      await page.waitForTimeout(800);
      const file = path.join(OUT, `AUDIT_DARK_${p.name}_${dim.label}.png`);
      await page.screenshot({ path: file, fullPage: false });
    });
  }
}

// RTL Arabic pass — only narrow mobile + wide desktop, light theme.
for (const dim of DARK_WIDTHS) {
  for (const p of PAGES) {
    test(`AR ${p.name} @ ${dim.label}`, async ({ page }) => {
      await page.setViewportSize({ width: dim.w, height: dim.h });
      await seedDismissedTour(page);
      await page.addInitScript(() => {
        try { localStorage.setItem('kmila-lang', 'ar'); } catch {}
      });
      await page.goto(p.route, { waitUntil: 'networkidle' });
      await page.waitForTimeout(800);
      const file = path.join(OUT, `AUDIT_AR_${p.name}_${dim.label}.png`);
      await page.screenshot({ path: file, fullPage: false });
    });
  }
}
