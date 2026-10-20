/**
 * android-gallery.spec.ts — Capturas NATIVAS del head Android en el emulador,
 * vía Playwright Android (_android): conduce el BlazorWebView (navegación) y
 * captura la pantalla del dispositivo con device.screenshot() (nativa, con
 * barra de estado → prueba de ejecución nativa real).
 *
 * Requisitos: emulador booteado con la app kmila.kmila instalada.
 * Ejecutar: npx playwright test tests/99-audit/android-gallery.spec.ts --project=chromium-desktop
 */
import { test, _android as android } from '@playwright/test';
import * as path from 'path';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { RunBar } from '../../pageobjects/RunBar';

const OUT = '/Users/miku/Documents/Kmila-9s/Documentacion/TT2/images/gallery';
const BASE = 'https://0.0.0.1';
const PKG = 'kmila.kmila';
const PROJ = 'ZZ-TT2-Gallery';
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

test('android native gallery', async () => {
  test.setTimeout(600_000);
  const [device] = await android.devices();
  if (!device) throw new Error('no android device');

  // Asegurar app abierta.
  await device.shell(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`).catch(() => {});
  await sleep(6000);

  const webview = await device.webView({ pkg: PKG });
  const page = await webview.page();
  page.setDefaultTimeout(20_000);

  const shot = async (name: string) => { await device.screenshot({ path: path.join(OUT, `gal_${name}_android.png`) }); };

  // Absolutizar los goto relativos de los pageobjects (no hay baseURL en webview).
  const _goto = page.goto.bind(page);
  (page as any).goto = (url: string, opts?: any) =>
    _goto(typeof url === 'string' && url.startsWith('/') ? BASE + url : url, opts);
  await page.addInitScript(() => {
    try { for (const k of ['kmila.tutorialSeen', 'kmila.tutorialSeen.projects', 'kmila.tutorialSeen.editor']) localStorage.setItem(k, '1'); } catch {}
  });

  // --- Rutas de módulos ---
  const routes: [string, string][] = [
    ['/', 'home'], ['/projects', 'projects'], ['/learn', 'learn'], ['/practice', 'practice'],
    ['/concepts', 'concepts'], ['/settings', 'settings'], ['/logs', 'logs'], ['/privacy', 'privacy'],
  ];
  for (const [r, name] of routes) {
    await page.goto(r).catch(() => {});
    await sleep(r === '/' ? 3500 : 2600);
    await shot(name);
  }

  // --- Editor + estados ---
  const projects = new ProjectsPage(page);
  const editor = new EditorPage(page);
  const runbar = new RunBar(page);
  const CLICK = { timeout: 12_000 };
  await projects.goto().catch(() => {});
  await projects.removeProject(PROJ).catch(() => {});
  await projects.createProject(PROJ, { templateId: 'counter' }).catch(() => {});
  await projects.openProject(PROJ).catch(() => {});
  await editor.waitForReady().catch(() => {});
  await sleep(2200);

  const viewTabs = page.locator('.editor-workbench__view-tabs button.kmila-btn-tab');
  const nView = await viewTabs.count();
  await shot('editor');
  if (nView > 1) { await viewTabs.nth(1).click(CLICK).catch(() => {}); await sleep(1800); await shot('blocks'); }
  await viewTabs.nth(0).click(CLICK).catch(() => {}); await sleep(600);
  try { await runbar.pickFirstFpga(); await sleep(700); await shot('fpga'); } catch {}
  try { await runbar.build(); await runbar.waitForBuildIdle(); await sleep(1200); } catch {}
  try { await runbar.runBtn.click(CLICK); await sleep(5000); } catch {}
  try { await editor.openDockTab('Waveforms'); await sleep(1100); await shot('waveforms'); } catch {}
  try {
    await editor.openDockTab('Schematic');
    const modes = page.locator('.schematic-viewer__modes button.schematic-mode');
    if (await modes.count() >= 3) { await modes.nth(2).click(CLICK); await sleep(1100); }
    await shot('schematic');
  } catch {}
  if (nView > 2) { await viewTabs.nth(2).click(CLICK).catch(() => {}); await sleep(1600); await shot('replay'); }
  if (nView > 0) {
    await viewTabs.nth(nView - 1).click(CLICK).catch(() => {}); await sleep(1800);
    const vr = page.locator('[data-testid="visual-run"]');
    if (await vr.count()) { await vr.click(CLICK).catch(() => {}); await sleep(2500); }
    await shot('visual');
  }

  await device.close().catch(() => {});
});
