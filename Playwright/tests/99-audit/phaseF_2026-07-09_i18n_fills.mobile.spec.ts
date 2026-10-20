import { test, expect } from '@playwright/test';
import * as fs from 'fs';

const DICT_DIR = '/Users/miku/Documents/Kmila-9s/App/Kmila/Kmila.Shared/wwwroot/Dictionaries';
const LANGS = ['en', 'es', 'fr', 'de', 'ar', 'ch', 'jp'];
const NEW_KEYS_ALL = [
  'SETTINGS_TEMPLATE_DOWNLOADED',
  'OUTPUT_LOG_DOWNLOADED',
  'COMMON_EXPORT_FAILED',
];
const WAVEFORM_BP_KEYS = [
  'WAVEFORM_TIME_BP_ADD',
  'WAVEFORM_TIME_BP_TITLE',
  'WAVEFORM_TIME_BP_ADDED',
  'WAVEFORM_TIME_BP_REMOVED',
];

test.setTimeout(120_000);

test('every dictionary has all Batch-1 keys (2026-07-09 audit fills)', () => {
  const missing: { lang: string; keys: string[] }[] = [];
  for (const lang of LANGS) {
    const dict = JSON.parse(fs.readFileSync(`${DICT_DIR}/${lang}.json`, 'utf-8'));
    const required = [...NEW_KEYS_ALL, ...WAVEFORM_BP_KEYS];
    const absent = required.filter(k => !(k in dict) || typeof dict[k] !== 'string' || dict[k].length === 0);
    if (absent.length) missing.push({ lang, keys: absent });
  }
  expect(missing, JSON.stringify(missing, null, 2)).toEqual([]);
});

test('WAVEFORM_TIME_BP_ADDED/REMOVED preserve {0} placeholder', () => {
  for (const lang of LANGS) {
    const dict = JSON.parse(fs.readFileSync(`${DICT_DIR}/${lang}.json`, 'utf-8'));
    expect(dict.WAVEFORM_TIME_BP_ADDED, `${lang} WAVEFORM_TIME_BP_ADDED missing {0}`).toContain('{0}');
    expect(dict.WAVEFORM_TIME_BP_REMOVED, `${lang} WAVEFORM_TIME_BP_REMOVED missing {0}`).toContain('{0}');
  }
});

test('Settings page — no raw SETTINGS_TEMPLATE_DOWNLOADED leak', async ({ page }) => {
  await page.goto('/settings', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  const bodyText = await page.evaluate(() => document.body.innerText);
  expect(bodyText).not.toContain('SETTINGS_TEMPLATE_DOWNLOADED');
  expect(bodyText).not.toContain('OUTPUT_LOG_DOWNLOADED');
  expect(bodyText).not.toContain('COMMON_EXPORT_FAILED');
});
