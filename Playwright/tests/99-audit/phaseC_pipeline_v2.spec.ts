import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseC_v2_state.json');

async function shortGoto(page: Page, url: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
}
async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}
function saveState(state: any) {
  fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2));
}

test.describe('Phase C v2 — pick FPGA then Run', () => {
  test.setTimeout(240_000);

  test('open editor, pick FPGA via topbar or config modal, then Run', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = { steps: [] };

    await shortGoto(page, '/editor/411');
    await dismissTour(page);
    await page.waitForTimeout(3000);
    state.steps.push({ step: 'editor_opened', at: Date.now() });
    saveState(state);

    // Click FPGA-picker chip in the RunBar (`run-bar__fpga--missing`) — this
    // opens the TopBar's FPGA-picker via JS interop, per Editor.razor:1332
    const fpgaChip = page.locator('.run-bar__fpga--missing').first();
    if (await fpgaChip.count()) {
      await fpgaChip.click().catch(() => {});
      await page.waitForTimeout(800);
      state.fpgaChipClicked = true;
      saveState(state);
    }

    // Now the FPGA picker (likely a dropdown/menu) should be open.
    // Try to click the first available FPGA option.
    const fpgaOptions = await page.evaluate(() => {
      // Look for typical picker UI: dropdown menu items, list items with FPGA names.
      const anyMenu = document.querySelector('[class*="fpga-picker"], .kmila-fpga-menu, [role="menu"]');
      if (!anyMenu) return { menuFound: false, options: [] };
      const items = Array.from(anyMenu.querySelectorAll('button, li, [role="menuitem"]'))
        .filter(i => (i.textContent ?? '').trim().length > 0)
        .slice(0, 15)
        .map(i => (i.textContent ?? '').trim().slice(0, 40));
      return { menuFound: true, options: items };
    });
    state.fpgaOptions = fpgaOptions;
    saveState(state);

    // If no picker menu was found, try clicking the config-trigger button (opens ConfigurationModal)
    if (!fpgaOptions.menuFound) {
      const cfgBtn = page.locator('.run-bar__config-trigger').first();
      if (await cfgBtn.count()) {
        await cfgBtn.click().catch(() => {});
        await page.waitForTimeout(600);
        state.configOpened = true;
        saveState(state);
      }
    }

    // At this point either menu or modal is open. Try to click any "AMD" or first non-empty picker item.
    const picked = await page.evaluate(() => {
      const scope = document.querySelector('[class*="modal"], [class*="picker"], [role="menu"], [role="dialog"]');
      if (!scope) return { pickedAny: false };
      // Look for FPGA card entries. The picker uses `.kmila-fpga-card` or similar per convention.
      const cards = Array.from(scope.querySelectorAll('button, [class*="fpga-card"], [role="menuitem"], li'));
      // Pick the first that has "Artix" / "Zynq" / "Basys" / "AMD" / "Xilinx" / "Intel" / "Cyclone" in its text.
      const target = cards.find(c => /(Artix|Zynq|Basys|Cyclone|MAX|Nexys|Spartan|ECP|iCE|Lattice|AMD|Xilinx|Intel)/i.test((c.textContent ?? '')));
      if (!target) return { pickedAny: false, cardsFound: cards.length };
      (target as HTMLElement).click();
      return { pickedAny: true, pickedText: (target.textContent ?? '').trim().slice(0, 60) };
    });
    state.picked = picked;
    saveState(state);

    await page.waitForTimeout(1000);

    // Close the modal / picker if still open (press escape or click a close/apply button)
    const applyBtn = page.locator('button').filter({ hasText: /Aplicar|Apply|Aceptar|OK|Confirmar/i }).first();
    if (await applyBtn.count()) {
      await applyBtn.click().catch(() => {});
      await page.waitForTimeout(800);
    } else {
      await page.keyboard.press('Escape').catch(() => {});
      await page.waitForTimeout(500);
    }

    // Now re-read RunBar to see if Build/Run are enabled
    const postFpga = await page.evaluate(() => {
      const bar = document.querySelector('.run-bar');
      if (!bar) return null;
      const buildBtn = bar.querySelector('.run-bar__build') as HTMLButtonElement | null;
      const runBtn = bar.querySelector('.run-bar__run') as HTMLButtonElement | null;
      const fpgaChip = bar.querySelector('.run-bar__fpga');
      return {
        rootClasses: bar.className,
        buildDisabled: buildBtn?.disabled ?? null,
        buildText: (buildBtn?.textContent ?? '').trim(),
        runDisabled: runBtn?.disabled ?? null,
        runText: (runBtn?.textContent ?? '').trim(),
        fpgaChipText: (fpgaChip?.textContent ?? '').trim().slice(0, 60),
        fpgaChipMissingClass: fpgaChip?.classList.contains('run-bar__fpga--missing'),
      };
    });
    state.postFpga = postFpga;
    saveState(state);

    // If Run is now enabled, click it
    if (postFpga && postFpga.runDisabled === false) {
      const runBtn = page.locator('.run-bar__run').first();
      await runBtn.click().catch(() => {});
      state.runClicked = true;
      // Wait for sim: RunBar should transition to Building → Running → Complete
      await page.waitForTimeout(8000);
      saveState(state);

      const postRun = await page.evaluate(() => {
        const bar = document.querySelector('.run-bar');
        // Look for waveform: WaveformViewer renders an SVG inside .kmila-dock__panel--waveforms
        // First, click the Waveforms dock tab to force render
        return {
          runBarClasses: bar?.className,
          runBarText: bar ? (bar.textContent ?? '').replace(/\s+/g, ' ').trim().slice(0, 200) : null,
        };
      });
      state.postRun = postRun;
      saveState(state);

      // Click Waveforms dock tab
      await page.locator('#dock-tab-Waveforms').click().catch(() => {});
      await page.waitForTimeout(1500);
      const wave = await page.evaluate(() => {
        const panel = document.querySelector('.kmila-dock__panel--waveforms, .kmila-dock__panel');
        if (!panel) return null;
        const svgs = panel.querySelectorAll('svg');
        return {
          panelHtml: (panel.innerHTML ?? '').slice(0, 300),
          svgCount: svgs.length,
          textFirst: (panel.textContent ?? '').trim().slice(0, 400),
        };
      });
      state.waveformPanel = wave;
      saveState(state);

      // Click Schematic dock tab
      await page.locator('#dock-tab-Schematic').click().catch(() => {});
      await page.waitForTimeout(1500);
      const schem = await page.evaluate(() => {
        const panel = document.querySelector('.kmila-dock__panel');
        if (!panel) return null;
        const svgs = panel.querySelectorAll('svg');
        return {
          svgCount: svgs.length,
          textFirst: (panel.textContent ?? '').trim().slice(0, 200),
        };
      });
      state.schematicPanel = schem;
      saveState(state);

      // Click Flow
      await page.locator('#dock-tab-Flow').click().catch(() => {});
      await page.waitForTimeout(2000); // mermaid render
      const flow = await page.evaluate(() => {
        const panel = document.querySelector('.kmila-dock__panel');
        if (!panel) return null;
        return {
          svgCount: panel.querySelectorAll('svg').length,
          textFirst: (panel.textContent ?? '').trim().slice(0, 200),
          hasMermaid: !!panel.querySelector('.mermaid, [class*="mermaid"]'),
        };
      });
      state.flowPanel = flow;
      saveState(state);

      // Click Runs
      await page.locator('#dock-tab-Runs').click().catch(() => {});
      await page.waitForTimeout(800);
      const runs = await page.evaluate(() => {
        const panel = document.querySelector('.kmila-dock__panel');
        if (!panel) return null;
        return {
          textFirst: (panel.textContent ?? '').trim().slice(0, 400),
          rowCount: panel.querySelectorAll('[class*="run-entry"], .kmila-runs-row, li').length,
        };
      });
      state.runsPanel = runs;
      saveState(state);

      // Click Output
      await page.locator('#dock-tab-Output').click().catch(() => {});
      await page.waitForTimeout(500);
      const output = await page.evaluate(() => {
        const panel = document.querySelector('.kmila-dock__panel');
        if (!panel) return null;
        return { textFirst: (panel.textContent ?? '').trim().slice(0, 500) };
      });
      state.outputPanel = output;
      saveState(state);
    }

    console.log('DONE_PHASE_C_V2');
  });
});
