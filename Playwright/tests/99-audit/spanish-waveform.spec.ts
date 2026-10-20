import { test } from '@playwright/test';
import * as fs from 'fs';

test('Spanish waveform toolbar @ medium', async ({ page }) => {
  await page.setViewportSize({ width: 900, height: 800 });
  await page.addInitScript(() => {
    try {
      localStorage.setItem('kmila.tutorialSeen', '1');
      localStorage.setItem('kmila.tutorialSeen.editor', '1');
    } catch {}
  });
  // Drop the user into a sample project quickly
  await page.goto('/editor/282', { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);
  // Capture the empty-state editor — the toolbar layout we care about is the
  // viewer container's chrome which is independent of the sim having data.
  fs.writeFileSync('/tmp/kmila_ui_audit/AUDIT_es_editor_medium.png', await page.screenshot());
});

test('Spanish editor @ wide', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.addInitScript(() => {
    try {
      localStorage.setItem('kmila.tutorialSeen', '1');
      localStorage.setItem('kmila.tutorialSeen.editor', '1');
    } catch {}
  });
  await page.goto('/editor/282', { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);
  fs.writeFileSync('/tmp/kmila_ui_audit/AUDIT_es_editor_wide.png', await page.screenshot());
});
