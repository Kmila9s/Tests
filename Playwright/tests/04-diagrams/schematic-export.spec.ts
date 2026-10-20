import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { RunBar } from '../../pageobjects/RunBar';

/**
 * Phase 04 — schematic PNG export sweep.
 *
 * For each of four built-in example projects (dip-adder, traffic,
 * shift, capstone-calc), this suite opens the project, switches to
 * the Schematic dock, and exports the schematic to PNG under every
 * combination of scale (1×/2×/3×), background (White/Dark), and the
 * new SchematicRenderMode (Human/Raw). Each PNG is saved to
 * `artifacts/schematic/{demo}_scale{N}_{bg}_{mode}.png` for external
 * review — the test itself only asserts PNG magic bytes; the visual
 * "human-friendly" check is done by reading the saved files afterwards.
 *
 * Selectors:
 *   - Example gallery cards carry `data-example-id="{id}"` (added to
 *     ProjectsView.razor so tests don't have to match localized titles).
 *   - Export toolbar button lives inside `.schematic-viewer__actions`
 *     and has an accent-styled class; we target it by title attribute
 *     falling back to the icon+text combo.
 *   - Modal form uses stable ids: #schex-format, #schex-scale,
 *     #schex-bg, #schex-mode, plus a submit inside `.kmila-export-footer`.
 */

// The four demos under test. `slug` is what lands in the filename; `id`
// is the `data-example-id` attribute on the gallery card.
// `mainFile` names the file the example uses as its top-level entity —
// we mark it as the project's entry point before building. Single-file
// examples land the design in `main.vhd`; capstone-calc ships 15 files
// with `calc_top.vhd` as its top.
const DEMOS: Array<{ id: string; slug: string; complexity: string; mainFile: string }> = [
  { id: 'dip-adder', slug: 'dip-adder', complexity: 'trivial combinational', mainFile: 'main.vhd' },
  { id: 'traffic', slug: 'traffic', complexity: 'moore FSM', mainFile: 'main.vhd' },
  { id: 'shift', slug: 'shift', complexity: 'bit-slice fan-out', mainFile: 'main.vhd' },
  { id: 'capstone-calc', slug: 'capstone-calc', complexity: 'hierarchical CPU', mainFile: 'calc_top.vhd' },
];

const SCALES: Array<{ value: string; slug: string }> = [
  { value: '1', slug: 'scale1' },
  { value: '2', slug: 'scale2' },
  { value: '3', slug: 'scale3' },
];

const BACKGROUNDS: Array<{ value: string; slug: string }> = [
  { value: 'White', slug: 'light' },
  { value: 'Dark', slug: 'dark' },
];

const MODES: Array<{ value: string; slug: string }> = [
  { value: 'Human', slug: 'human' },
  { value: 'Raw', slug: 'raw' },
];

const ARTIFACT_DIR = path.resolve(__dirname, '../../artifacts/schematic');

async function openExampleGallery(page: Page) {
  await expect(page.locator('.projects-header__title')).toBeVisible({ timeout: 20_000 });
  // Blazor-Server can carry over a `.kviz-wiring-backdrop--open` element
  // from a previous circuit's state; JS-removing it is ineffective because
  // Blazor re-renders it from server state. A page.reload() forces a
  // fresh circuit whose IsOpen defaults to false, which is the reliable
  // way to shed the overlay.
  if (
    await page
      .locator('.kviz-wiring-backdrop--open, .modal-backdrop')
      .first()
      .isVisible({ timeout: 500 })
      .catch(() => false)
  ) {
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(page.locator('.projects-header__title')).toBeVisible({ timeout: 20_000 });
  }
  const examplesBtn = page.locator('button.projects-examples').first();
  await expect(examplesBtn).toBeVisible({ timeout: 15_000 });
  await examplesBtn.click({ timeout: 10_000 });
  const modal = page.locator('.examples-gallery-modal');
  if (!(await modal.isVisible({ timeout: 3_000 }).catch(() => false))) {
    await examplesBtn.click({ timeout: 10_000 });
  }
  await expect(modal).toBeVisible({ timeout: 15_000 });
}

async function pickExample(page: Page, exampleId: string) {
  const card = page.locator(`button.examples-gallery-card[data-example-id="${exampleId}"]`);
  await expect(card).toBeVisible({ timeout: 10_000 });
  await card.click();
  // The example flow creates the project on the server then routes to /editor/{id}.
  // Capstone Calculator ships 15 VHDL files so allow a wider window here.
  await page.waitForURL(/\/editor\/.+/, { timeout: 60_000 });
}

async function buildAndOpenSchematic(page: Page, mainFile: string) {
  const editor = new EditorPage(page);
  const runbar = new RunBar(page);
  await editor.waitForReady();
  await editor.openDockTab('Files');
  await editor.files.toggleEntryPoint(mainFile);
  await runbar.pickFirstFpga();
  await runbar.build();
  await runbar.waitForBuildIdle(120_000);
  await editor.openDockTab('Schematic');
  // Ensure the SVG has painted at least one cell before we start
  // exporting — otherwise the exporter would receive an empty layout.
  await expect(page.locator('svg.schematic-svg')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('.schematic-viewer__chip')).toBeVisible({ timeout: 15_000 });
}

async function exportOne(
  page: Page,
  demoSlug: string,
  scale: { value: string; slug: string },
  bg: { value: string; slug: string },
  mode: { value: string; slug: string },
): Promise<{ file: string; bytes: number }> {
  // Open the modal via the toolbar Export button. The button has no
  // stable test-id so we target the accent-styled action in the
  // schematic toolbar — there is only one accent action in that scope.
  const exportBtn = page.locator('.schematic-viewer__actions .kmila-btn--accent').first();
  await expect(exportBtn).toBeVisible();
  await exportBtn.click();

  // Modal fields.
  await page.locator('#schex-format').selectOption('Png');
  await page.locator('#schex-scale').selectOption(scale.value);
  await page.locator('#schex-bg').selectOption(bg.value);
  await page.locator('#schex-mode').selectOption(mode.value);

  const submit = page.locator('.kmila-export-footer .kmila-btn--accent').first();
  const [download] = await Promise.all([
    page.waitForEvent('download', { timeout: 60_000 }),
    submit.click(),
  ]);
  expect(download.suggestedFilename()).toMatch(/\.png$/i);

  // Wait for the modal + backdrop to fully unmount. For big schematics
  // (capstone-calc) the BB fade takes longer than the default; if it's
  // still around after a generous window, force-remove so the next
  // iteration's Export click isn't intercepted.
  const stillMounted = async () =>
    (await page.locator('.modal.show, .modal-backdrop').count()) > 0;
  await expect
    .poll(stillMounted, { timeout: 20_000, intervals: [200, 500, 1000] })
    .toBe(false)
    .catch(async () => {
      await page.evaluate(() => {
        document
          .querySelectorAll('.modal.show, .modal-backdrop')
          .forEach((el) => el.remove());
      });
    });

  const file = path.join(
    ARTIFACT_DIR,
    `${demoSlug}_${scale.slug}_${bg.slug}_${mode.slug}.png`,
  );
  await download.saveAs(file);

  const buf = fs.readFileSync(file);
  // PNG magic bytes 89 50 4E 47 0D 0A 1A 0A.
  expect(buf.byteLength).toBeGreaterThan(100);
  expect(buf[0]).toBe(0x89);
  expect(buf[1]).toBe(0x50);
  expect(buf[2]).toBe(0x4e);
  expect(buf[3]).toBe(0x47);

  return { file, bytes: buf.byteLength };
}

test.describe('Schematic PNG export — full sweep across built-in demos', () => {
  test.setTimeout(600_000);

  test.beforeAll(() => {
    fs.mkdirSync(ARTIFACT_DIR, { recursive: true });
  });

  for (const demo of DEMOS) {
    test(`sweep — ${demo.id} (${demo.complexity})`, async ({ page }) => {
      const projects = new ProjectsPage(page);
      await projects.goto();
      await openExampleGallery(page);
      await pickExample(page, demo.id);
      await buildAndOpenSchematic(page, demo.mainFile);

      const results: Array<{ file: string; bytes: number }> = [];
      for (const mode of MODES) {
        for (const bg of BACKGROUNDS) {
          for (const scale of SCALES) {
            const r = await exportOne(page, demo.slug, scale, bg, mode);
            results.push(r);
          }
        }
      }
      // 3 scales × 2 bgs × 2 modes = 12 files per demo.
      expect(results.length).toBe(12);
      for (const r of results) {
        // Extra floor so an under-rasterised (blank) PNG surfaces here
        // rather than passing the magic-byte check but arriving as ~1kB.
        expect(r.bytes).toBeGreaterThan(500);
      }
    });
  }
});
