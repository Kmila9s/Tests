import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseD_state.json');

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

test.describe('Phase D — Visual runtime', () => {
  test.setTimeout(180_000);

  test('enumerate visual palette + placed widgets on demo_board', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = { projects: {} };

    // First: get a list of all project /editor/{id} hrefs from Projects page
    await shortGoto(page, '/projects');
    await dismissTour(page);
    const projectLinks = await page.evaluate(() => {
      const anchors = Array.from(document.querySelectorAll<HTMLAnchorElement>('a[href*="/editor/"]'));
      return anchors.map(a => ({ href: a.getAttribute('href'), text: (a.textContent ?? '').trim().slice(0, 80) }));
    });
    state.projectList = projectLinks;
    save(state);

    // demo_board specifically — matched by name containing "Tablero"
    const demoBoard = projectLinks.find(l => /Tablero/i.test(l.text ?? ''));
    if (!demoBoard || !demoBoard.href) throw new Error('demo_board not found');
    state.demoBoardHref = demoBoard.href;
    save(state);

    // Extract projectId from href e.g. /editor/411 → 411
    const idMatch = demoBoard.href.match(/\/editor\/([^/?#]+)/);
    if (!idMatch) throw new Error('no id in demoBoard href');
    const projectId = idMatch[1];
    state.projectId = projectId;

    // Go to /visual/{projectId}
    await shortGoto(page, `/visual/${projectId}`);
    await dismissTour(page);
    await page.waitForTimeout(4000); // let visual canvas hydrate

    // Enumerate palette items
    const palette = await page.evaluate(() => {
      const p = document.querySelector('.kmila-widget-palette, [class*="WidgetPalette"], .widget-palette');
      if (!p) return { found: false };
      const items = Array.from(p.querySelectorAll('button, [class*="palette-item"], [class*="widget-item"], li'))
        .filter(i => (i.textContent ?? '').trim().length > 0);
      return {
        found: true,
        count: items.length,
        items: items.map(i => ({
          text: (i.textContent ?? '').trim().slice(0, 40),
          type: i.getAttribute('data-widget-type') || i.getAttribute('data-type') || null,
          draggable: i.getAttribute('draggable') === 'true',
        })),
      };
    });
    state.palette = palette;
    save(state);

    // Enumerate placed widgets on canvas
    const placed = await page.evaluate(() => {
      const canvas = document.querySelector('.kmila-visual-canvas, [class*="VisualCanvas"], .visual-canvas');
      if (!canvas) return { found: false };
      const widgets = Array.from(canvas.querySelectorAll('[data-widget-type], [class*="widget-"], [class*="kviz-"]'));
      return {
        found: true,
        count: widgets.length,
        firstFew: widgets.slice(0, 15).map(w => ({
          type: w.getAttribute('data-widget-type') || w.className.match(/(?:kviz|widget)-([a-z0-9-]+)/)?.[1] || '?',
          label: (w.querySelector('[class*="label"]')?.textContent ?? '').trim().slice(0, 20),
        })),
      };
    });
    state.placedWidgets = placed;
    save(state);

    // Try to find action bar: Ejecutar, Conectar, delete, undo, redo, Guardado, Descartar
    const actions = await page.evaluate(() => {
      const bar = document.querySelector('.visual-actions, [class*="VisualPanel"] .kmila-btn, [class*="visual-toolbar"]');
      // Fallback: any visible buttons at the top of VisualPanel
      const allButtons = Array.from(document.querySelectorAll<HTMLButtonElement>('button'))
        .filter(b => {
          const r = b.getBoundingClientRect();
          return r.top < 300 && r.left < 1000 && r.width > 0 && r.height > 0;
        })
        .map(b => ({ text: (b.textContent ?? '').trim().slice(0, 40), title: b.title }));
      return { topBarButtons: allButtons.slice(0, 30) };
    });
    state.actions = actions;
    save(state);

    // Try clicking each palette item to see if wiring modal opens (or widget adds to canvas)
    // Only sample first 3 palette items to avoid timeout
    const paletteInteract: any[] = [];
    if (palette.found && palette.items) {
      for (let i = 0; i < Math.min(3, palette.items.length); i++) {
        const it = palette.items[i];
        // We don't fully interact — just test if we can hover / drag it
        paletteInteract.push({ index: i, label: it.text, type: it.type, tested: false, note: 'drag-drop interaction requires manual verify' });
      }
    }
    state.paletteInteract = paletteInteract;
    save(state);

    // Look for pre-wired example gallery link — memory mentions Demo Board / Counter Clock / Traffic Light FSM / 8-bit Shift Register
    const examples = await page.evaluate(() => {
      // The example gallery might be in a modal or on the projects page
      const galleryLinks = Array.from(document.querySelectorAll<HTMLAnchorElement>('a'))
        .filter(a => /Demo Board|Counter Clock|Traffic Light|Shift Register/i.test((a.textContent ?? '')));
      return galleryLinks.map(a => ({ text: (a.textContent ?? '').trim().slice(0, 60), href: a.getAttribute('href') }));
    });
    state.examples = examples;
    save(state);

    console.log('DONE_PHASE_D');
  });
});
