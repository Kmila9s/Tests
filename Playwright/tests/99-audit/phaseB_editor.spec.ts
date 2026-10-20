import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const evid = (name: string) => path.join(EVIDENCE_DIR, `EVID_B_${name}.png`);

async function shortGoto(page: Page, url: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
}

async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}

async function findFirstEditorHref(page: Page): Promise<string | null> {
  const link = page.locator('a[href*="/editor/"]').first();
  if (await link.count()) return await link.getAttribute('href');
  return null;
}

test.describe('Phase B — Editor core', () => {
  test.setTimeout(180_000);

  test('walk editor tabs, dock tabs, RunBar', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });

    // 1) Get an editor URL from Projects
    await shortGoto(page, '/projects');
    await dismissTour(page);
    const href = await findFirstEditorHref(page);
    if (!href) throw new Error('No /editor/ href found on /projects');
    console.log('EDITOR_HREF:', href);

    // 2) Open editor
    await shortGoto(page, href);
    await dismissTour(page);
    await page.waitForTimeout(3000); // Monaco + Blazor hydration
    await page.screenshot({ path: evid('01_editor_cold_open'), fullPage: false });

    // 3) Locate workbench tabs (Code / Blocks / Replay / Visual)
    const tabButtons = page.locator('[role="tab"], .kmila-btn-tab, [class*="EditorTabStrip"] button, [class*="editor-tab"]');
    const tabCount = await tabButtons.count();
    console.log('WORKBENCH_TAB_COUNT:', tabCount);
    for (let i = 0; i < Math.min(tabCount, 6); i++) {
      const label = (await tabButtons.nth(i).textContent().catch(() => ''))?.trim() ?? `t${i}`;
      console.log(`WORKBENCH_TAB[${i}]:`, label);
    }

    // Try to click each workbench tab in turn
    const workbenchLabels = ['Código', 'Code', 'Bloques', 'Blocks', 'Reproducción', 'Replay', 'Visual'];
    for (const label of workbenchLabels) {
      const btn = page.getByRole('tab', { name: new RegExp(label, 'i') })
        .or(page.locator('button', { hasText: new RegExp(`^${label}$`, 'i') })).first();
      if (await btn.count()) {
        await btn.click().catch(() => {});
        await page.waitForTimeout(1000);
        await page.screenshot({ path: evid(`02_workbench_${label.toLowerCase()}`) });
      }
    }

    // 4) Dock tabs — Schematic/Flow/Ports/Waveforms/Runs/Output
    const dockLabels = ['Esquema', 'Schematic', 'Flujo', 'Flow', 'Puertos', 'Ports', 'Ondas', 'Waveforms', 'Ejecuciones', 'Runs', 'Salida', 'Output'];
    for (const label of dockLabels) {
      const btn = page.locator('[class*="EditorDock"] button, [class*="editor-dock"] button, .kmila-btn-tab').filter({ hasText: new RegExp(`^${label}$`, 'i') }).first();
      if (await btn.count()) {
        await btn.click().catch(() => {});
        await page.waitForTimeout(800);
        await page.screenshot({ path: evid(`03_dock_${label.toLowerCase()}`) });
      }
    }

    // 5) RunBar — capture Idle state
    await page.screenshot({ path: evid('04_runbar_idle'), fullPage: false });

    // Try to find + click Run
    const run = page.locator('button').filter({ hasText: /^(Ejecutar|Run|Correr)/i }).first();
    if (await run.count()) {
      await run.click().catch(() => {});
      await page.waitForTimeout(1500);
      await page.screenshot({ path: evid('05_runbar_after_run_click') });
      await page.waitForTimeout(4000);
      await page.screenshot({ path: evid('06_runbar_running_or_complete') });
    }

    // 6) FilesCard / File modal
    const createFile = page.locator('button').filter({ hasText: /Nuevo archivo|New file|Crear archivo/i }).first();
    if (await createFile.count()) {
      await createFile.click().catch(() => {});
      await page.waitForTimeout(600);
      await page.screenshot({ path: evid('07_create_file_modal') });
      const cancel = page.locator('button').filter({ hasText: /Cancelar|Cancel/i }).first();
      if (await cancel.count()) await cancel.click().catch(() => {});
    }

    // 7) Configuration modal (ConfigurationsPill)
    const configPill = page.locator('.configurations-pill, [class*="ConfigurationsPill"], button[class*="config"]').first();
    if (await configPill.count()) {
      await configPill.click().catch(() => {});
      await page.waitForTimeout(600);
      await page.screenshot({ path: evid('08_config_modal') });
      const close = page.locator('button').filter({ hasText: /Cerrar|Close|Cancelar/i }).first();
      if (await close.count()) await close.click().catch(() => {});
    }

    console.log('DONE_PHASE_B');
  });
});
