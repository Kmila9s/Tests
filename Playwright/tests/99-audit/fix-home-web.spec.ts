/** Re-captura la Home web sin el tour de bienvenida. */
import { test } from '@playwright/test';
import * as path from 'path';
const OUT = '/Users/miku/Documents/Kmila-9s/Documentacion/TT2/images/gallery';

test('fix home web', async ({ page }) => {
  await page.goto('/');
  await page.waitForTimeout(2500);
  for (const g of [
    page.getByRole('button', { name: /saltar|omitir|skip/i }),
    page.getByText('Saltar tour', { exact: false }),
    page.getByText('Saltar', { exact: false }),
    page.getByText('Skip tour', { exact: false }),
  ]) {
    try { const e = g.first(); if ((await e.count()) && (await e.isVisible())) { await e.click({ timeout: 4000 }); await page.waitForTimeout(600); } } catch {}
  }
  // por si queda algo del coachmark
  await page.evaluate(() => {
    const nodes = Array.from(document.querySelectorAll('div,section,aside'));
    for (const n of nodes) {
      const t = n.textContent || '';
      if (/bienvenida a Kmila|Welcome to Kmila|PASO \d+ DE|STEP \d+ OF/i.test(t) && (n as HTMLElement).offsetWidth > 150 && t.length < 400) {
        let p: HTMLElement | null = n as HTMLElement;
        for (let i = 0; i < 5 && p; i++) { const c = (p.className || '').toString(); if (/backdrop|overlay|tour|coach|modal/i.test(c)) break; p = p.parentElement; }
        (p || (n as HTMLElement)).remove();
      }
    }
  }).catch(() => {});
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(OUT, 'gal_home_web.png') });
});
