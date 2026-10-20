import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseF_drilldown_state.json');

function save(state: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2)); }
async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}

test.describe('Phase F — Content drilldown', () => {
  test.setTimeout(240_000);

  test('open one Learn lesson, one Practice, one Concepts lesson + exercise', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = {};

    const consoleMsgs: string[] = [];
    page.on('console', m => consoleMsgs.push(`[${m.type()}] ${m.text().slice(0, 240)}`));

    async function inspect(url: string, key: string) {
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(2500);
      await dismissTour(page);
      await page.waitForFunction(() => (window as any).Blazor?._internal, { timeout: 15_000 }).catch(() => {});
      await page.waitForTimeout(2000);

      const info = await page.evaluate(() => {
        const h1 = document.querySelector('h1')?.textContent?.trim() ?? null;
        const h2s = Array.from(document.querySelectorAll('h2')).map(h => h.textContent?.trim()).slice(0, 8);
        const codeBlocks = document.querySelectorAll('pre code, .lesson-code, [class*="hljs"], .kmila-code').length;
        const monacoEditors = document.querySelectorAll('.monaco-editor').length;
        const blockCanvas = document.querySelectorAll('#kmila-blocks-canvas, [id*="blocks-canvas"], .kmila-blocks').length;
        const mermaidRendered = document.querySelectorAll('.mermaid svg, svg[id*="mermaid"]').length;
        const gateAnimator = document.querySelectorAll('.gate-animator, [class*="gate-animator"], svg[data-animator="gate"]').length;
        const fsmAnimator = document.querySelectorAll('.fsm-animator, [class*="fsm-animator"]').length;
        const errorBanners = Array.from(document.querySelectorAll('.alert-danger, .kmila-error, [class*="error"]'))
          .map(n => (n.textContent ?? '').trim().slice(0, 200)).filter(Boolean).slice(0, 5);
        const bodyText = document.body.innerText;
        const rawKeys = [...bodyText.matchAll(/\b(LEARN|PRACTICE|CONCEPTS|LESSON|EXERCISE|MODULE|COMMON|NAV|EDITOR|OUTPUT|RUN)_[A-Z0-9_]{2,}\b/g)]
          .map(m => m[0]);
        const rawKeysUnique = Array.from(new Set(rawKeys));
        return { h1, h2s, codeBlocks, monacoEditors, blockCanvas, mermaidRendered, gateAnimator, fsmAnimator, errorBanners, rawKeysUnique };
      });
      state[key] = info;
      save(state);
      await page.waitForTimeout(400);
    }

    await inspect('/learn/vhdl-basics/01-anatomy', 'learn_vhdl_anatomy');
    await inspect('/learn/concurrent-thinking/02-delta-cycles', 'learn_concurrent_delta');  // has inline ExecutionPlayer
    await inspect('/learn/fsm/01-mealy-vs-moore', 'learn_fsm_mealy');                       // has FsmAnimator
    await inspect('/practice/m1-half-adder', 'practice_half_adder');
    await inspect('/concepts/m-execution-models/01-what-does-run-mean', 'concepts_lesson');
    await inspect('/concepts/exercise/e3-half-adder', 'concepts_exercise_half_adder');

    state.consoleTail = consoleMsgs.slice(-30);
    save(state);
  });
});
