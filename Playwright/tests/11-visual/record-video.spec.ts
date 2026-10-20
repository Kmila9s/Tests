// Canvas video recording v1 — §6 Item 1.
//
// MediaRecorder + captureStream + offscreen canvas fed by SVG snapshots.
// Verifies the recording state machine + that a webm blob download fires
// on Stop. Playwright's chromium ships MediaRecorder with vp9 support so
// magic-byte assertions are reliable.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';
import * as fs from 'fs';

async function openVisual(page: any, projectName: string) {
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject(projectName);
  const idMatch = page.url().match(/\/editor\/(\d+)/);
  if (!idMatch) throw new Error(`No id in ${page.url()}`);
  const projectId = idMatch[1];
  await page.goto(`/visual/${projectId}`);
  await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
  await page.waitForLoadState('networkidle');
  return projectId;
}

test.describe('Visual canvas recording', () => {
  test.setTimeout(120_000);

  test('kmilaCanvasRecordStart / Stop / Status are exposed', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const exposed = await page.evaluate(() => ({
      start: typeof (window as any).kmilaCanvasRecordStart === 'function',
      stop:  typeof (window as any).kmilaCanvasRecordStop  === 'function',
      status:typeof (window as any).kmilaCanvasRecordStatus=== 'function',
    }));
    expect(exposed.start).toBe(true);
    expect(exposed.stop).toBe(true);
    expect(exposed.status).toBe(true);
  });

  test('Record → wait → Stop produces a webm blob download', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase1-ToggleLed').projectName);

    // Add a widget so the recorded frame isn't blank canvas.
    const swBtn = page.locator('[data-testid="visual-palette-switch"]');
    await expect(swBtn).toBeEnabled({ timeout: 10_000 });
    await swBtn.click();

    const recBtn = page.locator('[data-testid="visual-record"]');
    await expect(recBtn).toBeVisible();
    await recBtn.click();

    // Elapsed chip appears within a second or so.
    const elapsedChip = page.locator('[data-testid="visual-record-elapsed"]');
    await expect(elapsedChip).toBeVisible({ timeout: 5_000 });

    // Record for ~2 s of frames.
    await page.waitForTimeout(2_000);

    const stopBtn = page.locator('[data-testid="visual-record-stop"]');
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      stopBtn.click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/\.webm$/i);
    const path = await download.path();
    expect(path).toBeTruthy();
    const buf = fs.readFileSync(path!);
    // WebM/Matroska EBML magic: 1A 45 DF A3
    expect(buf[0]).toBe(0x1a);
    expect(buf[1]).toBe(0x45);
    expect(buf[2]).toBe(0xdf);
    expect(buf[3]).toBe(0xa3);
    // Non-zero size. Actual encoded size depends on frame content —
    // static / near-blank frames compress extremely well and can land
    // under 1 KB, so we only guard against the empty-blob regression.
    expect(buf.length).toBeGreaterThan(50);
  });
});
