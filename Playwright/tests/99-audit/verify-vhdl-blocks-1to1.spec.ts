// 2026-07-19 Phase 16 — Layer 3 end-to-end validator for the block-editor
// 1:1 round-trip. Exercises the full Blazor/JS wiring:
//   1. Create a fresh project.
//   2. Load a known-clean VHDL fixture into Monaco.
//   3. Save (Ctrl+S).
//   4. Switch to the Blocks tab — triggers `SyncCodeToBlocksAsync`.
//   5. Assert no `EDITOR_BLOCKS_RAW_*` toast appears (fixtures are all
//      known-clean per RoundTripFidelityTests).
//   6. Assert no `EDITOR_BLOCKS_SILENT_LOSS_*` toast (Phase 0).
//   7. Switch back to Code — triggers `SyncBlocksToCodeAsync`.
//   8. Read Monaco's value and assert byte-idempotence: after one
//      through-the-UI round-trip, the code stays the same (this is the
//      strict criterion documented in `BLOCK_EDITOR_ROUNDTRIP_2026-07-06.md`).
//
// Fixtures under Fixtures/RoundTripFidelity/*.vhd that are known clean
// (0 raws in unit tests post-Phase-9) are exercised here. Kitchen sink is
// intentionally excluded because it stresses every dark corner and its
// clean state is asserted at the unit-test layer; UI E2E adds nothing.
//
// Skipped in CI when Kmila.Web can't boot (Playwright webServer times out
// after 180s per playwright.config.ts).

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { Monaco } from '../../pageobjects/Monaco';

const FIXTURE_DIR = path.resolve(
  __dirname,
  '../../../../Tests/Unit/Kmila.Shared.Tests/Fixtures/RoundTripFidelity',
);

// Known-clean fixtures post-Phase-9 (RT_KitchenSink at 0 too, but omitted).
// 2026-07-19 Phase 19 — added p19_subprograms.vhd, a synthetic testbench
// that exercises component_decl + procedure_decl + function_decl +
// procedure_call in one file. Round-trips to 0 raws per
// RT_P19_ProcedureCall_InProcessBody + the corresponding builder scan.
const CLEAN_FIXTURES = [
  'sample_main.vhd',
  'counter_clock.vhd',
  'traffic_light.vhd',
  'shift_register.vhd',
  'dip_adder.vhd',
  'matrix_plus.vhd',
  'rgb_rainbow.vhd',
  'keypad_display.vhd',
  'lcd_hello.vhd',
  'p19_subprograms.vhd',
];

// Toast keys the Editor fires when the recognizer falls back to raw blocks
// or when the emitter surfaces silent-loss diagnostics. Locale-independent
// substring match — kmila-toast__title carries the translated title.
const RAW_TOAST_KEYS_ES = [
  'Fragmentos VHDL preservados',
  'Bloques incompletos detectados',
];

test.describe.configure({ mode: 'serial' });

test.describe('Phase 16 — VHDL↔Blocks 1:1 end-to-end (post-Phase-9)', () => {
  let projectName: string;

  test.beforeEach(async ({ page }) => {
    projectName = `RT-1to1-${Date.now()}`;
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.createProject(projectName);
    await projects.openProject(projectName);
    const editor = new EditorPage(page);
    await editor.waitForReady();
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

  for (const fixture of CLEAN_FIXTURES) {
    test(`${fixture} — no raw/silent-loss toast + byte-idempotent`, async ({ page }) => {
      const fixturePath = path.join(FIXTURE_DIR, fixture);
      // Skip individual test if fixture is missing on disk (dev machine
      // may not have unit-test fixtures if the /Tests directory wasn't
      // synced from git).
      test.skip(!fs.existsSync(fixturePath), `fixture not on disk: ${fixture}`);
      const v0 = fs.readFileSync(fixturePath, 'utf8');

      // 1. Push V0 into Monaco.
      const monaco = new Monaco(page);
      await monaco.setValue(v0);

      // 2. Save so the .bvhdl sidecar path is exercised on the next
      //    Blocks-tab switch. Without a save, the file stays dirty and
      //    the block-editor sync depends on tab-switch semantics.
      await monaco.saveWithShortcut();
      await page.waitForTimeout(500);

      // 3. Switch to the Blocks tab. Locate by the workbench view-tab
      //    strip's second child (positional selector — resilient to
      //    locale changes; the workbench strip is Code / Blocks / Replay /
      //    Visual in that order).
      const blocksTab = page.locator(
        '.editor-workbench__view-tabs .kmila-btn-tab',
      ).nth(1);
      await blocksTab.click();

      // 4. Wait for the block engine to boot and SyncCodeToBlocksAsync to
      //    finish. 2s covers block-engine init + serialize / load cycle.
      await page.waitForTimeout(2000);

      // 5. Assert no raw-fragment or silent-loss toast surfaced. Query the
      //    toast host DOM and check every visible title.
      const toastTitles = await page
        .locator('.kmila-toast__title')
        .allTextContents();
      for (const key of RAW_TOAST_KEYS_ES) {
        expect(
          toastTitles.some((t) => t.includes(key)),
          `unexpected toast "${key}" — fixture ${fixture} is supposed ` +
            `to round-trip clean (0 raws in RoundTripFidelityTests). ` +
            `Observed toasts: ${JSON.stringify(toastTitles)}`,
        ).toBe(false);
      }

      // 6. Switch back to Code — triggers SyncBlocksToCodeAsync.
      const codeTab = page.locator(
        '.editor-workbench__view-tabs .kmila-btn-tab',
      ).nth(0);
      await codeTab.click();
      await page.waitForTimeout(1500);

      // 7. Read Monaco value and assert byte-idempotence. V0 → blocks →
      //    V1: the round-trip may canonicalize whitespace, add the
      //    `library ieee;` preamble, etc. The strict criterion is V1 →
      //    blocks → V2 == V1, but doing two full trips through the UI is
      //    slow. Weaker check: V1 must be non-empty and contain the
      //    entity/architecture identifiers from V0 (semantic survival).
      const v1 = await monaco.getValue();
      expect(v1.length, 'V1 must not be empty').toBeGreaterThan(0);

      // Extract entity name from V0 and assert it survives in V1.
      const entityMatch = v0.match(/\bentity\s+(\w+)\s+is\b/i);
      if (entityMatch) {
        expect(v1, `entity ${entityMatch[1]} must survive round-trip`)
          .toContain(entityMatch[1]);
      }

      // 8. Second round-trip: assert byte-idempotence V1 → blocks → V2 == V1.
      //    This is the strict fidelity criterion from
      //    BLOCK_EDITOR_ROUNDTRIP_2026-07-06.md.
      await blocksTab.click();
      await page.waitForTimeout(1500);
      await codeTab.click();
      await page.waitForTimeout(1500);
      const v2 = await monaco.getValue();
      expect(v2, 'byte-idempotence: V1 → blocks → V2 must equal V1')
        .toBe(v1);
    });
  }
});
