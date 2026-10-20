// Canvas export — §6 Item 2.
//
// Verifies PNG + SVG export. Both go through the SVG <foreignObject>
// pipeline in kmila-canvas-export.js — SVG is emitted as-is, PNG is
// rasterised via an offscreen canvas. Assertions check download magic
// bytes rather than pixel content so the spec doesn't drift with cosmetic
// widget-styling tweaks.
//
// PDF is intentionally deferred until jspdf is vendored under
// wwwroot/lib/jspdf/ — kmilaCanvasExportSupportsPdf() returns false.

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

test.describe('Visual canvas export', () => {
  test.setTimeout(120_000);

  test('exports SVG with an <svg> root element', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase1-ToggleLed').projectName);

    // Add a couple of widgets so the export isn't empty.
    const swBtn = page.locator('[data-testid="visual-palette-switch"]');
    await expect(swBtn).toBeEnabled({ timeout: 10_000 });
    await swBtn.click();
    const ledBtn = page.locator('[data-testid="visual-palette-led"]');
    await ledBtn.click();

    const btn = page.locator('[data-testid="visual-export-svg"]');
    await expect(btn).toBeVisible();
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      btn.click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/\.svg$/i);
    const path = await download.path();
    expect(path).toBeTruthy();
    const buf = fs.readFileSync(path!);
    // SVG starts with '<' (0x3C). Also check for the string "<svg" inside
    // the first 128 bytes to reject a plain HTML export.
    expect(buf[0]).toBe(0x3c);
    const head = buf.slice(0, 256).toString('utf8');
    expect(head).toMatch(/<svg[\s>]/);
  });

  test('PNG is now supported (bespoke rasteriser); PDF still deferred', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const supports = await page.evaluate(() => ({
      png: typeof (window as any).kmilaCanvasExportSupportsPng === 'function'
        ? (window as any).kmilaCanvasExportSupportsPng() : null,
      pdf: typeof (window as any).kmilaCanvasExportSupportsPdf === 'function'
        ? (window as any).kmilaCanvasExportSupportsPdf() : null,
    }));
    // PNG is truly supported now via kmilaCanvasRender. PDF is still
    // documented as not-yet-supported — needs jspdf vendored.
    expect(supports.png).toBe(true);
    expect(supports.pdf).toBe(false);
  });
});
