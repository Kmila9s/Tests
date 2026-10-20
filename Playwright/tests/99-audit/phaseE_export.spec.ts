import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseE_state.json');

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

test.describe('Phase E — Export/Import round-trip', () => {
  test.setTimeout(120_000);

  test('bundle export from Settings + Visual PNG/SVG export', async ({ page, context }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = {};

    // ============================================================
    // 1) Settings → Project Bundle → Export
    // ============================================================
    await shortGoto(page, '/settings');
    await dismissTour(page);
    await page.waitForTimeout(2000);

    // Scroll to Project Bundle card
    const bundleCard = page.locator('button').filter({ hasText: /Exportar proyecto|Export project|Descargar proyecto/i }).first();
    const bundleCardVisible = await bundleCard.count();
    state.bundleButtonCount = bundleCardVisible;
    save(state);

    if (bundleCardVisible > 0) {
      // Blazor Server bundle export doesn't use browser download event —
      // it uses IFileExportService which on Web calls JS interop to save a file.
      // On Web target, that likely triggers a browser download via <a href="data:" download>
      // or `URL.createObjectURL(blob)`. Let's try to catch it.

      // Some bundle exports need a project selection first (dropdown)
      const projectSelector = page.locator('select, [class*="bundle-project-select"]').first();
      if (await projectSelector.count()) {
        // Select the demo_board (id 411)
        await projectSelector.selectOption({ value: '411' }).catch(async () => {
          // Fallback: select by label
          await projectSelector.selectOption({ label: /Tablero/i }).catch(() => {});
        });
        await page.waitForTimeout(500);
      }

      // Try to catch the download or just fire the click
      let downloadCaptured = false;
      const downloadPromise = page.waitForEvent('download', { timeout: 10_000 }).catch(() => null);
      await bundleCard.click().catch(() => {});
      const download = await downloadPromise;
      if (download) {
        const savePath = path.join(EVIDENCE_DIR, `EVID_E_bundle_${await download.suggestedFilename()}`);
        await download.saveAs(savePath);
        const stat = fs.statSync(savePath);
        state.bundleDownload = {
          filename: await download.suggestedFilename(),
          savedTo: savePath,
          sizeBytes: stat.size,
        };
        downloadCaptured = true;
      } else {
        state.bundleDownload = { captured: false, note: 'download event did not fire in 10s' };
      }
      state.bundleDownloadCaptured = downloadCaptured;
      save(state);
    }

    // ============================================================
    // 2) Visual PNG export on /visual/411
    // ============================================================
    await shortGoto(page, '/visual/411');
    await dismissTour(page);
    await page.waitForTimeout(3500);

    const pngBtn = page.locator('button[title="Exportar PNG"]').first();
    if (await pngBtn.count()) {
      const dlPromise = page.waitForEvent('download', { timeout: 15_000 }).catch(() => null);
      await pngBtn.click().catch(() => {});
      const download = await dlPromise;
      if (download) {
        const savePath = path.join(EVIDENCE_DIR, `EVID_E_visual_${await download.suggestedFilename()}`);
        await download.saveAs(savePath);
        const stat = fs.statSync(savePath);
        state.visualPng = {
          filename: await download.suggestedFilename(),
          savedTo: savePath,
          sizeBytes: stat.size,
        };
      } else {
        state.visualPng = { captured: false };
      }
      save(state);
    }

    const svgBtn = page.locator('button[title="Exportar SVG"]').first();
    if (await svgBtn.count()) {
      const dlPromise = page.waitForEvent('download', { timeout: 15_000 }).catch(() => null);
      await svgBtn.click().catch(() => {});
      const download = await dlPromise;
      if (download) {
        const savePath = path.join(EVIDENCE_DIR, `EVID_E_visual_${await download.suggestedFilename()}`);
        await download.saveAs(savePath);
        const stat = fs.statSync(savePath);
        state.visualSvg = {
          filename: await download.suggestedFilename(),
          savedTo: savePath,
          sizeBytes: stat.size,
        };
      } else {
        state.visualSvg = { captured: false };
      }
      save(state);
    }

    console.log('DONE_PHASE_E');
  });
});
