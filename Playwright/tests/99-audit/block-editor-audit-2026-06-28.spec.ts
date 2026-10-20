import { test } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const OUT = '/tmp/kmila_block_audit';
fs.mkdirSync(OUT, { recursive: true });

const TOUR_KEYS = [
  'kmila.tutorialSeen',
  'kmila.tutorialSeen.home', 'kmila.tutorialSeen.projects',
  'kmila.tutorialSeen.editor', 'kmila.tutorialSeen.learn',
  'kmila.tutorialSeen.concepts', 'kmila.tutorialSeen.practice',
  'kmila.tutorialSeen.settings',
];

async function seedDismissedTour(page) {
  await page.addInitScript((keys) => {
    try { for (const k of keys) localStorage.setItem(k, '1'); } catch {}
  }, TOUR_KEYS);
}

const SHOTS = [
  { name: 'editor-blocks-desktop-light', w: 1440, h: 900, dark: false, view: 'editor' },
  { name: 'editor-blocks-desktop-dark',  w: 1440, h: 900, dark: true,  view: 'editor' },
  { name: 'editor-blocks-mobile-light',  w:  375, h: 812, dark: false, view: 'editor' },
  { name: 'editor-blocks-mobile-dark',   w:  375, h: 812, dark: true,  view: 'editor' },
  { name: 'practice-blocks-desktop-light', w: 1440, h: 900, dark: false, view: 'practice' },
  { name: 'practice-blocks-mobile-light',  w:  375, h: 812, dark: false, view: 'practice' },
];

test.describe.configure({ mode: 'serial' });

for (const s of SHOTS) {
  test(`shot @ ${s.name}`, async ({ page }) => {
    await page.setViewportSize({ width: s.w, height: s.h });
    if (s.dark) {
      await page.addInitScript(() => {
        try { localStorage.setItem('kmila-theme', 'dark'); } catch {}
      });
    }
    await seedDismissedTour(page);

    if (s.view === 'editor') {
      // Grab the first project URL by scraping /projects in desktop mode,
      // then page.goto directly — bypasses the brittle tile-click at 375 px
      // where tiles can render below the fold / behind a drawer.
      await page.setViewportSize({ width: 1440, height: 900 });
      await page.goto('/projects', { waitUntil: 'networkidle' });
      await page.waitForTimeout(800);
      const href = await page
        .locator('a[href^="/editor/"]').first()
        .getAttribute('href').catch(() => null);
      // Restore the actual viewport for the screenshot
      await page.setViewportSize({ width: s.w, height: s.h });
      if (href) {
        await page.goto(href, { waitUntil: 'networkidle' });
        await page.waitForTimeout(2500); // monaco bootstrap
      }
      // Scope to the editor workbench tab strip — there are OTHER tablists
      // on the page (FPGA picker chips also use role=tab + .kmila-btn-tab).
      const blocksTab = page
        .locator('.editor-workbench__view-tabs .kmila-btn-tab')
        .filter({ hasText: /blocks|bloques|blöcke|blocs|ブロック|块|كتل/i });
      if (await blocksTab.count() > 0) {
        await blocksTab.first().click({ force: true });
        await page.waitForTimeout(2000); // block engine init + load .bvhdl
      } else {
        // Fallback: positional second tab inside the workbench strip
        const fallback = page.locator('.editor-workbench__view-tabs .kmila-btn-tab').nth(1);
        if (await fallback.count() > 0) {
          await fallback.click({ force: true });
          await page.waitForTimeout(2000);
        }
      }
    } else {
      // Concept exercise with actual block scaffold (half-adder is the
      // first non-reading exercise). e1 (sequential-pizza) is markdown
      // only — block editor lives further down or off the route.
      await page.goto('/concepts/exercise/e3-half-adder', { waitUntil: 'networkidle' });
      await page.waitForTimeout(2500);
      // Scroll the block editor into view before screenshotting so the
      // canvas — not the lesson preamble — is what the frame shows.
      const root = page.locator('.kmila-block-root').first();
      if (await root.count() > 0) {
        await root.scrollIntoViewIfNeeded().catch(() => {});
        await page.waitForTimeout(500);
      }
    }

    await page.screenshot({ path: path.join(OUT, `${s.name}.png`), fullPage: false });
  });
}
