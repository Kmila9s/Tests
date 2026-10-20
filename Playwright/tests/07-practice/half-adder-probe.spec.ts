import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { Monaco } from '../../pageobjects/Monaco';

/**
 * Phase 07 — Practice probe matrix for m1-from-zero/easy/01-half-adder.
 *
 * Each case submits a different VHDL variant and asserts the verdict class
 * on `.practice-verdict--{ok|wa|build|rt|to}`. This is the anti-cheat audit
 * deliverable in miniature — extend across more exercises by parametrizing
 * the same harness over additional `data/vhdl/practice/{exercise}-*.vhdl`.
 *
 * CHEAT-CANONICAL (copy of solution.vhdl) is currently **expected to pass**
 * with the Phase-0 waveform-diff validator — captured so future hardening
 * of the validator changes one constant in this file.
 */
// PracticeCatalog flattens disk paths into single-segment slugs at load time —
// `m1-from-zero/easy/01-half-adder/` on disk → `m1-half-adder` in the URL.
const EXERCISE = 'm1-half-adder';

const dataDir = path.resolve(__dirname, '../../data/vhdl/practice');
const right = fs.readFileSync(path.join(dataDir, 'half-adder-right.vhdl'), 'utf8');
const wrong = fs.readFileSync(path.join(dataDir, 'half-adder-wrong.vhdl'), 'utf8');
const half = fs.readFileSync(path.join(dataDir, 'half-adder-half.vhdl'), 'utf8');
const cheatHardcode = fs.readFileSync(
  path.join(dataDir, 'half-adder-cheat-hardcode.vhdl'),
  'utf8',
);

type VerdictClass = 'ok' | 'wa' | 'build' | 'rt' | 'to';

const verdictLocator = (page: any, klass: VerdictClass) =>
  page.locator(`.practice-verdict.practice-verdict--${klass}`);

async function submitVariant(page: any, source: string) {
  const monaco = new Monaco(page);
  await monaco.setValue(source);
  const submit = page.locator('button.practice-actions__submit');
  await expect(submit).toBeEnabled({ timeout: 15_000 });
  await submit.click();
  // Wait for grading to complete: the verdict block becomes visible and the
  // submit button leaves its disabled/grading state.
  await expect(submit).toBeEnabled({ timeout: 90_000 });
  await expect(page.locator('.practice-verdict')).toBeVisible({ timeout: 5_000 });
}

test.describe('Practice — half-adder 5-case probe', () => {
  test.setTimeout(180_000);

  test.beforeEach(async ({ page }) => {
    await page.goto(`/practice/${EXERCISE}`);
    await expect(page.locator('.practice-monaco')).toBeVisible({ timeout: 30_000 });
    const monaco = new Monaco(page);
    await monaco.waitForReady();
  });

  test('WRONG: swapped logic → WrongAnswer', async ({ page }) => {
    await submitVariant(page, wrong);
    await expect(verdictLocator(page, 'wa')).toBeVisible();
    await expect(verdictLocator(page, 'ok')).toHaveCount(0);
  });

  test('HALF: missing carry → WrongAnswer with at least one passing test', async ({ page }) => {
    await submitVariant(page, half);
    await expect(verdictLocator(page, 'wa')).toBeVisible();
    // Mixed test results: at least one .ok and at least one .ko in the test list.
    const okItems = await page.locator('.practice-verdict__tests li.ok').count();
    const koItems = await page.locator('.practice-verdict__tests li.ko').count();
    expect(okItems).toBeGreaterThan(0);
    expect(koItems).toBeGreaterThan(0);
  });

  test('RIGHT: canonical implementation → Solved', async ({ page }) => {
    await submitVariant(page, right);
    await expect(verdictLocator(page, 'ok')).toBeVisible();
  });

  test('CHEAT-HARDCODE: outputs hardcoded to 0 → WrongAnswer', async ({ page }) => {
    await submitVariant(page, cheatHardcode);
    await expect(verdictLocator(page, 'wa')).toBeVisible();
  });

  /**
   * Documents the known Phase-0 validator limitation: an exact copy of the
   * canonical solution passes (waveform-diff has no structural rubric).
   * If the validator is hardened to detect verbatim copies, flip the
   * expected verdict to `wa` and update PLAN.md accordingly.
   */
  test('CHEAT-CANONICAL-COPY: byte-for-byte solution copy → Solved (known limitation)', async ({
    page,
  }) => {
    await submitVariant(page, right);
    await expect(verdictLocator(page, 'ok')).toBeVisible();
  });
});
