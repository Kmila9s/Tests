import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';

async function shortGoto(page: Page, url: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
}

async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}

test.describe('Phase B — Editor DOM audit', () => {
  test.setTimeout(180_000);

  test('capture editor DOM state as JSON', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });

    // 1) Get an editor URL
    await shortGoto(page, '/projects');
    await dismissTour(page);
    const href = await page.locator('a[href*="/editor/"]').first().getAttribute('href');
    if (!href) throw new Error('No editor href');

    await shortGoto(page, href);
    await dismissTour(page);
    await page.waitForTimeout(3500); // Monaco hydration

    const state: any = { editorHref: href, sections: {} };

    // 2) Workbench tabs — target the .kmila-btn-tab in the main editor area
    // (excluding modal / topbar tabs)
    const workbench = await page.evaluate(() => {
      const tabs = Array.from(document.querySelectorAll<HTMLButtonElement>(
        '.kmila-editor-tabstrip .kmila-btn-tab, [class*="EditorTabStrip"] .kmila-btn-tab, .editor-tabs .kmila-btn-tab'
      ));
      return tabs.map(t => ({
        text: (t.textContent ?? '').trim(),
        active: t.getAttribute('aria-selected') === 'true' || t.classList.contains('active') || t.classList.contains('is-active'),
        classes: t.className,
      }));
    });
    state.sections.workbenchTabs = workbench;

    // 3) Dock tabs — inside EditorDock component
    const dock = await page.evaluate(() => {
      // Try several selectors — dock might have a specific class
      const dockRoot = document.querySelector('.kmila-editor-dock, [class*="EditorDock"], .editor-dock');
      if (!dockRoot) return { found: false };
      const tabs = Array.from(dockRoot.querySelectorAll<HTMLButtonElement>('.kmila-btn-tab, button[role="tab"], [class*="dock-tab"]'));
      return {
        found: true,
        tabs: tabs.map(t => ({
          text: (t.textContent ?? '').trim(),
          active: t.getAttribute('aria-selected') === 'true' || t.classList.contains('active'),
          hasBadge: !!t.querySelector('.badge, [class*="badge"], .kmila-pip, [class*="pip"]'),
        })),
      };
    });
    state.sections.dockTabs = dock;

    // 4) RunBar
    const runbar = await page.evaluate(() => {
      const bar = document.querySelector('.kmila-runbar, [class*="RunBar"], .runbar');
      if (!bar) return { found: false };
      const buttons = Array.from(bar.querySelectorAll('button'));
      return {
        found: true,
        buttons: buttons.map(b => ({
          text: (b.textContent ?? '').trim().slice(0, 60),
          title: b.getAttribute('title'),
          disabled: (b as HTMLButtonElement).disabled,
          aria: b.getAttribute('aria-label'),
        })),
      };
    });
    state.sections.runbar = runbar;

    // 5) FilesCard — file list panel
    const files = await page.evaluate(() => {
      const card = document.querySelector('.kmila-files-card, [class*="FilesCard"], .files-card');
      if (!card) return { found: false };
      const items = Array.from(card.querySelectorAll('[class*="file-row"], li, .list-group-item'));
      return {
        found: true,
        count: items.length,
        firstFew: items.slice(0, 8).map(i => ((i.textContent ?? '').trim().slice(0, 80))),
      };
    });
    state.sections.filesCard = files;

    // 6) ConfigurationsPill
    const pill = await page.evaluate(() => {
      const p = document.querySelector('.configurations-pill, [class*="ConfigurationsPill"]');
      if (!p) return { found: false };
      return {
        found: true,
        text: (p.textContent ?? '').trim().slice(0, 200),
      };
    });
    state.sections.configPill = pill;

    // 7) Monaco presence
    const monaco = await page.evaluate(() => {
      const editors = Array.from(document.querySelectorAll('.monaco-editor'));
      return {
        count: editors.length,
        firstHasContent: editors[0] ? !!editors[0].textContent?.length : false,
      };
    });
    state.sections.monaco = monaco;

    // 8) BlockEditor host
    const blocks = await page.evaluate(() => {
      const host = document.querySelector('.kmila-block-editor, [class*="BlockEditor"], [class*="block-editor"]');
      return { found: !!host };
    });
    state.sections.blockEditor = blocks;

    // 9) VisualPanel host
    const vp = await page.evaluate(() => {
      const host = document.querySelector('.kmila-visual-panel, [class*="VisualPanel"], .visual-panel');
      return { found: !!host };
    });
    state.sections.visualPanel = vp;

    // 10) Full editor screenshot at cold-open state (single image, small)
    await page.screenshot({ path: path.join(EVIDENCE_DIR, 'EVID_B_editor_state.png'), fullPage: false });

    // Try switching to Blocks tab and capture its DOM state
    const blocksTab = page.locator('.kmila-btn-tab').filter({ hasText: /^\s*(Bloques|Blocks)\s*$/i }).first();
    if (await blocksTab.count()) {
      await blocksTab.click().catch(() => {});
      await page.waitForTimeout(1200);
      const blocksSurface = await page.evaluate(() => {
        const svg = document.querySelectorAll('.kmila-block-editor svg, [class*="BlockEditor"] svg, .kmila-block-canvas');
        return { svgCount: svg.length };
      });
      state.sections.blocksTabActive = blocksSurface;
    }

    // Try Visual tab
    const visualTab = page.locator('.kmila-btn-tab').filter({ hasText: /^\s*Visual\s*$/i }).first();
    if (await visualTab.count()) {
      await visualTab.click().catch(() => {});
      await page.waitForTimeout(1200);
      const visualSurface = await page.evaluate(() => {
        const canvas = document.querySelector('.kmila-visual-canvas, [class*="VisualCanvas"], .visual-canvas');
        const palette = document.querySelector('.kmila-widget-palette, [class*="WidgetPalette"]');
        return {
          canvas: !!canvas,
          palette: !!palette,
          paletteItems: palette ? palette.querySelectorAll('button, .kmila-widget-item, li').length : 0,
        };
      });
      state.sections.visualTabActive = visualSurface;
    }

    // Try Replay tab
    const replayTab = page.locator('.kmila-btn-tab').filter({ hasText: /^\s*(Reproducción|Replay|Playback)\s*$/i }).first();
    if (await replayTab.count()) {
      await replayTab.click().catch(() => {});
      await page.waitForTimeout(800);
      const replaySurface = await page.evaluate(() => {
        const host = document.querySelector('.kmila-step-replay, [class*="StepReplayPanel"]');
        return { found: !!host };
      });
      state.sections.replayTabActive = replaySurface;
    }

    // Emit findings
    const outPath = path.join(EVIDENCE_DIR, 'phaseB_dom.json');
    fs.writeFileSync(outPath, JSON.stringify(state, null, 2));
    console.log('PHASEB_DOM_JSON_SAVED_TO:', outPath);
    console.log('PHASEB_SUMMARY:', JSON.stringify({
      workbenchTabCount: state.sections.workbenchTabs?.length,
      dockTabCount: state.sections.dockTabs?.tabs?.length,
      runbarButtonCount: state.sections.runbar?.buttons?.length,
      monacoCount: state.sections.monaco?.count,
      filesCount: state.sections.filesCard?.count,
    }, null, 2));
  });
});
