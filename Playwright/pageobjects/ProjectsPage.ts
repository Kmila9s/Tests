import { expect, Locator, Page } from '@playwright/test';
import { ConfirmDialog } from './ConfirmDialog';

/**
 * ProjectsPage — /projects.
 *
 * Selectors target stable CSS classes (locale-independent) from
 * Kmila.Shared/Pages/ProjectsView.razor and ProjectDetailsModal.razor.
 */
export class ProjectsPage {
  readonly headerTitle: Locator;
  readonly searchInput: Locator;
  readonly searchClear: Locator;
  readonly createCta: Locator;
  readonly sampleCta: Locator;
  readonly grid: Locator;
  readonly emptyState: Locator;
  readonly noMatch: Locator;

  // Create modal — v1 template gallery
  readonly modalNameInput: Locator;
  readonly modalSubmit: Locator;
  readonly modalAutoImport: Locator;

  constructor(readonly page: Page) {
    this.headerTitle = page.locator('.projects-header__title');
    this.searchInput = page.locator('.projects-search input[type="search"]');
    this.searchClear = page.locator('.projects-search__clear');
    this.createCta = page
      .locator('button.projects-cta, .projects-empty button.kmila-btn--accent')
      .first();
    this.sampleCta = page.locator('button.projects-sample').first();
    this.grid = page.locator('.projects-grid');
    this.emptyState = page.locator('.projects-empty:not(.projects-empty--compact)');
    this.noMatch = page.locator('.projects-empty--compact');

    // Template gallery modal selectors (replaces ProjectDetailsModal as the
    // primary create flow from v1.23.0 onward).
    this.modalNameInput = page.locator('#TemplateProjectName');
    this.modalSubmit = page.locator('.kmila-template-form__actions button');
    this.modalAutoImport = page.locator('.kmila-template-form__check input[type="checkbox"]');
  }

  templateCard(id: string): Locator {
    return this.page.locator(`button.kmila-template-card[data-template-id="${id}"]`);
  }

  async goto() {
    // Pre-mark the welcome tour (kmila-tutorial.js) as seen so its dialog
    // doesn't open on first /projects visit. The tour ships with v1.21+
    // and otherwise intercepts pointer events on the project grid (card
    // delete button, in particular). addInitScript runs before every
    // navigation in this browser context, so a single goto() call is
    // enough.
    await this.page.addInitScript(() => {
      try {
        localStorage.setItem('kmila.tutorialSeen', '1');
        localStorage.setItem('kmila.tutorialSeen.projects', '1');
        localStorage.setItem('kmila.tutorialSeen.editor', '1');
      } catch {
        /* sandboxed contexts: best-effort */
      }
    });
    await this.page.goto('/projects');
    await expect(this.headerTitle).toBeVisible();
  }

  cardByName(name: string): Locator {
    return this.page
      .locator('article.project-card')
      .filter({ has: this.page.locator('.project-card__name', { hasText: name }) });
  }

  /**
   * Open the create-project modal and submit it with the given name. From
   * v1.23.0 the modal is the template gallery — by default we pick `empty`
   * (single multi-file scaffold, no library auto-import) to mirror the
   * old single-file-create UX. Pass `templateId` to choose a different one
   * and `registerAsLibrary` for the library template's checkbox.
   *
   * After submit we land on /editor/{id} (the gallery navigates straight in),
   * so callers that want to verify the project card existence should
   * navigate back to /projects themselves.
   */
  async createProject(
    name: string,
    optsOrDescription: string | { templateId?: string; registerAsLibrary?: boolean; description?: string } = {},
  ) {
    // Accept either the legacy `(name, description)` form or the v1.23
    // `(name, { templateId, registerAsLibrary, … })` form.
    const opts: { templateId?: string; registerAsLibrary?: boolean; description?: string } =
      typeof optsOrDescription === 'string' ? { description: optsOrDescription } : optsOrDescription;
    const templateId = opts.templateId ?? 'empty';
    await this.page.goto('/projects/create');
    await expect(this.modalNameInput).toBeVisible({ timeout: 15_000 });

    // Pick the requested template (Empty is preselected, but selecting again is idempotent).
    await this.templateCard(templateId).click();

    await this.modalNameInput.fill(name);

    if (opts.registerAsLibrary && templateId === 'library') {
      await this.modalAutoImport.check();
    }

    await this.modalSubmit.click();

    // The gallery navigates straight to the new project's editor on success.
    await this.page.waitForURL(/\/editor\/.+/, { timeout: 15_000 });

    // Return to /projects so legacy assertions that target the card / search
    // input keep working — this mirrors the old modal's "stay on /projects"
    // post-submit behaviour.
    await this.page.goto('/projects');
    await expect(this.cardByName(name)).toBeVisible({ timeout: 10_000 });
  }

  /**
   * Removes a project via UI. `confirm` lets a test exercise the cancel path.
   * Best-effort — if the card isn't present, returns silently.
   *
   * `.project-card__actions` is pointer-events:none until :hover or
   * :focus-within (pages.css:1305+), so we hover before clicking.
   */
  async removeProject(name: string, confirm: 'yes' | 'no' = 'yes') {
    const card = this.cardByName(name);
    if (!(await card.isVisible().catch(() => false))) return;

    await card.hover();
    const deleteBtn = card.locator('.project-card__action--danger');
    await deleteBtn.click();

    const dialog = new ConfirmDialog(this.page);
    await expect(dialog.root).toBeVisible();

    if (confirm === 'yes') {
      await dialog.confirmDanger.click();
      await expect(card).toBeHidden({ timeout: 10_000 });
    } else {
      await dialog.cancelSecondary.click();
      await expect(dialog.root).toBeHidden();
      await expect(card).toBeVisible();
    }
  }

  async openProject(name: string) {
    await this.cardByName(name).locator('a.project-card__link').first().click();
    await this.page.waitForURL(/\/editor\/.+/);
  }
}
