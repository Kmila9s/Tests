import { expect, Locator, Page } from '@playwright/test';
import { ConfirmDialog } from './ConfirmDialog';

/**
 * FilesCard — the project file tree inside the Editor.
 * Source: Kmila.Shared/Components/FilesCard.razor + Modals/CreateFileModal.razor.
 *
 * Kebab menus carry a stable `data-kmila-menu-key` attribute:
 *   - folders: `folder:{path}`
 *   - files:   `file:{id}`
 * Menu items render in a floating panel at `.file-row__menu-panel` with
 * items styled `.file-row__menu-item` (icon + label).
 */
export class FilesCard {
  readonly root: Locator;
  readonly toolbarNewFileBtn: Locator;
  readonly toolbarNewFolderBtn: Locator;
  readonly fileRows: Locator;
  readonly folderRows: Locator;
  readonly menuPanel: Locator;

  // CreateFileModal
  readonly createNameInput: Locator;
  readonly createExtSelect: Locator;
  readonly createSubmit: Locator;

  constructor(readonly page: Page) {
    this.root = page.locator('.files-list');
    this.toolbarNewFileBtn = this.root.locator('.files-list__toolbar button').first();
    this.toolbarNewFolderBtn = this.root.locator('.files-list__toolbar button').last();
    this.fileRows = this.root.locator('li.file-row').filter({ hasNot: page.locator('.file-row--folder') });
    this.folderRows = this.root.locator('li.file-row.file-row--folder');
    this.menuPanel = page.locator('.file-row__menu-panel');

    this.createNameInput = page.locator('#InputProjectFileName');
    this.createExtSelect = page.locator('#extentionType');
    // BlazorBootstrap <Button Outline=true Color=Success Type=Submit> renders as
    // button[type=submit].btn-outline-success.
    this.createSubmit = page
      .locator('.modal.show button[type="submit"]')
      .last();
  }

  fileRowByName(name: string): Locator {
    return this.root
      .locator('li.file-row')
      .filter({ has: this.page.locator('.file-row__name', { hasText: name }) })
      .filter({ hasNot: this.page.locator('.file-row--folder') });
  }

  folderRowByName(name: string): Locator {
    return this.root
      .locator('li.file-row.file-row--folder')
      .filter({ has: this.page.locator('.file-row__name', { hasText: name }) });
  }

  async createFile(baseName: string, ext: 'vhdl' | 'txt' | 'md' = 'txt') {
    await this.toolbarNewFileBtn.click();
    await expect(this.createNameInput).toBeVisible();
    await this.createNameInput.fill(baseName);
    await this.createExtSelect.selectOption(ext);
    await this.createSubmit.click();
    const fullName = `${baseName}.${ext}`;
    await expect(this.fileRowByName(fullName)).toBeVisible({ timeout: 10_000 });
    // Wait for the modal to actually fade out — without this the modal-
    // backdrop intercepts subsequent clicks (e.g. opening the file).
    await expect(this.createNameInput).toBeHidden({ timeout: 10_000 });
    await expect(this.page.locator('.modal.show')).toHaveCount(0, { timeout: 10_000 });
    return fullName;
  }

  async createFolder(name: string) {
    await this.toolbarNewFolderBtn.click();
    const editingInput = this.root.locator('.file-row.is-editing input.file-row__rename');
    await expect(editingInput).toBeVisible();
    await editingInput.fill(name);
    await editingInput.press('Enter');
    await expect(this.folderRowByName(name)).toBeVisible({ timeout: 10_000 });
  }

  async openFileKebab(fileName: string) {
    const row = this.fileRowByName(fileName);
    await row.hover();
    await row.locator('button.file-row__menu-btn').click();
    await expect(this.menuPanel).toBeVisible();
  }

  async openFolderKebab(folderName: string) {
    const row = this.folderRowByName(folderName);
    await row.scrollIntoViewIfNeeded();
    await row.hover();
    await row.locator('button.file-row__menu-btn').click();
    await expect(this.menuPanel).toBeVisible();
  }

  /** Click a menu item by its icon class — locale-independent. */
  private menuItemByIcon(iconClass: string): Locator {
    return this.menuPanel.locator('.file-row__menu-item').filter({
      has: this.page.locator(`i.bi.${iconClass}, .bi.${iconClass}`),
    });
  }

  async renameFile(oldName: string, newFullName: string) {
    await this.openFileKebab(oldName);
    // The Rename menu item is the second one for a file (Set entry · Rename · Details · Move to · Delete).
    // dispatchEvent bypasses Playwright's viewport check — the menu panel is
    // position:fixed and may anchor outside the viewport when the row is near
    // the dock bottom.
    await this.menuPanel.locator('.file-row__menu-item').nth(1).dispatchEvent('click');
    // Once editing, the row swaps the name <button> for an <input>; target any
    // visible rename input in the card (only one row edits at a time).
    const input = this.root.locator('li.file-row input.file-row__rename');
    await expect(input).toBeVisible();
    await input.fill(newFullName);
    await input.press('Enter');
    await expect(this.fileRowByName(newFullName)).toBeVisible({ timeout: 10_000 });
  }

  async toggleEntryPoint(fileName: string) {
    await this.openFileKebab(fileName);
    // Star icon — first menu item for files. dispatchEvent for viewport safety.
    await this.menuPanel.locator('.file-row__menu-item').first().dispatchEvent('click');
    await expect(this.menuPanel).toBeHidden();
  }

  async deleteFile(fileName: string, confirm: 'yes' | 'no' = 'yes') {
    await this.openFileKebab(fileName);
    await this.menuPanel.locator('.file-row__menu-item--danger').dispatchEvent('click');

    const dialog = new ConfirmDialog(this.page);
    await expect(dialog.root).toBeVisible();
    if (confirm === 'yes') {
      await dialog.confirmDanger.click();
      await expect(this.fileRowByName(fileName)).toBeHidden({ timeout: 10_000 });
    } else {
      await dialog.cancelSecondary.click();
      await expect(dialog.root).toBeHidden();
    }
  }

  /**
   * Removes every file + folder that the v1 templates v1 scaffold ships
   * for the `empty` template (main.vhd at root + docs/README.md inside docs/).
   * Use in legacy tests that pre-date the templates work and assume the
   * project starts with zero files.
   *
   * After each UI-driven delete the BB modal backdrop fades out; wait for
   * `.modal-backdrop` to leave the DOM before returning so the next
   * createFile / createFolder modal doesn't stack on a stale backdrop and
   * fail to dismiss.
   */
  async clearScaffold() {
    const waitNoBackdrop = async () => {
      await expect(this.page.locator('.modal-backdrop')).toHaveCount(0, { timeout: 10_000 });
      await expect(this.page.locator('.modal.show')).toHaveCount(0, { timeout: 10_000 });
    };

    if (await this.folderRowByName('docs').isVisible().catch(() => false)) {
      await this.deleteFolder('docs', 'yes');
      await waitNoBackdrop();
    }
    if (await this.fileRowByName('main.vhd').isVisible().catch(() => false)) {
      await this.deleteFile('main.vhd', 'yes');
      await waitNoBackdrop();
    }
  }

  async deleteFolder(folderName: string, confirm: 'yes' | 'no' = 'yes') {
    await this.openFolderKebab(folderName);
    // The menu panel is position:fixed and anchored to the kebab via JS,
    // so for a folder near the dock bottom it can spill below the viewport.
    // dispatchEvent bypasses Playwright's viewport actionability check.
    await this.menuPanel
      .locator('.file-row__menu-item--danger')
      .dispatchEvent('click');

    const dialog = new ConfirmDialog(this.page);
    await expect(dialog.root).toBeVisible();
    if (confirm === 'yes') {
      await dialog.confirmDanger.click();
      await expect(this.folderRowByName(folderName)).toBeHidden({ timeout: 10_000 });
    } else {
      await dialog.cancelSecondary.click();
    }
  }
}
