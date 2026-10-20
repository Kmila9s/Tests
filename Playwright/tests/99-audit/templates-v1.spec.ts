/**
 * v1 Project Templates — end-to-end verification (goal-set for v1.23.0).
 *
 * Flow:
 *   1. Create a library project from the gallery, tick "Add to Kmila libraries".
 *   2. Create a second library project (different name → different package).
 *   3. Create an Empty consumer project, rewrite main.vhd to `use` both
 *      libraries, build it, and run the simulation.
 *   4. Assert that each project landed on disk with multiple files in
 *      multiple directories (the goal: prove Kmila handles multi-file +
 *      multi-dir scaffolds end-to-end, not just a single main.vhd).
 *   5. Assert no danger rows in the Output panel after build, and the
 *      simulation completes without crashing.
 *
 * Self-cleaning: removes the three created projects on teardown so a
 * second run starts from a clean slate.
 */
import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { Monaco } from '../../pageobjects/Monaco';
import { RunBar } from '../../pageobjects/RunBar';
import { OutputPanel } from '../../pageobjects/OutputPanel';

const STAMP = Date.now().toString(36);
const LIB_A = `LibAlpha-${STAMP}`;
const LIB_B = `LibBravo-${STAMP}`;
const CONSUMER = `Consumer-${STAMP}`;

// Sanitized identifiers (must mirror ProjectTemplateService.SanitizeIdentifier).
const sanitize = (raw: string) =>
  raw
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '_')
    .replace(/^_+|_+$/g, '');
const LIB_A_PKG = sanitize(LIB_A);
const LIB_B_PKG = sanitize(LIB_B);

const CONSUMER_SOURCE = `library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

library WORK;
use WORK.${LIB_A_PKG}.all;
use WORK.${LIB_B_PKG}.all;

entity consumer is
  port (
    clk    : in  STD_LOGIC;
    reset  : in  STD_LOGIC;
    a_in   : in  STD_LOGIC_VECTOR(7 downto 0);
    b_in   : in  STD_LOGIC_VECTOR(7 downto 0);
    sum    : out STD_LOGIC_VECTOR(7 downto 0);
    popped : out STD_LOGIC_VECTOR(3 downto 0)
  );
end entity consumer;

architecture rtl of consumer is
  signal sum_reg : STD_LOGIC_VECTOR(7 downto 0) := (others => '0');
  signal pop_reg : unsigned(3 downto 0) := (others => '0');
begin
  process(clk, reset)
  begin
    if reset = '1' then
      sum_reg <= (others => '0');
      pop_reg <= (others => '0');
    elsif rising_edge(clk) then
      sum_reg <= ${LIB_A_PKG}.sat_add(a_in, b_in);
      pop_reg <= ${LIB_B_PKG}.popcount(sum_reg);
    end if;
  end process;

  sum    <= sum_reg;
  popped <= STD_LOGIC_VECTOR(pop_reg);
end architecture rtl;
`;

test.describe('Project templates v1 — gallery + library round-trip', () => {
  // Cold build + sim takes time on first project of a session.
  test.setTimeout(300_000);

  test.beforeEach(async ({ page }) => {
    // The welcome tour (kmila-tutorial.js) opens on a fresh /projects
    // visit and intercepts clicks. Pre-mark every tour as seen so the
    // gallery flow runs unobstructed.
    await page.addInitScript(() => {
      try {
        localStorage.setItem('kmila.tutorialSeen', '1');
        localStorage.setItem('kmila.tutorialSeen.projects', '1');
        localStorage.setItem('kmila.tutorialSeen.editor', '1');
      } catch { /* sandboxed contexts: best-effort */ }
    });
  });

  test.afterEach(async ({ page }) => {
    const projects = new ProjectsPage(page);
    for (const name of [CONSUMER, LIB_B, LIB_A]) {
      try {
        await projects.goto();
        await projects.removeProject(name, 'yes');
      } catch {
        /* best-effort */
      }
    }
  });

  test('library auto-import + consumer build + run', async ({ page }) => {
    const projects = new ProjectsPage(page);
    const editor = new EditorPage(page);
    const monaco = new Monaco(page);
    const runbar = new RunBar(page);

    // ---- Step 1: library A ----
    await projects.goto();
    await projects.createProject(LIB_A, { templateId: 'library', registerAsLibrary: true });

    // The new project lives at /editor/{id}; we returned to /projects via the
    // page object. Open it to inspect the file tree.
    await projects.openProject(LIB_A);
    await editor.waitForReady();
    await editor.openDockTab('Files');

    // Multi-file multi-dir assertion: pkg.vhd + pkg_body.vhd at root + docs/README.md.
    await expect(editor.files.fileRowByName('pkg.vhd')).toBeVisible();
    await expect(editor.files.fileRowByName('pkg_body.vhd')).toBeVisible();
    await expect(editor.files.folderRowByName('docs')).toBeVisible();

    // ---- Step 2: library B ----
    await projects.goto();
    await projects.createProject(LIB_B, { templateId: 'library', registerAsLibrary: true });

    await projects.openProject(LIB_B);
    await editor.waitForReady();
    await editor.openDockTab('Files');
    await expect(editor.files.fileRowByName('pkg.vhd')).toBeVisible();
    await expect(editor.files.fileRowByName('pkg_body.vhd')).toBeVisible();
    await expect(editor.files.folderRowByName('docs')).toBeVisible();

    // ---- Step 3: consumer ----
    await projects.goto();
    await projects.createProject(CONSUMER, { templateId: 'empty' });

    await projects.openProject(CONSUMER);
    await editor.waitForReady();
    await editor.openDockTab('Files');

    // Empty template also ships multi-dir (main.vhd + docs/README.md).
    await expect(editor.files.fileRowByName('main.vhd')).toBeVisible();
    await expect(editor.files.folderRowByName('docs')).toBeVisible();

    // ---- Step 4: rewrite main.vhd to use both libraries ----
    await editor.files.fileRowByName('main.vhd').locator('.file-row__name').click();
    await monaco.setValue(CONSUMER_SOURCE);
    await runbar.save();
    await expect(page.locator('.editor-unsaved')).toBeHidden({ timeout: 10_000 });
    await editor.files.toggleEntryPoint('main.vhd');

    // ---- Step 5: build + run + assert success ----
    await runbar.pickFirstFpga();
    await runbar.build();
    await runbar.waitForBuildIdle();

    await editor.openDockTab('Output');
    const out = new OutputPanel(page);
    // Build with both libraries linked should succeed and produce no danger
    // rows. This is the load-bearing assertion: it proves library
    // auto-import + LibraryCompiler discovery actually wired the consumer
    // to both `liba` and `libb` packages.
    await out.waitForSuccess();
    await expect(out.dangerRows).toHaveCount(0);

    // ---- Step 6: actually execute the simulation ----
    // Run with a short window (1µs default is fine for a simple counter
    // process). The run button is only enabled once Build is idle and the
    // FPGA is picked, both of which are true at this point.
    await runbar.runBtn.click();
    // Sim end is signalled by the build/run spinner going away and the
    // simulation state returning to Idle/Complete. waitForBuildIdle reuses
    // the same spinner selector pattern.
    await expect(runbar.runBtn.locator('.spinner-border'))
      .toHaveCount(0, { timeout: 60_000 });
    // Output panel should still hold the prior success row; danger rows
    // should remain zero after the sim run.
    await editor.openDockTab('Output');
    await expect(out.dangerRows).toHaveCount(0);
  });
});
