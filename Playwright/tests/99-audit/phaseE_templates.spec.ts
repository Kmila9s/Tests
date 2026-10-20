import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseE_templates_state.json');

function save(state: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2)); }
async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}

test.describe('Phase E — Template & config exports', () => {
  test.setTimeout(120_000);

  test('FPGA template + Language template downloads', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = { steps: [] };

    const consoleMsgs: string[] = [];
    page.on('console', m => consoleMsgs.push(`[${m.type()}] ${m.text().slice(0, 200)}`));

    await page.goto('/settings', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    await dismissTour(page);

    // Wait for SignalR circuit
    await page.waitForFunction(() => {
      const w = window as any;
      return w.Blazor && w.Blazor._internal;
    }, { timeout: 15_000 }).catch(() => {});
    await page.waitForTimeout(2500);

    // Both cards use SETTINGS_DOWNLOAD_TEMPLATE — locate by parent card title.
    const cards = [
      { key: 'fpga',  headingRe: /FPGA personalizados|Custom FPGAs|User FPGAs/i },
      { key: 'lang',  headingRe: /Idiomas personalizados|Custom languages|User languages/i },
    ];

    state.results = {};
    for (const c of cards) {
      const heading = page.locator('h2', { hasText: c.headingRe }).first();
      const found = await heading.count();
      state.results[c.key] = { heading: found > 0 ? await heading.innerText() : 'NOT FOUND' };
      save(state);
      if (!found) continue;

      const card = heading.locator('xpath=ancestor::article[contains(@class,"settings-card")]').first();
      // Download template button is the first <button> in the card
      const dlBtn = card.locator('button').first();
      const enabled = await dlBtn.isEnabled();
      state.results[c.key].dlBtnEnabled = enabled;
      state.results[c.key].dlBtnText = (await dlBtn.innerText()).trim();
      save(state);
      if (!enabled) continue;

      // Scroll into view then click + capture download
      await dlBtn.scrollIntoViewIfNeeded();
      const downloadPromise = page.waitForEvent('download', { timeout: 20_000 }).catch(() => null);
      await dlBtn.click({ force: true });
      const download = await downloadPromise;

      if (!download) {
        state.results[c.key].download = { captured: false };
        save(state);
        continue;
      }
      const suggested = download.suggestedFilename();
      const savePath = path.join(EVIDENCE_DIR, `EVID_E_${c.key}_${suggested}`);
      await download.saveAs(savePath);
      const bytes = fs.readFileSync(savePath);
      state.results[c.key].download = {
        captured: true,
        filename: suggested,
        savedTo: savePath,
        sizeBytes: bytes.length,
      };
      // Try to parse as JSON
      try {
        const parsed = JSON.parse(bytes.toString('utf-8'));
        state.results[c.key].jsonKeys = Object.keys(parsed);
        state.results[c.key].jsonSample = JSON.stringify(parsed).slice(0, 300);
      } catch (e: any) {
        state.results[c.key].jsonParseError = e.message;
      }
      save(state);

      await page.waitForTimeout(1200);
    }

    // Also inspect toast keys we saw
    const toastHostText = await page.locator('.kmila-toast-host').innerText().catch(() => '');
    state.finalToastHost = toastHostText;
    state.consoleTail = consoleMsgs.slice(-30);
    save(state);
    state.steps.push('done');
    save(state);
  });
});
