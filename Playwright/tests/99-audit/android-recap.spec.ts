/** Re-captura robusta de las 8 rutas Android (espera a que el runtime híbrido
 * termine de arrancar tras cada recarga: contenido real, no "Loading..."). */
import { test, _android as android } from '@playwright/test';
import * as path from 'path';

const OUT = '/Users/miku/Documents/Kmila-9s/Documentacion/TT2/images/gallery';
const BASE = 'https://0.0.0.1';
const PKG = 'kmila.kmila';
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

test('android recap routes', async () => {
  test.setTimeout(400_000);
  const [device] = await android.devices();
  await device.shell(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`).catch(() => {});
  await sleep(5000);
  const wv = await device.webView({ pkg: PKG });
  const page = await wv.page();
  page.setDefaultTimeout(30_000);
  const _g = page.goto.bind(page);
  (page as any).goto = (u: string, o?: any) => _g(u.startsWith('/') ? BASE + u : u, o);
  await page.addInitScript(() => {
    try { for (const k of ['kmila.tutorialSeen', 'kmila.tutorialSeen.projects', 'kmila.tutorialSeen.editor']) localStorage.setItem(k, '1'); } catch {}
  });
  const shot = async (n: string) => { await device.screenshot({ path: path.join(OUT, `gal_${n}_android.png`) }); };
  const dismissTour = async () => {
    for (const sel of ['button:has-text("Skip tour")', 'button:has-text("Saltar")', 'button:has-text("Skip")', '.kmila-tour__skip', '[data-tour-skip]']) {
      const b = page.locator(sel).first();
      if (await b.count() && await b.isVisible().catch(() => false)) { await b.click().catch(() => {}); await sleep(600); break; }
    }
  };
  const waitContent = async () => {
    await page.waitForFunction(() => {
      const t = (document.body?.innerText || '').trim();
      return t.replace(/\s/g, '').length > 200 && !/^Loading/i.test(t);
    }, { timeout: 45_000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  };
  for (const [r, n] of [
    ['/', 'home'], ['/projects', 'projects'], ['/learn', 'learn'], ['/practice', 'practice'],
    ['/concepts', 'concepts'], ['/settings', 'settings'], ['/logs', 'logs'], ['/privacy', 'privacy'],
  ] as [string, string][]) {
    await page.goto(r).catch(() => {});
    await waitContent();
    await dismissTour();
    await sleep(1600);
    await shot(n);
  }
  await device.close().catch(() => {});
});
