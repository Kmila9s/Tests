import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';

/**
 * Editor — file & folder CRUD from the FilesCard.
 *
 * Each test creates its own project, exercises one piece of the file
 * tree, and best-effort cleans up. Project removal goes through the
 * /projects page's ConfirmDialog (covered by 02-projects).
 */
test.describe('Editor — file & folder operations', () => {
  let projectName: string;

  test.beforeEach(async ({ page }) => {
    projectName = `Files-${Date.now()}`;
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.createProject(projectName, 'File-ops scratch project.');
    await projects.openProject(projectName);
    const editor = new EditorPage(page);
    await editor.waitForReady();
    // FilesCard lives in the Files dock panel; default is Entities. Switch.
    await editor.openDockTab('Files');
    await expect(editor.files.root).toBeVisible();
    // Templates v1 (v1.23+) seeds new projects with main.vhd + docs/README.md.
    // These file-ops tests pre-date the templates work and assume an empty
    // starting tree (e.g. the rename test would collide with the scaffold's
    // own README.md). Strip the scaffold so each test sees the clean slate
    // it was written for.
    await editor.files.clearScaffold();
  });

  test.afterEach(async ({ page }) => {
    // Best-effort cleanup so the projects list doesn't accumulate.
    try {
      const projects = new ProjectsPage(page);
      await projects.goto();
      await projects.removeProject(projectName, 'yes');
    } catch {
      /* ignore */
    }
  });

  test('create a new VHDL file via toolbar', async ({ page }) => {
    const editor = new EditorPage(page);
    const created = await editor.files.createFile('demo', 'vhdl');
    expect(created).toBe('demo.vhdl');
    await expect(editor.files.fileRowByName('demo.vhdl')).toBeVisible();
  });

  test('create a new folder via toolbar', async ({ page }) => {
    const editor = new EditorPage(page);
    await editor.files.createFolder('subdir');
    await expect(editor.files.folderRowByName('subdir')).toBeVisible();
  });

  test('rename a file (also covers extension change)', async ({ page }) => {
    const editor = new EditorPage(page);
    await editor.files.createFile('notes', 'txt');
    await editor.files.renameFile('notes.txt', 'README.md');
    await expect(editor.files.fileRowByName('README.md')).toBeVisible();
    await expect(editor.files.fileRowByName('notes.txt')).toBeHidden();
  });

  test('toggle entry-point on a VHDL file', async ({ page }) => {
    const editor = new EditorPage(page);
    await editor.files.createFile('top', 'vhdl');
    await editor.files.toggleEntryPoint('top.vhdl');

    const row = editor.files.fileRowByName('top.vhdl');
    await expect(row).toHaveClass(/is-entry/);
    await expect(row.locator('.file-row__entry-marker')).toBeVisible();

    // Toggling again clears the entry-point.
    await editor.files.toggleEntryPoint('top.vhdl');
    await expect(row).not.toHaveClass(/is-entry/);
  });

  test('delete a file — cancel keeps it, confirm removes it', async ({ page }) => {
    const editor = new EditorPage(page);
    await editor.files.createFile('scratch', 'txt');

    await editor.files.deleteFile('scratch.txt', 'no');
    await expect(editor.files.fileRowByName('scratch.txt')).toBeVisible();

    await editor.files.deleteFile('scratch.txt', 'yes');
    await expect(editor.files.fileRowByName('scratch.txt')).toBeHidden();
  });

  test('delete a folder', async ({ page }) => {
    const editor = new EditorPage(page);
    await editor.files.createFolder('to-remove');
    await editor.files.deleteFolder('to-remove', 'yes');
    await expect(editor.files.folderRowByName('to-remove')).toBeHidden();
  });
});
