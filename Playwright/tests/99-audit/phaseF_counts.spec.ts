import { test } from '@playwright/test';
import * as fs from 'fs';

const STATE_PATH = '/tmp/kmila_audit_2026-07-08/phaseF_counts.json';
function save(s: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(s, null, 2)); }

test.setTimeout(180_000);
test('count lessons/practices/concepts under default+all filters', async ({ page }) => {
  const state: any = {};
  for (const [key, url] of [['learn','/learn'],['practice','/practice'],['concepts','/concepts']]) {
    try {
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(3000);
    } catch (e) { /* soldier on */ }

    const counts = await page.evaluate((k) => {
      // count anchor cards linking to `/k/...`
      const links = Array.from(document.querySelectorAll('a[href]'))
        .map(a => (a as HTMLAnchorElement).getAttribute('href') ?? '')
        .filter(h => h.startsWith('/' + k + '/') && !h.endsWith('/' + k));
      const unique = new Set(links);
      // count via lesson-card
      const cards = document.querySelectorAll('.learn-card, .practice-card, .exercise-card, .lesson-card, [class*="lesson"], [class*="exercise"]').length;
      // count via progress
      const progressText = document.querySelector('.learn-hero__progress-count')?.textContent?.trim() ?? null;
      return { totalAnchors: links.length, uniqueSlugs: unique.size, cardsByClass: cards, progressText };
    }, key);

    (state as any)[key] = counts;
    save(state);
  }
});
