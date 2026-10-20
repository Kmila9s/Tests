/**
 * thesis-evidence.spec.ts — Captura de evidencia para la tesis TT2.
 *
 * Recorre TODAS las vistas estáticas, abre un proyecto de trabajo desde la
 * plantilla `counter`, captura cada view-tab y cada dock-tab del editor, ejecuta
 * una simulación y captura Waveforms / Schematic (3 modos) / Flow / Runs / Visual.
 * Las imágenes se escriben en Documentacion/TT2/evidence_2026-09-14/screenshots/.
 *
 * Tooling de captura (no es lógica de la app). Reutiliza los pageobjects
 * existentes. Ejecutar:
 *   cd Tests/Playwright
 *   npx playwright test tests/99-audit/thesis-evidence.spec.ts --project=chromium-desktop
 */
import { test, expect } from '@playwright/test';
import * as path from 'path';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { RunBar } from '../../pageobjects/RunBar';

const OUT = '/Users/miku/Documents/Kmila-9s/Documentacion/TT2/evidence_2026-09-14/screenshots';
const PROJ = 'ZZ-TT2-Evidence';

function shot(page, name: string, opts: any = {}) {
  return page.screenshot({ path: path.join(OUT, name + '.png'), ...opts });
}

// Suprimir coachmarks del tour en todo el contexto.
test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    try {
      for (const k of ['kmila.tutorialSeen', 'kmila.tutorialSeen.projects', 'kmila.tutorialSeen.editor'])
        localStorage.setItem(k, '1');
    } catch {}
  });
});

test('vistas estáticas', async ({ page }) => {
  const routes: [string, string][] = [
    ['/', '00-home'],
    ['/projects', '01-projects'],
    ['/learn', '02-learn'],
    ['/concepts', '03-concepts'],
    ['/practice', '04-practice'],
    ['/settings', '05-settings'],
    ['/privacy', '06-privacy'],
    ['/logs', '07-logs'],
  ];
  for (const [route, name] of routes) {
    await page.goto(route);
    await page.waitForTimeout(1200);
    await shot(page, name, { fullPage: true });
  }
});

test('vista de una lección Learn y una práctica', async ({ page }) => {
  test.setTimeout(120_000);
  // Los tres hubs usan tarjetas `a.learn-card`.
  for (const [hub, name] of [['/learn', '08-learn-lesson'], ['/practice', '09-practice-exercise'], ['/concepts', '10-concept-lesson']] as [string, string][]) {
    await page.goto(hub);
    await page.waitForTimeout(1200);
    const card = page.locator('a.learn-card').first();
    if (await card.count()) {
      await card.click().catch(() => {});
      await page.waitForTimeout(2500);
    }
    // viewport (no fullPage: las páginas de detalle con animaciones/ExecutionPlayer
    // pueden colgar una captura fullPage por inestabilidad de layout)
    await shot(page, name);
  }
});

test('editor: vistas, dock y ejecución', async ({ page }) => {
  test.setTimeout(180_000);
  const projects = new ProjectsPage(page);
  const editor = new EditorPage(page);
  const runbar = new RunBar(page);

  await projects.goto();
  // proyecto de trabajo desde la plantilla counter (tiene reloj → produce ondas)
  await projects.removeProject(PROJ).catch(() => {});
  await projects.createProject(PROJ, { templateId: 'counter' });
  await projects.openProject(PROJ);
  await editor.waitForReady();
  await page.waitForTimeout(1500);

  // --- View tabs ---
  const viewTabs = page.locator('.editor-workbench__view-tabs button.kmila-btn-tab');
  const nView = await viewTabs.count();
  await shot(page, '20-editor-code');
  if (nView > 1) { await viewTabs.nth(1).click(); await page.waitForTimeout(1200); await shot(page, '21-editor-blocks'); }
  // Visual tab (última)
  if (nView > 0) { await viewTabs.nth(nView - 1).click(); await page.waitForTimeout(1500); await shot(page, '22-editor-visual'); }
  // volver a Code
  await viewTabs.nth(0).click(); await page.waitForTimeout(500);

  // --- FPGA + build ---
  await runbar.pickFirstFpga();
  await shot(page, '30-runbar-fpga');
  await runbar.build();
  await runbar.waitForBuildIdle();
  await page.waitForTimeout(1000);

  // --- Dock tabs ---
  const docks: any[] = ['Entities', 'Schematic', 'Flow', 'Ports', 'Waveforms', 'Runs', 'Files', 'Output'];
  for (let k = 0; k < docks.length; k++) {
    try {
      await editor.openDockTab(docks[k]);
      await page.waitForTimeout(800);
      await shot(page, `4${k}-dock-${docks[k].toLowerCase()}`);
    } catch { /* algún panel puede requerir un run primero */ }
  }

  // --- Schematic: 3 modos ---
  try {
    await editor.openDockTab('Schematic');
    const modes = page.locator('.schematic-viewer__modes button.schematic-mode');
    const nm = await modes.count();
    for (let m = 0; m < nm; m++) {
      await modes.nth(m).click();
      await page.waitForTimeout(900);
      await shot(page, `50-schematic-mode${m}`);
    }
  } catch {}

  // --- Ejecución (Run) ---
  await runbar.runBtn.click();
  await page.waitForTimeout(4000); // dejar correr y que el dock salte a Waveforms
  await shot(page, '60-run-waveforms', { fullPage: false });
  try { await editor.openDockTab('Waveforms'); await page.waitForTimeout(800); await shot(page, '61-waveforms-panel'); } catch {}
  try {
    await editor.openDockTab('Schematic');
    const dbg = page.locator('.schematic-viewer__modes button.schematic-mode');
    if (await dbg.count() >= 3) { await dbg.nth(2).click(); await page.waitForTimeout(1000); await shot(page, '62-schematic-debug'); }
  } catch {}

  // --- Visual runtime corriendo ---
  try {
    await viewTabs.nth(nView - 1).click(); // Visual tab
    await page.waitForTimeout(1500);
    const visRun = page.locator('[data-testid="visual-run"]');
    if (await visRun.count()) { await visRun.click(); await page.waitForTimeout(2500); }
    await shot(page, '70-visual-run');
  } catch {}

  // limpieza
  await projects.goto();
  await projects.removeProject(PROJ).catch(() => {});
});
