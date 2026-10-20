import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseD_v2_state.json');

async function shortGoto(page: Page, url: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
}
async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}
function save(state: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2)); }

test.describe('Phase D v2 — real selectors', () => {
  test.setTimeout(120_000);

  test('enumerate palette + canvas widgets on demo_board', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = {};

    await shortGoto(page, '/visual/411');
    await dismissTour(page);
    await page.waitForTimeout(4000);

    const palette = await page.evaluate(() => {
      const p = document.querySelector('.visual-page__palette');
      if (!p) return { found: false };
      const items = Array.from(p.querySelectorAll('button')).map(b => ({
        title: b.getAttribute('title'),
        aria: b.getAttribute('aria-label'),
        text: (b.textContent ?? '').trim().slice(0, 40),
        type: b.getAttribute('data-widget-type') || b.getAttribute('data-type'),
        classes: b.className.slice(0, 100),
      }));
      return { found: true, count: items.length, items };
    });
    state.palette = palette;
    save(state);

    const canvas = await page.evaluate(() => {
      const c = document.querySelector('.kviz-canvas');
      if (!c) return { found: false };
      const children = Array.from(c.children);
      // Widgets on canvas have class like kviz-<type> or data attributes
      const widgets = Array.from(c.querySelectorAll('[class*="kviz-widget"], [class*="kviz-comp"], [data-kviz-type]'))
        .map(w => ({
          type: w.getAttribute('data-kviz-type') || w.className.match(/kviz-(?:widget-)?([a-z0-9-]+)/i)?.[1] || '?',
          classes: (w as HTMLElement).className.slice(0, 100),
        }));
      // Fallback: iterate direct children
      const directChildren = children.map(ch => ({
        tag: ch.tagName,
        classes: (ch as HTMLElement).className.slice(0, 100),
      }));
      return {
        found: true,
        rootClasses: (c as HTMLElement).className,
        childCount: children.length,
        widgets,
        directChildren: directChildren.slice(0, 20),
      };
    });
    state.canvas = canvas;
    save(state);

    // Locator panel + fit-to-view accessibility
    const locator = await page.evaluate(() => {
      const l = document.querySelector('[data-testid="visual-locator-panel"], .visual-page__locator');
      if (!l) return { found: false };
      return {
        found: true,
        text: (l.textContent ?? '').trim().slice(0, 200),
        visible: (l as HTMLElement).offsetWidth > 0,
      };
    });
    state.locator = locator;
    save(state);

    // Look for Ejecutar button — clicking triggers the sim inside Visual mode
    const visualRun = await page.evaluate(() => {
      // The "Ejecutar" button in visual-page__toolbar
      const btns = Array.from(document.querySelectorAll('.visual-page__toolbar button, .visual-page__runbar button'));
      return btns.map(b => ({
        text: (b.textContent ?? '').trim().slice(0, 30),
        disabled: (b as HTMLButtonElement).disabled,
      }));
    });
    state.visualRunButtons = visualRun;
    save(state);

    // Try clicking Ejecutar
    const runBtn = page.locator('.visual-page__toolbar button, .visual-page__runbar button').filter({ hasText: /^\s*Ejecutar\s*$/ }).first();
    let ran = false;
    if (await runBtn.count()) {
      const disabled = await runBtn.isDisabled().catch(() => true);
      state.visualRunBtnDisabled = disabled;
      if (!disabled) {
        await runBtn.click().catch(() => {});
        ran = true;
        await page.waitForTimeout(5000);
      }
    }
    state.visualRan = ran;
    save(state);

    // If ran, check for state changes: LED brightness, 7-seg digits, etc.
    if (ran) {
      const runningState = await page.evaluate(() => {
        const canvas = document.querySelector('.kviz-canvas');
        const led = document.querySelector('[class*="kviz-led"]');
        const svg = document.querySelector('.kviz-canvas svg');
        return {
          canvasClasses: canvas?.className,
          ledClasses: led?.className,
          svgFound: !!svg,
        };
      });
      state.runningState = runningState;
      save(state);
    }

    console.log('DONE_PHASE_D_V2');
  });
});
