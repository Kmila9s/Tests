import { test, Page } from '@playwright/test';
import * as fs from 'fs';

const STATE_PATH = '/tmp/kmila_audit_2026-07-08/phaseF_i18n_state.json';
function save(s: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(s, null, 2)); }

const LANGS: { code: string; label: RegExp; expectedH1Learn: RegExp }[] = [
  { code: 'en', label: /^EN\s+English$/,   expectedH1Learn: /Learn/i },
  { code: 'es', label: /^ES\s+Español$/,   expectedH1Learn: /Aprende/i },
  { code: 'fr', label: /^FR\s+Français$/,  expectedH1Learn: /Apprendre|Apprends/i },
  { code: 'de', label: /^DE\s+Deutsch$/,   expectedH1Learn: /Lerne/i },
  { code: 'ch', label: /^CH\s+中文$/,       expectedH1Learn: /学|学习/ },
  { code: 'jp', label: /^JP\s+日本語$/,     expectedH1Learn: /学ぶ|学習/ },
  { code: 'ar', label: /^AR\s+عربي$/,       expectedH1Learn: /تعلم|VHDL/ },
];

test.setTimeout(360_000);
test('7-language sweep over /learn, /practice, /concepts', async ({ page }) => {
  const state: any = { perLang: {} };
  const consoleMsgs: string[] = [];
  page.on('console', m => consoleMsgs.push(`[${m.type()}] ${m.text().slice(0, 200)}`));

  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);
  await page.waitForFunction(() => (window as any).Blazor?._internal, { timeout: 15_000 }).catch(() => {});
  await page.waitForTimeout(2000);

  for (const lang of LANGS) {
    // Set the language via profile menu button (Kmila's canonical path)
    // First open profile menu — click the top-right avatar
    const avatar = page.locator('button').filter({ hasText: /^$/ }).nth(0); // avatar with empty text
    // Try direct locator via language buttons — they may already be visible if picker persists
    const langBtn = page.locator('button').filter({ hasText: lang.label }).first();
    let clicked = false;
    if (await langBtn.count()) {
      await langBtn.scrollIntoViewIfNeeded().catch(() => {});
      await langBtn.click({ force: true }).catch(() => {});
      clicked = true;
    } else {
      // Open profile menu by clicking an avatar-role button then retry
      const profileTrigger = page.locator('[class*="profile"], [aria-label*="profile" i], [aria-label*="cuenta" i], [class*="avatar"]').first();
      if (await profileTrigger.count()) {
        await profileTrigger.click({ force: true }).catch(() => {});
        await page.waitForTimeout(1000);
        const retry = page.locator('button').filter({ hasText: lang.label }).first();
        if (await retry.count()) {
          await retry.click({ force: true }).catch(() => {});
          clicked = true;
        }
      }
    }
    await page.waitForTimeout(2000);

    const info: any = { clicked };
    for (const url of ['/learn', '/practice', '/concepts']) {
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(2500);
      const pageInfo = await page.evaluate(() => {
        const h1 = document.querySelector('h1')?.textContent?.trim() ?? null;
        const bodyText = document.body.innerText;
        const rawKeys = [...bodyText.matchAll(/\b(LEARN|PRACTICE|CONCEPTS|LESSON|EXERCISE|MODULE|COMMON|NAV|EDITOR|OUTPUT|RUN|SETTINGS|HOME|FLOW|WAVEFORM|SIM|APP|SETUP|SIGN|LOGIN|TOP|BUNDLE|LOGS)_[A-Z0-9_]{2,}\b/g)]
          .map(m => m[0]);
        return {
          h1,
          rawKeyCount: rawKeys.length,
          rawKeyUnique: Array.from(new Set(rawKeys)).slice(0, 30),
        };
      });
      info[url] = pageInfo;
    }
    // Sanity: does /learn h1 look translated
    info.h1LearnMatchesExpected = lang.expectedH1Learn.test(info['/learn']?.h1 ?? '');
    state.perLang[lang.code] = info;
    save(state);
  }

  state.consoleTail = consoleMsgs.slice(-15);
  save(state);
});
