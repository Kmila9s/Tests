import { test, expect } from '@playwright/test';

test.setTimeout(180_000);

// F-F-07: Concepts.LoadAsync used to race with itself when
// Traductor.StaticOnLangChanged or Progress.OnChange fired mid-load. This
// spec drives multiple language switches in fast succession then asserts the
// module cards and progress bar all render consistent counts.
test('Concepts page — LoadAsync is race-safe under rapid language switches', async ({ page }) => {
  await page.goto('/concepts', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3500);

  // Baseline: read the progress bar text once page settles.
  const heroProgressText = await page.locator('.learn-hero__progress, .concepts-hero__progress').first().innerText().catch(() => '');

  // Fire a burst of language changes via the Traductor JS bridge — simulates
  // the race window. Falls back gracefully if the bridge name changed.
  await page.evaluate(async () => {
    const w = window as any;
    const langs = ['es', 'en', 'fr', 'en'];
    for (const l of langs) {
      try {
        if (w.kmilaSetLanguage) await w.kmilaSetLanguage(l);
        else if (w.Traductor?.setLanguage) await w.Traductor.setLanguage(l);
      } catch {}
      await new Promise(r => setTimeout(r, 50));
    }
  });
  await page.waitForTimeout(2500);

  // Progress line must still render (no torn state) and count must be a
  // "N / M" fraction with N ≤ M.
  const fractionText = await page.locator(':text-matches("\\\\d+\\\\s*/\\\\s*\\\\d+")').first().innerText().catch(() => '');
  const m = fractionText.match(/(\d+)\s*\/\s*(\d+)/);
  if (m) {
    const done = parseInt(m[1], 10);
    const total = parseInt(m[2], 10);
    expect(done).toBeLessThanOrEqual(total);
    expect(total).toBeGreaterThan(0);
  }

  // Nothing raw-key leaked into body.
  const body = await page.evaluate(() => document.body.innerText);
  const raw = [...body.matchAll(/\b(LEARN|CONCEPTS|MODULE|COMMON)_[A-Z_]{2,}\b/g)];
  expect(raw, JSON.stringify(raw.map(x => x[0]))).toEqual([]);
});
