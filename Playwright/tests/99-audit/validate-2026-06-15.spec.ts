import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// Quick visual-validation pass for the 2026-06-15 audit fixes. Captures full-
// page screenshots at a mobile viewport so dictionary + CSS regressions are
// obvious on the same form-factor the audit used.
//
// Run with:
//   KMILA_WEB_PORT=5297 npx playwright test tests/99-audit/validate-2026-06-15.spec.ts \
//     --headed=false --project=chromium-desktop --workers=1

const OUT = '/tmp/kmila_audit_2026-06-15_after';
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

async function setup(page: Page) {
  await page.setViewportSize({ width: 412, height: 915 }); // Pixel 7-ish
  await page.goto('/');
  await page.waitForLoadState('networkidle').catch(() => {});
  await page.evaluate((keys: string[]) => {
    keys.forEach(k => localStorage.setItem(k, '1'));
    localStorage.setItem('kmila-lang', 'es');
  }, TOUR_KEYS);
}

async function shot(page: Page, name: string) {
  await page.waitForTimeout(800);
  const file = path.join(OUT, `${name}.png`);
  await page.screenshot({ path: file, fullPage: true });
  // eslint-disable-next-line no-console
  console.log('saved', file);
}

test.describe.configure({ mode: 'serial' });

test('A-01/A-02/A-03 — Home feature bento', async ({ page }) => {
  // Use a much taller viewport so the bento renders inside one capture.
  await page.setViewportSize({ width: 412, height: 3000 });
  await page.goto('/');
  await page.evaluate((keys: string[]) => {
    keys.forEach(k => localStorage.setItem(k, '1'));
    localStorage.setItem('kmila-lang', 'es');
  }, TOUR_KEYS);
  await page.reload();
  await page.waitForSelector('h1, h2', { timeout: 30_000 });
  await page.waitForTimeout(1500);
  await shot(page, '01-home-tall');
  // Scroll explicitly to render lazy content
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
  await page.waitForTimeout(1500);
  await shot(page, '01b-home-bottom');
});

test('B-01 + B-03 + B-04 — Projects + Create modal', async ({ page }) => {
  await setup(page);
  await page.goto('/projects');
  await page.waitForSelector('h1, .projects-page__title, [class*="project"]', { timeout: 30_000 });
  await shot(page, '02-projects-list');

  // Open create modal
  const createBtn = page.locator('button:has-text("Crear")').first();
  if (await createBtn.count()) {
    await createBtn.click().catch(() => {});
    await page.waitForTimeout(700);
    await shot(page, '03-projects-create-modal');
  }
});

test('S-05 + J-01 + J-02 — Logs footer + source casing + untranslated note', async ({ page }) => {
  await setup(page);
  await page.goto('/logs');
  await page.waitForSelector('.logs-page', { timeout: 30_000 });
  await shot(page, '04-logs-page');
});

test('S-05 — Privacy footer at viewport bottom', async ({ page }) => {
  await setup(page);
  await page.goto('/privacy');
  await page.waitForSelector('h1, h2', { timeout: 30_000 });
  await shot(page, '05-privacy-page');
});

test('K-01 + K-03 — Settings card icons + danger button', async ({ page }) => {
  await setup(page);
  await page.goto('/settings');
  await page.waitForSelector('.settings-card', { timeout: 30_000 });
  await shot(page, '06-settings-top');
  // Scroll down to libraries card (where K-03 danger button lives)
  await page.evaluate(() => window.scrollTo(0, 800));
  await shot(page, '07-settings-libraries');
  await page.evaluate(() => window.scrollTo(0, 1800));
  await shot(page, '08-settings-bottom');
});

test('D-02 + D-03 — Editor dock tabs + view tabs', async ({ page }) => {
  await setup(page);
  await page.goto('/projects');
  await page.waitForSelector('a[href*="/editor/"]', { timeout: 30_000 });
  // Pull the first href and navigate directly — clicking can fail when the
  // card is below the viewport fold on a 412×915 mobile screen.
  const href = await page.locator('a[href*="/editor/"]').first().getAttribute('href');
  if (href) await page.goto(href);
  await page.waitForLoadState('networkidle').catch(() => {});
  await page.waitForTimeout(3500);
  await shot(page, '09-editor-page');
});

test('I-02 — Practice scaffold fallback', async ({ page }) => {
  await setup(page);
  await page.goto('/practice');
  await page.waitForSelector('h1, h2', { timeout: 30_000 });
  await shot(page, '10-practice-list');
  // Try clicking the first exercise
  const firstExercise = page.locator('a[href*="/practice/"], button:has(.practice-tile), .practice-tile').first();
  if (await firstExercise.count()) {
    await firstExercise.click().catch(() => {});
    await page.waitForTimeout(2500);
    await shot(page, '11-practice-exercise');
  }
});

test('G-02 — Lesson code-block wrap', async ({ page }) => {
  await setup(page);
  await page.goto('/learn');
  await page.waitForSelector('h1, h2', { timeout: 30_000 });
  await shot(page, '12-learn-list');
  // Try to navigate into a lesson that contains code blocks
  const firstLesson = page.locator('a[href*="/learn/"]').first();
  if (await firstLesson.count()) {
    await firstLesson.click().catch(() => {});
    await page.waitForTimeout(2500);
    await shot(page, '13-learn-lesson');
  }
});
