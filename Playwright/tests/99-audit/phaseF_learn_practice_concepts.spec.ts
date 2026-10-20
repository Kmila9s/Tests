import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseF_state.json');

function save(state: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2)); }
async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}

test.describe('Phase F — Learn / Practice / Concepts / Settings sweep', () => {
  test.setTimeout(180_000);

  test('enumerate all sections', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = { pages: {} };

    const consoleMsgs: string[] = [];
    page.on('console', m => consoleMsgs.push(`[${m.type()}] ${m.text().slice(0, 180)}`));

    async function surveyPage(url: string, key: string) {
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(2500);
      await dismissTour(page);
      await page.waitForFunction(() => (window as any).Blazor?._internal, { timeout: 15_000 }).catch(() => {});
      await page.waitForTimeout(1500);

      const info = await page.evaluate(() => {
        const title = document.title;
        const h1 = document.querySelector('h1')?.textContent?.trim() ?? null;
        // Cards: look for anchor tags + article/card wrappers
        const cards = Array.from(document.querySelectorAll('article, .card, .lesson-card, .exercise-card, [class*="card"]'))
          .map(n => (n.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 160))
          .filter(t => t.length > 5);
        const links = Array.from(document.querySelectorAll('a[href]'))
          .map(a => ({
            href: (a as HTMLAnchorElement).getAttribute('href') ?? '',
            text: (a.textContent ?? '').trim().slice(0, 100),
          }))
          .filter(l => l.text && !l.href.startsWith('#'));
        // Filter/tag buttons (kmila-btn-tab)
        const tabs = Array.from(document.querySelectorAll('.kmila-btn-tab, [role="tab"], button[class*="tab"]'))
          .map(n => (n.textContent ?? '').trim().slice(0, 80))
          .filter(Boolean);
        // Detect empty state / placeholder
        const placeholders = Array.from(document.querySelectorAll('[class*="placeholder"], [class*="empty-state"]'))
          .map(n => (n.textContent ?? '').trim().slice(0, 200))
          .filter(Boolean);
        // Untranslated raw i18n keys visible on page
        const bodyText = document.body.innerText;
        const unTranslated = [...bodyText.matchAll(/\b[A-Z][A-Z0-9_]{6,}\b/g)]
          .map(m => m[0])
          .filter(k => /^(NAV_|SETTINGS_|LEARN_|PRACTICE_|CONCEPTS_|LOGS_|COMMON_|RUN_|EDITOR_|VISUAL_|FLOW_|SCHEM_|EXPORT_|BUNDLE_|LESSON_|EXERCISE_|MODULE_|HOME_|BLOCK_|OUTPUT_)/.test(k));
        const unTranslatedUnique = Array.from(new Set(unTranslated));
        return { title, h1, cardCount: cards.length, cards: cards.slice(0, 40), linkCount: links.length, links: links.slice(0, 60), tabs, placeholders, unTranslated: unTranslatedUnique };
      });
      state.pages[key] = info;
      save(state);
    }

    await surveyPage('/learn',    'learn');
    await surveyPage('/practice', 'practice');
    await surveyPage('/concepts', 'concepts');
    await surveyPage('/settings', 'settings');
    await surveyPage('/logs',     'logs');
    await surveyPage('/privacy',  'privacy');

    state.consoleTail = consoleMsgs.slice(-20);
    save(state);
  });
});
