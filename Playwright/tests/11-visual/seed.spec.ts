// One-shot fixture seeder for Phase 0 Visual Runtime acceptance walk.
//
// Idempotent: if the project name already exists in /projects, the seed
// step skips re-creation but rewrites the VHDL content. This way the
// fixtures stay aligned with whatever the FIXTURES catalogue currently
// declares without forcing the user to clean up manually between runs.
//
// Run with:
//   npx playwright test tests/11-visual/seed.spec.ts --project=chromium-desktop

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { Monaco } from '../../pageobjects/Monaco';
import { RunBar } from '../../pageobjects/RunBar';
import { FIXTURES } from './fixtures';

test.describe('Visual Phase 0 — fixture seeder', () => {
  test.setTimeout(180_000);

  for (const fixture of FIXTURES) {
    test(`seed ${fixture.projectName}`, async ({ page }) => {
      const projects = new ProjectsPage(page);
      await projects.goto();

      // Idempotent: drop+recreate when the card already exists so the
      // file count + content stay aligned with the FIXTURES catalogue.
      // Cheaper than navigating in and editing in-place.
      const existing = projects.cardByName(fixture.projectName);
      if (await existing.isVisible().catch(() => false)) {
        await projects.removeProject(fixture.projectName, 'yes');
      }

      await projects.createProject(fixture.projectName, {
        templateId: 'empty',
        description: 'Visual Phase 0 fixture',
      });
      await projects.openProject(fixture.projectName);

      const editor = new EditorPage(page);
      const monaco = new Monaco(page);
      const runbar = new RunBar(page);

      await editor.waitForReady();
      await editor.openDockTab('Files');
      // Templates v1 ships main.vhd + docs/README.md; clear so the fixture
      // file is the only entity in the compile unit.
      await editor.files.clearScaffold();

      const fullName = await editor.files.createFile(fixture.fileSlug, 'vhdl');
      await editor.files.fileRowByName(fullName).locator('.file-row__name').click();
      await monaco.setValue(fixture.source);
      await runbar.save();
      await expect(page.locator('.editor-unsaved')).toBeHidden({ timeout: 10_000 });
      // Make the fixture file the entry point so /visual/{id} resolves the
      // entity even though the empty template still seeds main.vhd.
      await editor.files.toggleEntryPoint(fullName);
    });
  }
});
