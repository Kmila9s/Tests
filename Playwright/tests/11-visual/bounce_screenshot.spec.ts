// One-shot visual capture of the bounce checkbox UI for the screenshot
// review. Run after seed.spec.ts + wire_and_save.spec.ts have populated
// the Phase 0 fixtures.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

test('capture bounce UI', async ({ page }) => {
  test.setTimeout(60_000);
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject(fixtureByName('Visual-Phase0-Passthrough').projectName);
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  await page.goto(`/visual/${idMatch![1]}`);
  await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });

  const addBtn = page.locator('[data-testid="visual-add-button"]');
  await expect(addBtn).toBeEnabled();
  await expect.poll(async () => {
    if ((await page.locator('.kviz-button').count()) === 0) await addBtn.click();
    return (await page.locator('.kviz-button').count()) > 0;
  }, { timeout: 10_000 }).toBe(true);

  await page.locator('.kviz-button').last().click();
  await page.locator('.visual-page__props-checkbox').waitFor({ state: 'visible' });
  await page.locator('.visual-page__props').screenshot({ path: '/tmp/bounce_props.png' });
  await page.screenshot({ path: '/tmp/bounce_full.png', fullPage: false });
});
