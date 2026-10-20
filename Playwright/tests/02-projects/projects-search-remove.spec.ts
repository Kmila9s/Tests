import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

test.describe('Projects page — search + remove flow', () => {
  test('search bar live-filters by name', async ({ page }) => {
    const projects = new ProjectsPage(page);
    const stamp = Date.now();
    const matchName = `Alpha-${stamp}`;
    const otherName = `Bravo-${stamp}`;

    await projects.goto();
    await projects.createProject(matchName);
    await projects.createProject(otherName);

    await projects.searchInput.fill(matchName);
    await expect(projects.cardByName(matchName)).toBeVisible();
    await expect(projects.cardByName(otherName)).toBeHidden();

    // Clearing via the X button restores both cards.
    await projects.searchClear.click();
    await expect(projects.cardByName(matchName)).toBeVisible();
    await expect(projects.cardByName(otherName)).toBeVisible();

    // Cleanup
    await projects.removeProject(matchName);
    await projects.removeProject(otherName);
  });

  test('remove project — cancel keeps the card', async ({ page }) => {
    const projects = new ProjectsPage(page);
    const name = `Cancel-${Date.now()}`;

    await projects.goto();
    await projects.createProject(name);

    await projects.removeProject(name, 'no');

    await expect(projects.cardByName(name)).toBeVisible();

    // Cleanup
    await projects.removeProject(name, 'yes');
  });

  test('remove project — confirm deletes the card', async ({ page }) => {
    const projects = new ProjectsPage(page);
    const name = `Delete-${Date.now()}`;

    await projects.goto();
    await projects.createProject(name);
    await projects.removeProject(name, 'yes');

    await expect(projects.cardByName(name)).toBeHidden();
  });
});
