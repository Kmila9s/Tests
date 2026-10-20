import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { Monaco } from '../../pageobjects/Monaco';
import { RunBar } from '../../pageobjects/RunBar';

const HALF_ADDER_OK = fs.readFileSync(
  path.resolve(__dirname, '../../data/vhdl/half-adder-ok.vhdl'),
  'utf8',
);

/**
 * Phase 04 — diagrams render + export.
 *
 * Each test creates a fresh project, types a half-adder, builds, and exercises
 * one diagram surface. Project cleanup runs best-effort in afterEach.
 */
test.describe('Editor — diagram render + export', () => {
  test.setTimeout(180_000);
  let projectName: string;

  test.beforeEach(async ({ page }) => {
    projectName = `Diag-${Date.now()}`;
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.createProject(projectName);
    await projects.openProject(projectName);
    const editor = new EditorPage(page);
    await editor.waitForReady();
    await editor.openDockTab('Files');
  });

  test.afterEach(async ({ page }) => {
    try {
      const projects = new ProjectsPage(page);
      await projects.goto();
      await projects.removeProject(projectName, 'yes');
    } catch {
      /* ignore */
    }
  });

  async function buildHalfAdder(page: any) {
    const editor = new EditorPage(page);
    const monaco = new Monaco(page);
    const runbar = new RunBar(page);

    await editor.files.createFile('half_adder', 'vhdl');
    await editor.files.fileRowByName('half_adder.vhdl').locator('.file-row__name').click();
    await monaco.setValue(HALF_ADDER_OK);
    await runbar.save();
    await editor.files.toggleEntryPoint('half_adder.vhdl');
    await runbar.pickFirstFpga();
    await runbar.build();
    await runbar.waitForBuildIdle();
    return { editor, monaco, runbar };
  }

  test('schematic SVG renders after a successful build', async ({ page }) => {
    const { editor } = await buildHalfAdder(page);
    await editor.openDockTab('Schematic');
    const svg = page.locator('.schematic-viewer svg').first();
    await expect(svg).toBeVisible({ timeout: 15_000 });
    // The half-adder synthesizes to at least one cell (xor + and).
    await expect(page.locator('.schematic-viewer__chip')).toBeVisible();
  });

  test('flow diagram (Mermaid) renders after build', async ({ page }) => {
    const { editor } = await buildHalfAdder(page);
    await editor.openDockTab('Flow');
    // Mermaid renders asynchronously via the CDN; wait for the <svg> it
    // injects in place of <pre class="mermaid">.
    const svg = page.locator('.flow-diagram svg').first();
    await expect(svg).toBeVisible({ timeout: 20_000 });
  });

  test('flow diagram exports a valid SVG file', async ({ page }) => {
    const { editor } = await buildHalfAdder(page);
    await editor.openDockTab('Flow');
    await expect(page.locator('.flow-diagram svg').first()).toBeVisible({ timeout: 20_000 });

    const exportBtn = page.locator('.flow-diagram__head button', { hasText: 'SVG' }).first();
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      exportBtn.click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/\.svg$/i);

    const savePath = await download.path();
    expect(savePath).toBeTruthy();
    const buf = fs.readFileSync(savePath!);
    expect(buf.byteLength).toBeGreaterThan(100);
    // Magic: SVG files start with `<?xml` or `<svg`.
    const head = buf.slice(0, 80).toString('utf8');
    expect(head).toMatch(/^(<\?xml|<svg)/);
  });

  test('flow diagram exports a valid PNG file', async ({ page }) => {
    const { editor } = await buildHalfAdder(page);
    await editor.openDockTab('Flow');
    await expect(page.locator('.flow-diagram svg').first()).toBeVisible({ timeout: 20_000 });

    const exportBtn = page.locator('.flow-diagram__head button', { hasText: 'PNG' }).first();
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      exportBtn.click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/\.png$/i);

    const savePath = await download.path();
    expect(savePath).toBeTruthy();
    const buf = fs.readFileSync(savePath!);
    expect(buf.byteLength).toBeGreaterThan(100);
    // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
    expect(buf[0]).toBe(0x89);
    expect(buf[1]).toBe(0x50);
    expect(buf[2]).toBe(0x4e);
    expect(buf[3]).toBe(0x47);
  });

  test('waveform renders and VCD exports after a run', async ({ page }) => {
    const { editor, runbar } = await buildHalfAdder(page);

    // Click run — for a built design with no stimulus, the simulator still
    // executes to its configured time and produces waveform data.
    await expect(runbar.runBtn).toBeEnabled({ timeout: 15_000 });
    await runbar.runBtn.click();

    await editor.openDockTab('Waveforms');
    const svg = page.locator('.waveform-viewer svg').first();
    await expect(svg).toBeVisible({ timeout: 60_000 });

    // VCD export: button text contains "VCD" and dispatches the download.
    const vcdBtn = page.locator('.waveform-viewer__actions button', { hasText: 'VCD' }).first();
    await expect(vcdBtn).toBeVisible({ timeout: 10_000 });
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      vcdBtn.click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/\.vcd$/i);

    const savePath = await download.path();
    expect(savePath).toBeTruthy();
    const head = fs.readFileSync(savePath!).slice(0, 200).toString('utf8');
    // VCD files always start with `$version` or `$date` declarations.
    expect(head).toMatch(/\$(version|date|comment|timescale)/);
  });
});
