import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseE_vcd_state.json');
function save(state: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2)); }
async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}

test.describe('Phase E — VCD export from an editor run', () => {
  test.setTimeout(300_000);

  test('editor Build → Run → open Waveforms dock → click VCD → capture download', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = { steps: [] };

    const consoleMsgs: string[] = [];
    page.on('console', m => consoleMsgs.push(`[${m.type()}] ${m.text().slice(0, 300)}`));

    // Contador de 4 bits — project id 276 (verified in bundle spec)
    await page.goto('/editor/276', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(4000);
    await dismissTour(page);
    // Blazor Server hydration
    await page.waitForFunction(() => (window as any).Blazor?._internal, { timeout: 20_000 }).catch(() => {});
    await page.waitForTimeout(2500);

    // 0) Bypass F-E-12 by picking a specific FPGA model.
    // The picker is auto-opened when no saved config exists. Use the
    // `.kmila-fpga-picker__option` class (role="option") whose click handler is
    // `OnFPGAPicked(path)` in ProfileMenu.razor.
    const modelBtn = page.locator('.kmila-fpga-picker__option').filter({ hasText: /EP4CE10/ }).first();
    if (await modelBtn.count()) {
      await modelBtn.click({ force: true }).catch(() => {});
      await page.waitForTimeout(2500);
      state.pickedFpga = 'EP4CE10 via kmila-fpga-picker__option';
    } else {
      // Fallback: any first option
      const anyOption = page.locator('.kmila-fpga-picker__option').first();
      if (await anyOption.count()) {
        state.pickedFpga = 'first-option-fallback: ' + (await anyOption.innerText()).slice(0, 60);
        await anyOption.click({ force: true }).catch(() => {});
        await page.waitForTimeout(2500);
      } else {
        state.pickedFpga = 'kmila-fpga-picker__option not found';
      }
    }
    save(state);
    // Close any residual menu
    await page.keyboard.press('Escape').catch(() => {});
    await page.waitForTimeout(1000);

    // 1) Find Build button (has title / aria)
    const buildBtn = page.locator('button', { hasText: /^Build|Compilar$/i }).first()
      .or(page.locator('button[title*="Build" i], button[title*="Compilar" i]').first());
    const buildFound = await buildBtn.count();
    state.buildFound = buildFound;
    save(state);

    if (buildFound > 0) {
      await buildBtn.click({ force: true }).catch(() => {});
      await page.waitForTimeout(4000);
      state.steps.push('build_clicked');
      save(state);
    }

    // 2) Find Run button
    const runBtn = page.locator('button[title*="Run" i], button[title*="Ejecut" i], button.run-bar__run-btn').first();
    const runFound = await runBtn.count();
    state.runFound = runFound;
    save(state);

    if (runFound > 0) {
      await runBtn.click({ force: true }).catch(() => {});
      await page.waitForTimeout(6000); // sim runtime
      state.steps.push('run_clicked');
      save(state);
    }

    // Toast may say "FPGA required" — capture
    state.toastsAfterRun = await page.locator('.kmila-toast, .toast, [class*="toast"]').allInnerTexts();
    save(state);

    // 3) Find Waveforms dock tab and click
    const waveTab = page.locator('button', { hasText: /Waveforms|Ondas|Formas de onda/i }).first();
    const waveFound = await waveTab.count();
    state.waveTabFound = waveFound;
    save(state);
    if (waveFound > 0) {
      await waveTab.click({ force: true }).catch(() => {});
      await page.waitForTimeout(2500);
    }

    // 4) Find VCD button inside waveform viewer toolbar
    const vcdBtn = page.locator('button', { hasText: /^VCD$/ }).first();
    const vcdFound = await vcdBtn.count();
    state.vcdFound = vcdFound;
    state.vcdVisible = vcdFound > 0 ? await vcdBtn.isVisible() : false;
    save(state);

    // Diagnostic: dump the currently active dock tab body
    state.activeDockInfo = await page.evaluate(() => {
      const dockTabs = Array.from(document.querySelectorAll('[class*="dock-tab"], .kmila-btn-tab'))
        .map(n => ({ text: (n.textContent ?? '').trim().slice(0, 40), active: n.className.includes('active') || n.className.includes('is-active') }))
        .filter(t => t.text);
      const wv = document.querySelector('.waveform-viewer, [class*="waveform-viewer"]');
      const wvHtml = wv ? (wv as HTMLElement).outerHTML.slice(0, 500) : null;
      const allButtons = Array.from(document.querySelectorAll('button')).map(b => (b.textContent ?? '').trim()).filter(t => t && t.length < 30).slice(0, 60);
      const emptyStateText = Array.from(document.querySelectorAll('[class*="empty"], [class*="placeholder"]')).map(n => (n.textContent ?? '').trim().slice(0, 200)).filter(Boolean);
      return { dockTabs, wvHtml, allButtons, emptyStateText };
    });
    save(state);

    if (vcdFound > 0) {
      // Scroll VCD button into view + click, capture download
      await vcdBtn.scrollIntoViewIfNeeded().catch(() => {});
      const dlPromise = page.waitForEvent('download', { timeout: 15_000 }).catch(() => null);
      await vcdBtn.click({ force: true }).catch(() => {});
      await page.waitForTimeout(4000);
      const dl = await dlPromise;
      if (dl) {
        const suggested = dl.suggestedFilename();
        const savePath = path.join(EVIDENCE_DIR, `EVID_E_vcd_${suggested}`);
        await dl.saveAs(savePath);
        const st = fs.statSync(savePath);
        state.vcdDownload = { captured: true, filename: suggested, savedTo: savePath, sizeBytes: st.size };
        // Read first 800 chars of the VCD to validate format
        const raw = fs.readFileSync(savePath, 'utf-8').slice(0, 800);
        state.vcdHeader = raw;
      } else {
        state.vcdDownload = { captured: false, note: 'no download event' };
      }
      save(state);
    }

    state.toastsFinal = await page.locator('.kmila-toast, .toast, [class*="toast"]').allInnerTexts();
    state.consoleTail = consoleMsgs.slice(-30);
    save(state);
  });
});
