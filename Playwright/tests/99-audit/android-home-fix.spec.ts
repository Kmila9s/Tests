/** Descarta el tour de bienvenida y re-captura las rutas de contenido en Android. */
import { test, _android as android } from '@playwright/test';
import * as path from 'path';

const OUT = '/Users/miku/Documents/Kmila-9s/Documentacion/TT2/images/gallery';
const BASE = 'https://0.0.0.1';
const PKG = 'kmila.kmila';
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

test('android dismiss tour + recap', async () => {
  test.setTimeout(300_000);
  const [device] = await android.devices();
  await device.shell(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`).catch(() => {});
  await sleep(5000);
  const wv = await device.webView({ pkg: PKG });
  const page = await wv.page();
  page.setDefaultTimeout(30_000);
  const _g = page.goto.bind(page);
  (page as any).goto = (u: string, o?: any) => _g(u.startsWith('/') ? BASE + u : u, o);
  const shot = async (n: string) => { await device.screenshot({ path: path.join(OUT, `gal_${n}_android.png`) }); };

  const killTour = async () => {
    // 1) clic en "Skip tour" por texto (es un enlace/botón)
    for (const g of [page.getByText('Skip tour', { exact: false }), page.getByText('Saltar', { exact: false }), page.getByRole('button', { name: /skip|saltar|omitir/i })]) {
      try { const el = g.first(); if (await el.count() && await el.isVisible()) { await el.click({ timeout: 4000 }); await sleep(700); } } catch {}
    }
    // 2) fijar flags y quitar cualquier resto del coachmark del DOM
    await page.evaluate(() => {
      try { for (const k of ['kmila.tutorialSeen', 'kmila.tutorialSeen.projects', 'kmila.tutorialSeen.editor', 'kmila.tourSeen', 'kmila.welcomeSeen']) localStorage.setItem(k, '1'); } catch {}
      const nodes = Array.from(document.querySelectorAll('div,section,aside'));
      for (const n of nodes) {
        const t = (n.textContent || '');
        if (/Welcome to Kmila|Bienvenido a Kmila|STEP \d+ OF|PASO \d+ DE/.test(t) && (n as HTMLElement).offsetWidth > 150 && t.length < 400) {
          let p: HTMLElement | null = n as HTMLElement;
          for (let i = 0; i < 4 && p; i++) { if (p.parentElement && (p.parentElement.className || '').toString().match(/backdrop|overlay|tour|coach|modal/i)) { p = p.parentElement; break; } p = p.parentElement; }
          (p || n).remove();
        }
      }
      document.querySelectorAll('[class*="backdrop"],[class*="overlay"]').forEach((e) => { if (/tour|coach/i.test((e as HTMLElement).className)) e.remove(); });
    }).catch(() => {});
    await sleep(500);
  };

  const waitContent = async () => {
    await page.waitForFunction(() => { const t = (document.body?.innerText || '').trim(); return t.replace(/\s/g, '').length > 200 && !/^Loading/i.test(t); }, { timeout: 45_000 }).catch(() => {});
  };

  for (const [r, n] of [['/', 'home'], ['/projects', 'projects'], ['/learn', 'learn'], ['/practice', 'practice'], ['/concepts', 'concepts'], ['/settings', 'settings']] as [string, string][]) {
    await page.goto(r).catch(() => {});
    await waitContent();
    await killTour();
    await sleep(1200);
    await shot(n);
  }
  await device.close().catch(() => {});
});
