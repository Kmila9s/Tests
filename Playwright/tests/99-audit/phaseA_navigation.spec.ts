import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const PHASE = 'A';

function evid(name: string) {
  return path.join(EVIDENCE_DIR, `EVID_${PHASE}_${name}.png`);
}

async function ensureNoTour(page: Page) {
  // Dismiss the welcome tour if it appears — it targets NavMenu items and
  // blocks screenshots on cold starts.
  try {
    const dismiss = page.locator('.kmila-tutorial-skip, [data-tutorial-skip]');
    if (await dismiss.count()) await dismiss.first().click({ timeout: 500 });
  } catch { /* no tour */ }
}

async function go(page: Page, url: string, label: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
  await ensureNoTour(page);
  // give Blazor a beat to finish hydration
  await page.waitForTimeout(500);
  await page.screenshot({ path: evid(label), fullPage: false });
}

test.describe('Phase A — Navigation & Shell', () => {
  test.setTimeout(120_000);

  test('walk every top-level route + shell surface', async ({ page }) => {
    if (!fs.existsSync(EVIDENCE_DIR)) fs.mkdirSync(EVIDENCE_DIR, { recursive: true });

    const findings: Array<{id: string, label: string, evid: string, note: string}> = [];

    // A-01 Home
    await go(page, '/', 'A01_home');
    const heroExists = await page.locator('h1, .hero, [class*="hero"]').first().isVisible().catch(() => false);
    findings.push({id: 'A-01', label: 'Home hero renders', evid: evid('A01_home'), note: heroExists ? 'OK' : 'MISSING_HERO'});

    // A-02 Projects
    await go(page, '/projects', 'A02_projects');
    const projectsGrid = await page.locator('[class*="project"], .card, .kmila-card').first().isVisible().catch(() => false);
    findings.push({id: 'A-02', label: 'Projects grid', evid: evid('A02_projects'), note: projectsGrid ? 'OK' : 'MISSING_GRID'});

    // A-04 Learn
    await go(page, '/learn', 'A04_learn');
    findings.push({id: 'A-04', label: '/learn resolves', evid: evid('A04_learn'), note: 'captured'});

    // A-05 Practice
    await go(page, '/practice', 'A05_practice');
    findings.push({id: 'A-05', label: '/practice resolves', evid: evid('A05_practice'), note: 'captured'});

    // A-06 Concepts
    await go(page, '/concepts', 'A06_concepts');
    findings.push({id: 'A-06', label: '/concepts resolves', evid: evid('A06_concepts'), note: 'captured'});

    // A-07 Settings
    await go(page, '/settings', 'A07_settings');
    const settingsCards = await page.locator('.card, .kmila-card, [class*="settings"]').count();
    findings.push({id: 'A-07', label: '/settings cards visible', evid: evid('A07_settings'), note: `card_count=${settingsCards}`});

    // A-09 Logs
    await go(page, '/logs', 'A09_logs');
    findings.push({id: 'A-09', label: '/logs resolves', evid: evid('A09_logs'), note: 'captured'});

    // A-10 Privacy
    await go(page, '/privacy', 'A10_privacy');
    findings.push({id: 'A-10', label: '/privacy resolves', evid: evid('A10_privacy'), note: 'captured'});

    // A-16 Profile menu
    await go(page, '/', 'A16_home_pre_profile');
    const profileTrigger = page.locator('[class*="profile"], [aria-label*="profile" i], .kmila-topbar-user').first();
    let profileOpens = false;
    if (await profileTrigger.count()) {
      await profileTrigger.click().catch(() => {});
      await page.waitForTimeout(300);
      profileOpens = await page.locator('.dropdown-menu.show, [class*="profile-menu"], [role="menu"]').first().isVisible().catch(() => false);
    }
    await page.screenshot({ path: evid('A16_profile_menu') });
    findings.push({id: 'A-16', label: 'Profile menu opens', evid: evid('A16_profile_menu'), note: profileOpens ? 'OK' : 'NO_MENU'});

    // A-17 ConfigurationsPill scoped to /editor/*
    await go(page, '/projects', 'A17a_topbar_projects');
    const pillOnProjects = await page.locator('[class*="ConfigurationsPill"], [class*="configurations-pill"], .configurations-pill').first().isVisible().catch(() => false);
    findings.push({id: 'A-17a', label: 'ConfigurationsPill hidden off /editor', evid: evid('A17a_topbar_projects'), note: pillOnProjects ? 'LEAK' : 'OK'});

    // A-18 Language changer presence
    await go(page, '/', 'A18_language_home');
    const langTrigger = await page.locator('[class*="language"], [aria-label*="language" i], [aria-label*="idioma" i]').count();
    findings.push({id: 'A-18', label: 'Language switcher present', evid: evid('A18_language_home'), note: langTrigger > 0 ? `count=${langTrigger}` : 'MISSING'});

    // A-19 Theme switcher
    const themeTrigger = await page.locator('[class*="theme"], [aria-label*="theme" i], [aria-label*="tema" i]').count();
    findings.push({id: 'A-19', label: 'Theme switcher present', evid: evid('A18_language_home'), note: themeTrigger > 0 ? `count=${themeTrigger}` : 'MISSING'});

    // A-24 Footer link → /privacy
    const footerPrivacy = page.locator('footer a[href*="privacy"], a[href="/privacy"]').first();
    findings.push({id: 'A-24', label: 'Footer link to /privacy', evid: evid('A18_language_home'), note: (await footerPrivacy.count()) > 0 ? 'FOUND' : 'MISSING'});

    // dump findings as JSON alongside screenshots
    fs.writeFileSync(path.join(EVIDENCE_DIR, `phaseA_findings.json`), JSON.stringify(findings, null, 2));
    console.log('PHASEA_FINDINGS_JSON:', JSON.stringify(findings, null, 2));
  });
});
