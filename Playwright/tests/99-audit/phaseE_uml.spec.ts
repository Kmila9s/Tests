import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseE_uml_state.json');
function save(s: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(s, null, 2)); }
async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}

test.describe('Phase E — UML export from Flow dock', () => {
  test.setTimeout(180_000);

  test('open editor → switch to Flow dock → click UML → capture .puml', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = { steps: [] };
    const consoleMsgs: string[] = [];
    page.on('console', m => consoleMsgs.push(`[${m.type()}] ${m.text().slice(0, 200)}`));

    await page.goto('/editor/276', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(4000);
    await dismissTour(page);
    await page.waitForFunction(() => (window as any).Blazor?._internal, { timeout: 20_000 }).catch(() => {});
    await page.waitForTimeout(3000);

    // 0) Select a specific FPGA if the picker is open, or open+select if not
    // The initial /editor/276 without saved config auto-opens the FPGA picker.
    const specificFpga = page.locator('button').filter({ hasText: /EP4CE10.*Cyclone/i }).first();
    if (await specificFpga.count()) {
      await specificFpga.click({ force: true }).catch(() => {});
      await page.waitForTimeout(2000);
      state.pickedFpga = 'EP4CE10';
      save(state);
    } else {
      // Picker not visible — try opening via profile menu path (fallback)
      state.pickedFpga = 'picker not open on load';
      save(state);
    }
    // Close any residual menu with Escape
    await page.keyboard.press('Escape').catch(() => {});
    await page.waitForTimeout(500);

    // 1) Click Build (title contains "Compilar" or "Build")
    const buildBtn = page.locator('button').filter({ hasText: /^Compilar$|^Build$/ }).first();
    if (await buildBtn.count() === 0) {
      // Fallback to any button whose title contains those words
      const alt = page.locator('button[title*="Compilar" i], button[title*="Build" i]').first();
      if (await alt.count()) await alt.click({ force: true }).catch(() => {});
    } else {
      await buildBtn.click({ force: true }).catch(() => {});
    }
    await page.waitForTimeout(4000);
    state.steps.push('build_attempted');
    save(state);

    // 2) Click Flow dock tab — lenient text match
    const flowTab = page.locator('.kmila-dock__tab, .kmila-btn-tab').filter({ hasText: /Flujo|Flow/i }).first();
    const flowFound = await flowTab.count();
    state.flowFound = flowFound;
    save(state);
    if (flowFound > 0) {
      await flowTab.click({ force: true }).catch(() => {});
      await page.waitForTimeout(3000);
      state.steps.push('flow_tab_clicked');
      save(state);
    }

    // 3) Click UML button inside FlowDiagramViewer toolbar
    const umlBtn = page.locator('button', { hasText: /^UML$/ }).first();
    state.umlFound = await umlBtn.count();
    state.umlVisible = state.umlFound > 0 ? await umlBtn.isVisible() : false;
    save(state);

    if (state.umlFound > 0) {
      await umlBtn.scrollIntoViewIfNeeded().catch(() => {});
      const dlPromise = page.waitForEvent('download', { timeout: 15_000 }).catch(() => null);
      await umlBtn.click({ force: true }).catch(() => {});
      await page.waitForTimeout(3500);
      const dl = await dlPromise;
      if (dl) {
        const suggested = dl.suggestedFilename();
        const savePath = path.join(EVIDENCE_DIR, `EVID_E_uml_${suggested}`);
        await dl.saveAs(savePath);
        const st = fs.statSync(savePath);
        const raw = fs.readFileSync(savePath, 'utf-8').slice(0, 800);
        state.umlDownload = {
          captured: true,
          filename: suggested,
          savedTo: savePath,
          sizeBytes: st.size,
          startsWithAtStartUml: raw.trimStart().startsWith('@startuml'),
          preview: raw,
        };
      } else {
        state.umlDownload = { captured: false, note: 'no download event' };
      }
      save(state);
    }

    // Dock tabs + button diagnostic
    state.diagnostic = await page.evaluate(() => ({
      dockTabs: Array.from(document.querySelectorAll('.kmila-btn-tab, [class*="dock__tab"]'))
        .map(n => ({ text: (n.textContent ?? '').trim().slice(0, 40), active: n.className.includes('active') })),
      hasFlowViewer: !!document.querySelector('.flow-diagram-viewer, [class*="flow-viewer"], .flow-diagram, svg[id*="flow"]'),
      flowViewerHtml: document.querySelector('.flow-diagram-viewer, [class*="flow-diagram"]')?.outerHTML?.slice(0, 600) || null,
      dockActiveBody: document.querySelector('.kmila-dock__body, .editor-dock__body')?.outerHTML?.slice(0, 800) || null,
      buttonsSample: Array.from(document.querySelectorAll('button')).map(b => (b.textContent ?? '').trim()).filter(t => t && t.length < 40).slice(0, 40),
      emptyState: Array.from(document.querySelectorAll('[class*="empty"], [class*="placeholder"]')).map(n => (n.textContent ?? '').trim().slice(0, 200)).filter(Boolean).slice(0, 8),
    }));
    state.consoleTail = consoleMsgs.slice(-20);
    save(state);
  });
});
