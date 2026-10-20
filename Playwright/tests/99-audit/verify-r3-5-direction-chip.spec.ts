// R3-5 verification — Visual widget props pane must show a direction
// chip (Entrada / Salida) that distinguishes input widgets (button,
// switch) from output widgets (LED, LED-array, stepper, 7-seg).

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

test.setTimeout(60_000);

test('R3-5: props pane shows correct Input/Output direction chip', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('kmila.tutorialSeen', '1');
    localStorage.setItem('kmila.tutorialSeen.projects', '1');
  });

  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject('Visual-Phase0-Passthrough');
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  if (!idMatch) throw new Error('No project id');
  const projectId = idMatch[1];

  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible();
  await page.waitForLoadState('load');

  const btnPaletteChip = page.getByTestId('visual-palette-button');
  const ledPaletteChip = page.getByTestId('visual-palette-led');
  await expect(btnPaletteChip).toBeEnabled({ timeout: 10_000 });

  await expect.poll(async () => {
    if ((await page.locator('.kviz-button').count()) === 0) await btnPaletteChip.click();
    if ((await page.locator('.kviz-led').count()) === 0) await ledPaletteChip.click();
    return (await page.locator('.kviz-button').count()) > 0
        && (await page.locator('.kviz-led').count()) > 0;
  }, { timeout: 10_000, intervals: [400, 700, 1000] }).toBe(true);

  // Click the button widget to open the floating props pane. Force-click
  // because the .kviz-button-cap child intercepts pointer events (that's
  // where the physical button-press is registered during a live run).
  await page.locator('.kviz-button').first().click({ force: true });
  const propsHeader = page.locator('.visual-page__props-header');
  await expect(propsHeader).toBeVisible();
  const inputChip = propsHeader.locator('.visual-page__props-dir--in');
  await expect(inputChip).toBeVisible();
  await expect(inputChip).toHaveText(/Entrada|Input/i);
  await expect(propsHeader.locator('.visual-page__props-dir--out')).toHaveCount(0);

  // Close pane, wait for aside teardown, then click the LED widget.
  await propsHeader.locator('.visual-page__props-close').click();
  await expect(page.locator('.visual-page__props')).toHaveCount(0);
  await page.locator('.kviz-led').first().click({ force: true });
  await expect(propsHeader).toBeVisible();
  const outputChip = propsHeader.locator('.visual-page__props-dir--out');
  await expect(outputChip).toBeVisible();
  await expect(outputChip).toHaveText(/Salida|Output/i);
  await expect(propsHeader.locator('.visual-page__props-dir--in')).toHaveCount(0);
});
