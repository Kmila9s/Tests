import { test, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const evid = (name: string) => path.join(EVIDENCE_DIR, `EVID_A_${name}.png`);

async function shortGoto(page: Page, url: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1200); // Blazor hydration — no networkidle (never settles)
}

async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
}

test.describe('Phase A — shell details', () => {
  test.setTimeout(60_000);

  test('A-11 sidebar collapse toggle', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    await shortGoto(page, '/');
    await dismissTour(page);
    await page.screenshot({ path: evid('A11a_sidebar_expanded') });
    const collapse = page.getByRole('button', { name: /Contraer barra lateral|Collapse/i })
      .or(page.locator('a, button').filter({ hasText: /Contraer barra lateral/i }));
    if (await collapse.count()) {
      await collapse.first().click().catch(() => {});
      await page.waitForTimeout(500);
      await page.screenshot({ path: evid('A11b_sidebar_collapsed') });
    } else {
      await page.screenshot({ path: evid('A11b_sidebar_collapse_missing') });
    }
  });

  test('A-16 A-18 A-19 profile menu with lang/theme', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    await shortGoto(page, '/');
    await dismissTour(page);
    const avatar = page.locator('.kmila-avatar, [class*="avatar"], [class*="profile"], .kmila-topbar-user, img[alt*="user" i]').first();
    if (await avatar.count()) {
      await avatar.click().catch(() => {});
      await page.waitForTimeout(600);
    }
    await page.screenshot({ path: evid('A16_profile_menu_open') });

    // Language switcher — usually a globe icon or "Idioma" label
    const langEl = page.locator('[class*="language"], [class*="LanguageChanger"], .kmila-langchanger, button:has-text("Idioma")').first();
    if (await langEl.count()) {
      await langEl.click().catch(() => {});
      await page.waitForTimeout(400);
      await page.screenshot({ path: evid('A18_language_menu') });
    } else {
      await page.screenshot({ path: evid('A18_language_missing') });
    }

    // Theme switcher — usually a sun/moon icon
    await shortGoto(page, '/');
    await dismissTour(page);
    const themeEl = page.locator('[class*="theme"], [class*="ThemeSwitcher"], button:has-text("Tema")').first();
    if (await themeEl.count()) {
      await themeEl.click().catch(() => {});
      await page.waitForTimeout(300);
      await page.screenshot({ path: evid('A19_theme_menu') });
    } else {
      await page.screenshot({ path: evid('A19_theme_missing') });
    }
  });

  test('A-08 visual standalone route resolves', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    // We don't have a specific ProjectId, so try one from Projects screen
    await shortGoto(page, '/projects');
    await dismissTour(page);
    const firstProject = page.locator('a[href*="/editor/"], [class*="project-card"] a, .card a').first();
    let projectId: string | null = null;
    if (await firstProject.count()) {
      const href = await firstProject.getAttribute('href');
      const m = href?.match(/\/editor\/([^/?#]+)/);
      if (m) projectId = m[1];
    }
    if (projectId) {
      await shortGoto(page, `/visual/${projectId}`);
      await page.screenshot({ path: evid('A08_visual_standalone') });
    } else {
      await page.screenshot({ path: evid('A08_visual_no_project_id') });
    }
  });
});
