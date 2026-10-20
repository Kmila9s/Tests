/**
 * _galleryFlow.ts — Lógica compartida de captura para la galería multiplataforma
 * de la tesis TT2. NO es un spec por sí mismo (no termina en .spec.ts); lo invocan
 * thesis-gallery.spec.ts (desktop → sufijo "web") y
 * thesis-gallery.mobile.spec.ts (Pixel 7 → sufijo "webmobile").
 *
 * Escribe PNG en Documentacion/TT2/images/gallery/ con el patrón
 *   gal_<modulo>_<web|webmobile>.png
 * Reutiliza los pageobjects existentes (ProjectsPage/EditorPage/RunBar).
 */
import { test } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { RunBar } from '../../pageobjects/RunBar';

const OUT = '/Users/miku/Documents/Kmila-9s/Documentacion/TT2/images/gallery';
const PROJ = 'ZZ-TT2-Gallery';

export function galleryTests(suffix: string) {
  fs.mkdirSync(OUT, { recursive: true });
  const shot = (page: any, mod: string, opts: any = {}) =>
    page.screenshot({ path: path.join(OUT, `gal_${mod}_${suffix}.png`), fullPage: false, ...opts });

  // Suprimir coachmarks del tour.
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      try {
        for (const k of ['kmila.tutorialSeen', 'kmila.tutorialSeen.projects', 'kmila.tutorialSeen.editor'])
          localStorage.setItem(k, '1');
      } catch {}
    });
  });

  test(`galería rutas de módulos (${suffix})`, async ({ page }) => {
    test.setTimeout(150_000);
    const routes: [string, string][] = [
      ['/', 'home'],
      ['/projects', 'projects'],
      ['/learn', 'learn'],
      ['/practice', 'practice'],
      ['/concepts', 'concepts'],
      ['/settings', 'settings'],
      ['/logs', 'logs'],
      ['/privacy', 'privacy'],
    ];
    for (const [route, mod] of routes) {
      await page.goto(route);
      await page.waitForTimeout(1400);
      await shot(page, mod);
    }
  });

  test(`galería editor: pestañas, dock y ejecución (${suffix})`, async ({ page }) => {
    test.setTimeout(600_000);
    const CLICK = { timeout: 12_000 };
    const projects = new ProjectsPage(page);
    const editor = new EditorPage(page);
    const runbar = new RunBar(page);

    await projects.goto();
    await projects.removeProject(PROJ).catch(() => {});
    await projects.createProject(PROJ, { templateId: 'counter' });
    await projects.openProject(PROJ);
    await editor.waitForReady().catch(() => {});
    await page.waitForTimeout(1500);

    const viewTabs = page.locator('.editor-workbench__view-tabs button.kmila-btn-tab');
    const nView = await viewTabs.count();
    const tabByName = async (idx: number) => { try { await viewTabs.nth(idx).click(CLICK); await page.waitForTimeout(1400); } catch {} };

    // --- Code + Blocks (rápidos, sin dependencias) ---
    await shot(page, 'editor');                         // índice 0 = Code
    if (nView > 1) { await tabByName(1); await shot(page, 'blocks'); }

    // --- Build + Run PRIMERO (habilita Replay y puebla Ondas/Esquemático) ---
    await tabByName(0);                                 // volver a Code para construir
    try { await runbar.pickFirstFpga(); await page.waitForTimeout(500); await shot(page, 'fpga'); } catch {}
    try { await runbar.build(); await runbar.waitForBuildIdle(); await page.waitForTimeout(1000); } catch {}
    try { await runbar.runBtn.click(CLICK); await page.waitForTimeout(4500); } catch {}

    // --- Ondas ---
    try { await editor.openDockTab('Waveforms'); await page.waitForTimeout(900); await shot(page, 'waveforms'); } catch {}
    // --- Esquemático (modo Debug si existe) ---
    try {
      await editor.openDockTab('Schematic');
      const modes = page.locator('.schematic-viewer__modes button.schematic-mode');
      if (await modes.count() >= 3) { await modes.nth(2).click(CLICK); await page.waitForTimeout(1000); }
      await shot(page, 'schematic');
    } catch {}
    // --- Replay (pestaña índice 2), ya con corrida disponible ---
    if (nView > 2) { await tabByName(2); await shot(page, 'replay'); }
    // --- Visual Runtime (última pestaña), intentando arrancarlo ---
    if (nView > 0) {
      await tabByName(nView - 1);
      try {
        const visRun = page.locator('[data-testid="visual-run"]');
        if (await visRun.count()) { await visRun.click(CLICK).catch(() => {}); await page.waitForTimeout(2500); }
      } catch {}
      await shot(page, 'visual');
    }

    // limpieza
    await projects.goto().catch(() => {});
    await projects.removeProject(PROJ).catch(() => {});
  });
}
