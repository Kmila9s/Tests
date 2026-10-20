import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

/**
 * Smoke — create a project via the modal. Kept as the canary for the
 * create flow; deeper coverage lives under 02-projects.
 */
test('create a new project from the Projects page', async ({ page }) => {
  const name = `Smoke ${Date.now()}`;
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.createProject(name, 'Smoke test project.');
  await expect(projects.cardByName(name)).toBeVisible();
});
