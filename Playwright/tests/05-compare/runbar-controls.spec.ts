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

/**
 * Phase 05 — vary clock / FPGA / simulation time and assert the UI reflects
 * each change. Heavier port-moment editing lives in a follow-up spec once
 * PortMomentInput's selectors are mapped.
 */
test.describe('Editor — RunBar parameter changes', () => {
  test.setTimeout(180_000);
  let projectName: string;

  test.beforeEach(async ({ page }) => {
    projectName = `Compare-${Date.now()}`;
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.createProject(projectName);
    await projects.openProject(projectName);
    const editor = new EditorPage(page);
    await editor.waitForReady();
    await editor.openDockTab('Files');
    await editor.files.createFile('half_adder', 'vhdl');
    await editor.files.fileRowByName('half_adder.vhdl').locator('.file-row__name').click();
    const monaco = new Monaco(page);
    await monaco.setValue(HALF_ADDER_OK);
    const runbar = new RunBar(page);
    await runbar.save();
    await editor.files.toggleEntryPoint('half_adder.vhdl');
    await runbar.pickFirstFpga();
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

  test('FPGA chip reflects the picked board and the build succeeds', async ({ page }) => {
    const runbar = new RunBar(page);
    const model = await runbar.fpgaChip.locator('.run-bar__fpga-model').innerText();
    expect(model.trim().length).toBeGreaterThan(0);
    expect(model).not.toMatch(/select/i);

    await runbar.build();
    await runbar.waitForBuildIdle();

    const editor = new EditorPage(page);
    await editor.openDockTab('Output');
    const out = new OutputPanel(page);
    await out.waitForSuccess();
  });

  test('clock frequency is editable and persists', async ({ page }) => {
    const runbar = new RunBar(page);
    await runbar.clockInput.fill('100');
    await runbar.clockInput.press('Tab');
    // Custom clock pill class lights up — see RunBar.razor:78.
    await expect(page.locator('.run-bar__clock--custom')).toBeVisible();
    await expect(runbar.clockInput).toHaveValue('100');

    // The reset-clock button appears when ClockIsCustom is set.
    await expect(page.locator('button.run-bar__clock-reset')).toBeVisible();
  });

  test('simulation time/scale are editable and persisted across a rebuild', async ({ page }) => {
    const runbar = new RunBar(page);
    await runbar.timeInput.fill('500');
    await runbar.timeInput.press('Tab');
    await expect(runbar.timeInput).toHaveValue('500');

    const scale = page.locator('select.run-bar__scale');
    await scale.selectOption('NS');
    await expect(scale).toHaveValue('NS');

    // Build then verify values stick.
    await runbar.build();
    await runbar.waitForBuildIdle();
    await expect(runbar.timeInput).toHaveValue('500');
    await expect(scale).toHaveValue('NS');
  });

  test('two runs with different clocks both succeed', async ({ page }) => {
    const runbar = new RunBar(page);
    const editor = new EditorPage(page);
    const out = new OutputPanel(page);

    // Build #1 at default clock.
    await runbar.build();
    await runbar.waitForBuildIdle();
    await editor.openDockTab('Output');
    await out.waitForSuccess();
    const firstSuccessCount = await out.successRows.count();
    expect(firstSuccessCount).toBeGreaterThan(0);

    // Bump clock, rebuild.
    await runbar.clockInput.fill('200');
    await runbar.clockInput.press('Tab');
    await runbar.build();
    await runbar.waitForBuildIdle();
    await editor.openDockTab('Output');
    await out.waitForSuccess();
    const secondSuccessCount = await out.successRows.count();
    expect(secondSuccessCount).toBeGreaterThan(firstSuccessCount);
  });
});
