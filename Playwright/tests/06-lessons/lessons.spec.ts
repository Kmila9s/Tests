import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

interface ManifestLesson {
  id: string;
  title?: string;
  kind?: string;
}
interface ManifestModule {
  id: string;
  title?: string;
  lessons: ManifestLesson[];
}
interface Manifest {
  modules: ManifestModule[];
}

const manifest: Manifest = JSON.parse(
  fs.readFileSync(
    path.resolve(
      __dirname,
      '../../../../App/Kmila/Kmila.Shared/wwwroot/Learn/manifest.json',
    ),
    'utf8',
  ),
);

const allLessons = manifest.modules.flatMap((m) =>
  m.lessons.map((l) => ({ moduleId: m.id, lessonId: l.id, title: l.title ?? l.id })),
);

test.describe('Learn — catalog + per-lesson smoke', () => {
  test.setTimeout(60_000);

  test('catalog page renders all modules from the manifest', async ({ page }) => {
    await page.goto('/learn');
    // Cards are lazy — poll until the count stabilizes above the manifest size.
    await expect.poll(async () => page.locator('.learn-card').count(), {
      timeout: 15_000,
      intervals: [300, 600, 1000],
    }).toBeGreaterThanOrEqual(manifest.modules.length);
  });

  test('search filters the catalog live', async ({ page }) => {
    await page.goto('/learn');
    // Settle until the catalog has finished lazy-loading: count must be at
    // least the manifest module count.
    await expect
      .poll(async () => page.locator('.learn-card').count(), {
        timeout: 15_000,
        intervals: [300, 600, 1000],
      })
      .toBeGreaterThanOrEqual(manifest.modules.length);

    const search = page.locator('label.learn-hero__search input.form-control').first();
    const totalBefore = await page.locator('.learn-card').count();
    expect(totalBefore).toBeGreaterThan(0);
    await search.fill('zzzzz-no-match-zzzzz');
    // The live filter binds to oninput; poll for the count to drop.
    await expect
      .poll(async () => page.locator('.learn-card').count(), { timeout: 5_000 })
      .toBeLessThan(totalBefore);
  });

  // Allow-list for pre-existing JS errors not caused by anything this spec
  // touches. The "anonymous define" warning comes from Monaco/AMD re-loading
  // during Blazor navigation and predates this suite.
  const PAGEERROR_ALLOW = [
    /Can only have one anonymous define call per script file/i,
  ];

  // Iterate every lesson — assert it loads without 5xx and a body renders.
  for (const { moduleId, lessonId, title } of allLessons) {
    test(`lesson ${moduleId}/${lessonId} loads`, async ({ page }) => {
      const errors: string[] = [];
      page.on('pageerror', (e) => {
        if (!PAGEERROR_ALLOW.some((rx) => rx.test(e.message))) {
          errors.push(`pageerror: ${e.message}`);
        }
      });
      page.on('response', (r) => {
        if (r.status() >= 500) errors.push(`HTTP ${r.status()} ${r.url()}`);
      });

      await page.goto(`/learn/${moduleId}/${lessonId}`);
      const body = page.locator('article.lesson-body');
      await expect(body).toBeVisible({ timeout: 15_000 });

      const notFound = await page.locator('.lesson-missing').count();
      expect(notFound).toBe(0);

      expect(errors, `errors during ${moduleId}/${lessonId}: ${errors.join('; ')}`).toEqual([]);
      expect(title.length).toBeGreaterThan(0);
    });
  }
});
