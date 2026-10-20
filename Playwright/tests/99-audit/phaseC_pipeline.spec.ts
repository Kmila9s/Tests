import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseC_state.json');

async function shortGoto(page: Page, url: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
}

async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}

// Save state incrementally so timeouts still leave usable evidence.
function saveState(state: any) {
  fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2));
}

test.describe('Phase C — Simulation pipeline (UI driven)', () => {
  test.setTimeout(240_000);

  test('open editor, Build, Run, capture pipeline state', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = { steps: [] };
    saveState(state);

    // 1) Get editor URL
    await shortGoto(page, '/projects');
    await dismissTour(page);
    const href = await page.locator('a[href*="/editor/"]').first().getAttribute('href');
    if (!href) throw new Error('no editor href');
    state.editorHref = href;
    saveState(state);

    // 2) Open editor
    await shortGoto(page, href);
    await dismissTour(page);
    await page.waitForTimeout(3000);
    state.steps.push({ step: 'editor_open', at: Date.now() });
    saveState(state);

    // 3) Read RunBar state before build
    const preBuild = await page.evaluate(() => {
      const bar = document.querySelector('.run-bar');
      if (!bar) return null;
      const btns = Array.from(bar.querySelectorAll<HTMLButtonElement>('button')).map(b => ({
        text: (b.textContent ?? '').trim().slice(0, 40),
        title: b.title,
        disabled: b.disabled,
        classes: b.className.slice(0, 100),
      }));
      return {
        rootClasses: bar.className,
        buttonCount: btns.length,
        buttons: btns,
      };
    });
    state.preBuild = preBuild;
    saveState(state);

    // 4) Click Build (fast path — force-rebuild, no run)
    const buildBtn = page.locator('.run-bar button').filter({ hasText: /^(Compilar|Build)$/i }).first();
    let buildClicked = false;
    if (await buildBtn.count()) {
      await buildBtn.click().catch(() => {});
      buildClicked = true;
      await page.waitForTimeout(4000); // let build settle
    }
    state.steps.push({ step: 'after_build_click', buildClicked, at: Date.now() });

    const postBuild = await page.evaluate(() => {
      const bar = document.querySelector('.run-bar');
      const output = document.querySelector('.kmila-dock__panel--output, [class*="OutputPanel"]');
      return {
        runBarClasses: bar?.className,
        outputText: output ? (output.textContent ?? '').trim().slice(0, 600) : null,
      };
    });
    state.postBuild = postBuild;
    saveState(state);

    // 5) Click Run
    const runBtn = page.locator('.run-bar button').filter({ hasText: /^(Ejecutar|Run|Correr)$/i }).first();
    let runClicked = false;
    if (await runBtn.count()) {
      await runBtn.click().catch(() => {});
      runClicked = true;
      await page.waitForTimeout(6000); // let sim run
    }
    state.steps.push({ step: 'after_run_click', runClicked, at: Date.now() });
    saveState(state);

    // 6) Read pipeline outputs
    const postRun = await page.evaluate(() => {
      const bar = document.querySelector('.run-bar');
      const wave = document.querySelector('.kmila-waveform, [class*="WaveformViewer"]');
      const schem = document.querySelector('.kmila-schematic, [class*="SchematicViewer"]');
      const flow = document.querySelector('.kmila-flow, [class*="FlowDiagram"]');
      const runsPanel = document.querySelector('[class*="RunsPanel"]');
      const signals = document.querySelector('.kmila-signals-table, [class*="SignalsTable"]');

      return {
        runBarClasses: bar?.className,
        waveformFound: !!wave,
        waveformChildCount: wave ? wave.children.length : 0,
        waveformSvgCount: wave ? wave.querySelectorAll('svg').length : 0,
        schematicFound: !!schem,
        schematicSvgCount: schem ? schem.querySelectorAll('svg').length : 0,
        flowFound: !!flow,
        flowSvgCount: flow ? flow.querySelectorAll('svg').length : 0,
        signalsFound: !!signals,
        signalsRowCount: signals ? signals.querySelectorAll('tr, [class*="signal-row"]').length : 0,
        runsPanelFound: !!runsPanel,
        runsCount: runsPanel ? runsPanel.querySelectorAll('[class*="run-entry"], li, tr').length : 0,
      };
    });
    state.postRun = postRun;
    saveState(state);

    // 7) Try each dock tab click and record what renders
    const dockTabs = await page.evaluate(() => {
      const tabs = Array.from(document.querySelectorAll<HTMLButtonElement>('.kmila-dock__tab'));
      return tabs.map(t => ({
        text: (t.textContent ?? '').trim().slice(0, 40),
        id: t.id,
        selected: t.getAttribute('aria-selected') === 'true',
      }));
    });
    state.dockTabs = dockTabs;
    saveState(state);

    // Click each dock tab and read what appears in the panel
    const dockResults: any[] = [];
    for (const t of dockTabs) {
      const btn = page.locator(`#${t.id}`);
      if (await btn.count()) {
        await btn.click().catch(() => {});
        await page.waitForTimeout(700);
        const panelSummary = await page.evaluate(() => {
          const panel = document.querySelector('.kmila-dock__panel');
          if (!panel) return null;
          const empty = panel.querySelector('.kmila-dock__empty');
          return {
            emptyState: !!empty,
            firstChildTag: panel.firstElementChild?.tagName,
            firstText: (panel.textContent ?? '').trim().slice(0, 200),
            hasSvg: panel.querySelector('svg') !== null,
            hasTable: panel.querySelector('table') !== null,
          };
        });
        dockResults.push({ tab: t.text, id: t.id, panelSummary });
        saveState({ ...state, dockResults });
      }
    }
    state.dockResults = dockResults;
    saveState(state);

    console.log('DONE_PHASE_C  editor=' + href);
    console.log('SUMMARY:', JSON.stringify({
      preBuildButtons: state.preBuild?.buttonCount,
      afterBuild_runBarClasses: state.postBuild?.runBarClasses,
      afterRun_wave: state.postRun?.waveformFound,
      afterRun_signals: state.postRun?.signalsRowCount,
      dockTabCount: dockTabs.length,
    }, null, 2));
  });
});
