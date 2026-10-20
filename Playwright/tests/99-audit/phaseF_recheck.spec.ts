import { test, Page } from '@playwright/test';
import * as fs from 'fs';

const STATE_PATH = '/tmp/kmila_audit_2026-07-08/phaseF_recheck.json';
function save(s: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(s, null, 2)); }

test.setTimeout(120_000);
test('phase F recheck — block editor + fsm animator + connection-lost boilerplate', async ({ page }) => {
  const state: any = {};
  await page.goto('/concepts/exercise/e3-half-adder', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3500);
  state.concepts_ex_e3 = await page.evaluate(() => ({
    kmilaBlockEditor: document.querySelectorAll('.kmila-block-editor').length,
    kmilaBlockEditorCanvas: document.querySelectorAll('.kmila-block-editor__canvas').length,
    kmilaBlockToolbar: document.querySelectorAll('.kmila-block-toolbar').length,
    monaco: document.querySelectorAll('.monaco-editor').length,
    canvasChildCount: document.querySelector('.kmila-block-editor__canvas')?.children?.length ?? 0,
    canvasOuterHtml: (document.querySelector('.kmila-block-editor__canvas')?.outerHTML ?? '').slice(0, 400),
  }));
  save(state);

  await page.goto('/learn/fsm/01-mealy-vs-moore', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3500);
  state.learn_fsm = await page.evaluate(() => ({
    fsmAnimator: document.querySelectorAll('.fsm-animator, [id*="fsm"], [class*="fsm"]').length,
    svgCount: document.querySelectorAll('svg').length,
    ids: Array.from(document.querySelectorAll('[id]')).map(n => n.id).filter(i => /fsm|animator|player/i.test(i)),
  }));
  save(state);

  // Also test connection-lost is Blazor boilerplate
  state.componentsReconnectModal = await page.evaluate(() => ({
    exists: !!document.querySelector('#components-reconnect-modal'),
    outerHtml: (document.querySelector('#components-reconnect-modal')?.outerHTML ?? '').slice(0, 200),
  }));
  save(state);
});
