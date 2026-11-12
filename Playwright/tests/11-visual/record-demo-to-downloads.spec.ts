// In-app canvas recorder verification — Wave 5 canonical spec.
//
// Drives the actual toolbar buttons (Record → Stop) and captures the
// browser download the app emits. No Playwright CDP video capture, no
// ffmpeg — the whole pipeline is the vendored kmilaCanvasRender + the
// MediaRecorder in the browser.
//
// Fixture: 4-bit counter (clk / rst / count[3..0]) with an LED array
// wired to `count`. Kmila auto-clocks `clk`. The video should show the
// LED-array bits toggling through 16 binary states.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

test('In-app Record button captures the running counter to a webm download', async ({ page }) => {
  test.setTimeout(180_000);

  // 1) Open the counter project.
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject(fixtureByName('Visual-Phase1-Counter4').projectName);
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  if (!idMatch) throw new Error(`No project id in ${page.url()}`);
  await page.goto(`/visual/${idMatch[1]}`);
  await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
  await page.waitForLoadState('networkidle');

  // 2) Confirm the bespoke rasteriser is on window — without it, the
  //    recorder returns { ok: false, reason: 'renderer-missing' }.
  const rendererOk = await page.evaluate(() => typeof (window as any).kmilaCanvasRender === 'function');
  expect(rendererOk).toBe(true);

  // 3) Add an LED array — primary visualisation channel for count.
  const addLedArray = page.locator('[data-testid="visual-palette-led-array"]');
  await expect(addLedArray).toBeEnabled({ timeout: 10_000 });
  await expect.poll(async () => {
    if ((await page.locator('.kviz-led-array').count()) === 0) await addLedArray.click();
    return (await page.locator('.kviz-led-array').count()) > 0;
  }, { timeout: 10_000 }).toBe(true);

  // 4) Wire led-array.anodes → count.
  await page.locator('[data-testid="visual-wire"]').click();
  const modal = page.locator('.kviz-wiring-modal');
  await expect(modal).toBeVisible({ timeout: 5_000 });
  // Wiring modal groups pins per component, collapsed by default — expand all.
  for (let i = 0; i < 20; i++) {
    const closed = modal.locator('.kviz-wiring-group[data-open="false"] .kviz-wiring-group-header');
    if ((await closed.count()) === 0) break;
    await closed.first().click();
  }
  await modal.locator('.kviz-wiring-table tbody tr')
    .filter({ has: page.locator('code', { hasText: 'anodes' }) }).last()
    .locator('select').selectOption('count');
  await modal.locator('[data-testid="visual-wiring-done"]').click();
  await expect(modal).toBeHidden();

  // 5) Run — auto-clocks clk. Wait for Stop + runbar to confirm the
  //    coordinator is actually driving frames.
  await page.locator('[data-testid="visual-run"]').click();
  await expect(page.locator('[data-testid="visual-stop"]')).toBeVisible({ timeout: 5_000 });
  await expect(page.locator('.visual-page__runbar')).toBeVisible({ timeout: 5_000 });
  // Give the counter ~1 s so it's past state 0 when recording starts.
  await page.waitForTimeout(1_000);

  // 6) Click Record. Elapsed chip appears as soon as the JS-side session starts.
  await page.locator('[data-testid="visual-record"]').click();
  await expect(page.locator('[data-testid="visual-record-elapsed"]')).toBeVisible({ timeout: 5_000 });

  // 7) Record ~8 seconds of the counter running.
  await page.waitForTimeout(8_000);

  // 8) Click Stop — this fires the browser download event.
  const [download] = await Promise.all([
    page.waitForEvent('download'),
    page.locator('[data-testid="visual-record-stop"]').click(),
  ]);
  expect(download.suggestedFilename()).toMatch(/\.webm$/i);

  // Stop the sim cleanly (nice to leave state clean; not required).
  await page.locator('[data-testid="visual-stop"]').click().catch(() => {});

  // 9) Save into ~/Downloads with a stable name so the user can find it.
  const home = os.homedir();
  const downloadsDir = path.join(home, 'Downloads');
  if (!fs.existsSync(downloadsDir)) fs.mkdirSync(downloadsDir, { recursive: true });
  const outPath = path.join(downloadsDir, 'kmila-counter-canvas.webm');
  await download.saveAs(outPath);

  const stats = fs.statSync(outPath);
  console.log(`\n[record-demo] wrote ${outPath} (${stats.size.toLocaleString()} bytes)\n`);

  // 10) Assertions.
  //  - Valid WebM (EBML magic 1A 45 DF A3).
  const head = fs.readFileSync(outPath).subarray(0, 4);
  expect(head[0]).toBe(0x1a);
  expect(head[1]).toBe(0x45);
  expect(head[2]).toBe(0xdf);
  expect(head[3]).toBe(0xa3);
  //  - Non-blank recording. Blank webms sit around 1 KB. The bespoke
  //    rasteriser paints flat shapes so VP9 compresses static stretches
  //    aggressively; a real capture of ~8 seconds cycling an LED array
  //    lands around 30-50 KB depending on how many bits change per frame.
  //    20 KB is comfortably above the blank-frame floor.
  expect(stats.size).toBeGreaterThan(20_000);
});
