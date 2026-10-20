import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { Monaco } from '../../pageobjects/Monaco';
import { RunBar } from '../../pageobjects/RunBar';
import { OutputPanel } from '../../pageobjects/OutputPanel';

const HALF_ADDER_OK = fs.readFileSync(
  path.resolve(__dirname, '../../data/vhdl/half-adder-ok.vhdl'),
  'utf8',
);
const HALF_ADDER_BROKEN = fs.readFileSync(
  path.resolve(__dirname, '../../data/vhdl/half-adder-broken.vhdl'),
  'utf8',
);

test.describe('Editor — build flow + diagnostics', () => {
  // Cold parser warmup + simulation can run well past the 30s default.
  test.setTimeout(180_000);

  let projectName: string;

  test.beforeEach(async ({ page }) => {
    projectName = `Build-${Date.now()}`;
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.createProject(projectName, 'Build/run scratch project.');
    await projects.openProject(projectName);
    const editor = new EditorPage(page);
    await editor.waitForReady();
    await editor.openDockTab('Files');
    // Templates v1 (v1.23+) seeds new projects with main.vhd + docs/README.md.
    // This build/run suite drops its own half_adder.vhdl and toggles it
    // as entry; leaving the scaffold's main.vhd in place pulls a second
    // entity into the compile unit and breaks the half-adder assertions.
    // Strip the scaffold so the build sees exactly the file under test.
    await editor.files.clearScaffold();
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

  async function dropAndSave(page: any, content: string) {
    const editor = new EditorPage(page);
    const runbar = new RunBar(page);
    const monaco = new Monaco(page);

    await editor.files.createFile('half_adder', 'vhdl');
    await editor.files.fileRowByName('half_adder.vhdl').locator('.file-row__name').click();
    await monaco.setValue(content);
    await runbar.save();
    await expect(page.locator('.editor-unsaved')).toBeHidden({ timeout: 5_000 });
    await editor.files.toggleEntryPoint('half_adder.vhdl');
    return { editor, runbar, monaco };
  }

  test('valid half-adder builds successfully', async ({ page }) => {
    const { editor, runbar, monaco } = await dropAndSave(page, HALF_ADDER_OK);

    // Sanity: Monaco actually holds the half-adder source.
    expect((await monaco.getValue()).trim().startsWith('library IEEE')).toBe(true);

    await runbar.pickFirstFpga();
    await runbar.build();
    // Build is async; Editor.razor auto-switches the dock to Entities on
    // success right after, so wait for the build to finish FIRST, then
    // explicitly switch the dock to Output.
    await runbar.waitForBuildIdle();
    await editor.openDockTab('Output');
    const out = new OutputPanel(page);
    await out.waitForSuccess();
    await expect(out.dangerRows).toHaveCount(0);
  });

  test('invalid VHDL surfaces a danger row in the Output panel', async ({ page }) => {
    const { editor, runbar } = await dropAndSave(page, HALF_ADDER_BROKEN);

    await runbar.pickFirstFpga();
    await runbar.build();
    await runbar.waitForBuildIdle();

    await editor.openDockTab('Output');
    const out = new OutputPanel(page);
    await out.waitForError();
    await expect(out.successRows).toHaveCount(0);

    // Monaco markers are only published when the parser raises a structured
    // ParserException (line/column-tagged). BuildAsync returning false alone
    // does NOT populate them — that case is covered by the danger row above.
    // A dedicated diagnostics-marker test lives once we identify VHDL that
    // reliably triggers a ParserException via PublishDiagnosticsAsync.
  });

  test('fixing the invalid VHDL recovers a clean build', async ({ page }) => {
    const { runbar, monaco } = await dropAndSave(page, HALF_ADDER_BROKEN);

    await runbar.pickFirstFpga();
    await runbar.build();
    await runbar.waitForBuildIdle();

    const out = new OutputPanel(page);
    await out.waitForError();

    // Replace with valid source, save, rebuild — success row appears.
    await monaco.setValue(HALF_ADDER_OK);
    await runbar.save();
    await runbar.build();
    await runbar.waitForBuildIdle();
    await out.waitForSuccess();
  });
});
