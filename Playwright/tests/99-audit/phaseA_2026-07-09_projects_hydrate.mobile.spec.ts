import { test, expect } from '@playwright/test';

test.setTimeout(60_000);

// F-A-05: /projects on APK used to keep the skeleton visible forever when
// Projects.RehomeIfStaleAsync threw. On Web the skeleton always resolves,
// but this regression pins the *swap timing* so any future breakage of the
// StateHasChanged + finally-block guarantees is caught on desktop + mobile.
test('/projects skeleton resolves within 8 s to either grid or empty state', async ({ page }) => {
  await page.goto('/projects', { waitUntil: 'domcontentloaded' });

  // At most 8 s — the DB list on cold-start including template cache init
  // should never take longer than that on the Kmila.Web target.
  const skeleton = page.locator('.project-card--skeleton');
  await expect(async () => {
    const c = await skeleton.count();
    expect(c).toBe(0);
  }).toPass({ timeout: 8_000 });

  // After the skeleton is gone, one of three post-hydrate surfaces must
  // render: empty-state card, project card grid, or the "Choose a template"
  // modal that auto-opens on empty databases.
  const emptyState = page.locator('.projects-empty');
  const projectCard = page.locator('.project-card:not(.project-card--skeleton)');
  const templateModal = page.locator('.modal, [role="dialog"]');
  const emptyCount = await emptyState.count();
  const cardCount = await projectCard.count();
  const modalCount = await templateModal.count();
  expect(emptyCount + cardCount + modalCount).toBeGreaterThan(0);
});
