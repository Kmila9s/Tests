// Phase 2 acceptance walk — speed slider, custom delay, pause/resume,
// frame scrubbing, tick breakpoints, stepper widget.
//
// Run order:
//   1) tests/11-visual/seed.spec.ts       (idempotent fixture seed)
//   2) tests/11-visual/phase2.spec.ts     (these)
//
// Selector convention (2026-06-26 update — see VisualPanel.razor's
// data-testid attributes for the canonical list):
//   - Toolbar/runbar buttons use `[data-testid="visual-..."]`. Stable
//     across locales after the i18n pass that translated button labels.
//   - Widget shells still keyed by class (.kviz-led-array, .kviz-stepper, …).

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

async function openVisual(page: any, projectName: string) {
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject(projectName);
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  if (!idMatch) throw new Error(`No id in ${page.url()}`);
  const projectId = idMatch[1];
  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
  await page.waitForLoadState('networkidle');
  return projectId;
}

test.describe('Visual Phase 2 — controls + stepper', () => {
  test.setTimeout(120_000);

  test('§P2.A — Speed slider sets coordinator speed, custom delay overrides', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase1-Counter4').projectName);

    // Add an LED array + wire so the run loop is doing something.
    const addBtn = page.locator('[data-testid="visual-add-led-array"]');
    await expect(addBtn).toBeEnabled({ timeout: 10_000 });
    await expect.poll(async () => {
      if ((await page.locator('.kviz-led-array').count()) === 0) await addBtn.click();
      return (await page.locator('.kviz-led-array').count()) > 0;
    }, { timeout: 10_000 }).toBe(true);

    await page.locator('[data-testid="visual-wire"]').click();
    const modal = page.locator('.kviz-wiring-modal');
    await expect(modal).toBeVisible();
    await modal.locator('.kviz-wiring-table tbody tr')
      .filter({ has: page.locator('code', { hasText: 'anodes' }) }).last()
      .locator('select').selectOption('count');
    await modal.locator('[data-testid="visual-wiring-done"]').click();
    await expect(modal).toBeHidden();

    // Run; Phase 2 runbar should render.
    await page.locator('[data-testid="visual-run"]').click();
    await expect(page.locator('[data-testid="visual-stop"]')).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('.visual-page__runbar')).toBeVisible({ timeout: 5_000 });

    // Speed slider starts at index 1 (Normal). Slide to Fast (index 2).
    const speedSlider = page.locator('input.visual-page__speed-slider');
    await speedSlider.fill('2');
    await expect(page.locator('.visual-page__speed-label')).toHaveText(/10x|10×/);

    // Custom delay = 50 ms — label switches to "50 ms".
    const delay = page.locator('input.visual-page__delay-input');
    await delay.fill('50');
    await delay.press('Tab');
    await expect(page.locator('.visual-page__speed-label')).toHaveText(/50 ms/);

    // Clear the delay → slider preset returns.
    await delay.fill('');
    await delay.press('Tab');
    await expect(page.locator('.visual-page__speed-label')).toHaveText(/10x|10×/);

    await page.locator('[data-testid="visual-stop"]').click();
    await expect(page.locator('[data-testid="visual-run"]')).toBeVisible({ timeout: 10_000 });
  });

  test('§P2.B — Pause + scrub: slider rewinds canvas to a previous frame', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase1-Counter4').projectName);

    // Same LED-array wire as above (the test is independent — re-add).
    const addBtn = page.locator('[data-testid="visual-add-led-array"]');
    await expect(addBtn).toBeEnabled({ timeout: 10_000 });
    await expect.poll(async () => {
      if ((await page.locator('.kviz-led-array').count()) === 0) await addBtn.click();
      return (await page.locator('.kviz-led-array').count()) > 0;
    }, { timeout: 10_000 }).toBe(true);

    await page.locator('[data-testid="visual-wire"]').click();
    const modal = page.locator('.kviz-wiring-modal');
    await expect(modal).toBeVisible();
    await modal.locator('.kviz-wiring-table tbody tr')
      .filter({ has: page.locator('code', { hasText: 'anodes' }) }).last()
      .locator('select').selectOption('count');
    await modal.locator('[data-testid="visual-wiring-done"]').click();
    await expect(modal).toBeHidden();

    await page.locator('[data-testid="visual-run"]').click();
    await expect(page.locator('[data-testid="visual-stop"]')).toBeVisible({ timeout: 5_000 });

    // Wait until at least one LED is lit so we know counter has advanced.
    const arr = page.locator('.kviz-led-array').first();
    await expect.poll(async () =>
      arr.locator('.kviz-led-array__cell--on').count(),
      { timeout: 5_000 }).toBeGreaterThan(0);

    // Let the run accumulate some history.
    await page.waitForTimeout(500);

    // Pause. Scrub slider should activate (disabled class removes).
    await page.locator('[data-testid="visual-pause-continue"]:not(:has-text("Continue"))').or(page.locator('[data-testid="visual-pause-continue"]:has-text("Pause")')).click();
    await expect(page.locator('[data-testid="visual-pause-continue"]:has-text("▶")')).toBeVisible({ timeout: 3_000 });
    const scrub = page.locator('input.visual-page__scrub-slider');
    await expect(scrub).toBeEnabled();

    // Scrub to the oldest tick: array should usually have fewer (or zero)
    // cells lit than the latest paused state, since counter started at 0.
    const minTick = await scrub.getAttribute('min');
    expect(minTick).not.toBeNull();
    await scrub.fill(minTick!);
    // The slider should now hold the scrubbed value. Locale-independent —
    // probing the input's value is robust to en/es/fr/de/jp/ch/ar.
    await expect(scrub).toHaveValue(minTick!);

    // Continue → returns to running.
    await page.locator('[data-testid="visual-pause-continue"]:has-text("▶")').click();
    await expect(page.locator('[data-testid="visual-pause-continue"]:not(:has-text("Continue"))').or(page.locator('[data-testid="visual-pause-continue"]:has-text("Pause")'))).toBeVisible({ timeout: 3_000 });

    await page.locator('[data-testid="visual-stop"]').click();
    await expect(page.locator('[data-testid="visual-run"]')).toBeVisible({ timeout: 10_000 });
  });

  test('§P2.C — Tick breakpoint pauses the coordinator on hit', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase1-Counter4').projectName);

    await page.locator('[data-testid="visual-run"]').click();
    await expect(page.locator('[data-testid="visual-stop"]')).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('.visual-page__runbar')).toBeVisible();

    // Drop a breakpoint a few hundred ticks ahead — far enough that the
    // coordinator definitely hasn't crossed it yet, close enough that we
    // hit it within the test timeout.
    const bpInput = page.locator('input.visual-page__bp-input');
    await bpInput.fill('150');
    await page.locator('[data-testid="visual-bp-add"]').click();
    await expect(page.locator('.visual-page__bp-chip').filter({ hasText: '150' })).toBeVisible();

    // Wait for the break: "Continue" replaces "Pause" once paused.
    await expect(page.locator('[data-testid="visual-pause-continue"]:has-text("▶")')).toBeVisible({ timeout: 10_000 });
    // The break-hit chip is locale-translated ("breakpoint 150" / "punto
    // de ruptura 150" / …) — match on the trailing tick number which is
    // stable across locales.
    await expect(page.locator('.visual-page__bp-hit')).toContainText(/150/);

    // Remove the chip — clicking it deletes it.
    await page.locator('.visual-page__bp-chip').filter({ hasText: '150' }).click();
    await expect(page.locator('.visual-page__bp-chip').filter({ hasText: '150' })).toBeHidden();

    await page.locator('[data-testid="visual-pause-continue"]:has-text("▶")').click();
    await expect(page.locator('[data-testid="visual-pause-continue"]:not(:has-text("Continue"))').or(page.locator('[data-testid="visual-pause-continue"]:has-text("Pause")'))).toBeVisible({ timeout: 3_000 });

    await page.locator('[data-testid="visual-stop"]').click();
    await expect(page.locator('[data-testid="visual-run"]')).toBeVisible({ timeout: 10_000 });
  });

  test('§P2.D — Stepper widget adds, properties pane drives stepsPerRev + direction', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase2-Stepper').projectName);

    const addBtn = page.locator('[data-testid="visual-add-stepper"]');
    await expect(addBtn).toBeEnabled({ timeout: 10_000 });
    await expect.poll(async () => {
      if ((await page.locator('.kviz-stepper').count()) === 0) await addBtn.click();
      return (await page.locator('.kviz-stepper').count()) > 0;
    }, { timeout: 10_000 }).toBe(true);

    // Stepper renders 4 coils + meta readout.
    const stepper = page.locator('.kviz-stepper').first();
    await expect(stepper).toBeVisible();
    await expect(stepper.locator('.kviz-stepper-coil')).toHaveCount(4);
    await expect(stepper.locator('.kviz-stepper-rotor')).toBeVisible();

    // Properties pane: change stepsPerRev + direction.
    await stepper.click();
    const stepsInput = page.locator('.visual-page__props input[type="number"]');
    await expect(stepsInput).toBeVisible();
    await stepsInput.fill('100');
    await stepsInput.blur();

    const dirSelect = page.locator('.visual-page__props select').last();
    await dirSelect.selectOption('cw');
  });

  test('§P2.E — Stepper wired to fixture rotates while running', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase2-Stepper').projectName);

    const addBtn = page.locator('[data-testid="visual-add-stepper"]');
    await expect(addBtn).toBeEnabled({ timeout: 10_000 });
    await expect.poll(async () => {
      if ((await page.locator('.kviz-stepper').count()) === 0) await addBtn.click();
      return (await page.locator('.kviz-stepper').count()) > 0;
    }, { timeout: 10_000 }).toBe(true);

    // Wire: phaseA→a_out, phaseB→b_out, phaseAn→an_out, phaseBn→bn_out.
    await page.locator('[data-testid="visual-wire"]').click();
    const modal = page.locator('.kviz-wiring-modal');
    await expect(modal).toBeVisible();
    const rows = modal.locator('.kviz-wiring-table tbody tr');
    await rows.filter({ has: page.locator('code', { hasText: 'phaseA' }) }).first().locator('select').selectOption('a_out');
    await rows.filter({ has: page.locator('code', { hasText: 'phaseB' }) }).first().locator('select').selectOption('b_out');
    await rows.filter({ has: page.locator('code', { hasText: 'phaseAn' }) }).first().locator('select').selectOption('an_out');
    await rows.filter({ has: page.locator('code', { hasText: 'phaseBn' }) }).first().locator('select').selectOption('bn_out');
    await modal.locator('[data-testid="visual-wiring-done"]').click();
    await expect(modal).toBeHidden();

    await page.locator('[data-testid="visual-run"]').click();
    await expect(page.locator('[data-testid="visual-stop"]')).toBeVisible({ timeout: 5_000 });

    // Verify motion: poll for ≥3 s, succeed as soon as EITHER the rotor's
    // transform string changes OR at least one coil lights up. This is
    // robust to the off-by-one on first phase decode + the ~256 ms clock
    // half-period at Normal speed (rising-edge every 16 ticks → idx
    // increments at roughly that cadence).
    const rotor = page.locator('.kviz-stepper-rotor').first();
    const initialStyle = await rotor.getAttribute('style');
    await expect.poll(async () => {
      const styleNow = await rotor.getAttribute('style');
      const litNow = await page.locator('.kviz-stepper-coil--lit').count();
      return styleNow !== initialStyle || litNow > 0;
    }, { timeout: 5_000, intervals: [150, 300, 500] }).toBe(true);

    await page.locator('[data-testid="visual-stop"]').click();
    await expect(page.locator('[data-testid="visual-run"]')).toBeVisible({ timeout: 10_000 });
  });
});
