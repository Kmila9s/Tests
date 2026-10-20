import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const EVIDENCE_DIR = '/tmp/kmila_audit_2026-07-08';
const STATE_PATH = path.join(EVIDENCE_DIR, 'phaseE_bundle_state.json');

async function shortGoto(page: Page, url: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1800);
}
async function dismissTour(page: Page) {
  const skip = page.getByRole('button', { name: /Saltar tour|Skip tour/i });
  if (await skip.count()) await skip.first().click({ timeout: 500 }).catch(() => {});
  await page.waitForTimeout(300);
}
function save(state: any) { fs.writeFileSync(STATE_PATH, JSON.stringify(state, null, 2)); }

test.describe('Phase E — Bundle round-trip', () => {
  test.setTimeout(180_000);

  test('export bundle → inspect JSON → re-import → verify toast', async ({ page }) => {
    fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
    const state: any = { steps: [] };

    // Register listeners BEFORE navigation
    const consoleMsgs: string[] = [];
    page.on('console', m => consoleMsgs.push(`[${m.type()}] ${m.text()}`));
    page.on('pageerror', e => consoleMsgs.push(`[pageerror] ${e.message}`));
    const wsFrames: string[] = [];
    page.on('websocket', ws => {
      wsFrames.push(`[ws-open] ${ws.url()}`);
      ws.on('framesent', f => {
        const s = typeof f.payload === 'string' ? f.payload : (f.payload?.toString('utf-8') ?? '<binary>');
        wsFrames.push(`[ws-tx ${s.length}b] ${s.slice(0, 300)}`);
      });
      ws.on('framereceived', f => {
        const s = typeof f.payload === 'string' ? f.payload : (f.payload?.toString('utf-8') ?? '<binary>');
        wsFrames.push(`[ws-rx ${s.length}b] ${s.slice(0, 300)}`);
      });
    });
    const blazorHttp: string[] = [];
    page.on('request', r => {
      const u = r.url();
      if (u.includes('_blazor') || u.includes('/negotiate') || u.includes('_framework/blazor')) {
        blazorHttp.push(`[req] ${r.method()} ${u.replace('http://localhost:5297', '')}`);
      }
    });
    page.on('response', r => {
      const u = r.url();
      if (u.includes('_blazor') || u.includes('/negotiate') || u.includes('_framework/blazor')) {
        blazorHttp.push(`[resp] ${r.status()} ${u.replace('http://localhost:5297', '')}`);
      }
    });

    // ------------- 1) Settings page ---------------
    await shortGoto(page, '/settings');
    await dismissTour(page);
    // Force hydration by clicking somewhere non-destructive first
    await page.waitForTimeout(500);
    // Wait for Blazor SignalR to connect. Blazor Web App exposes Blazor.reconnect
    await page.waitForFunction(() => {
      const w = window as any;
      return w.Blazor && (w.Blazor._internal?.NavigationManager || w.Blazor.reconnect);
    }, { timeout: 20_000 }).catch(() => {});
    // Blazor Server: wait for circuit to be Ready
    await page.waitForFunction(() => {
      const w = window as any;
      return typeof w.Blazor !== 'undefined'
          && w.Blazor?._internal
          && !document.body.classList.contains('blazor-loading');
    }, { timeout: 15_000 }).catch(() => { /* ignore, may be inline SSR only */ });
    await page.waitForTimeout(3000);
    state.blazorReady = await page.evaluate(() => typeof (window as any).Blazor !== 'undefined');
    save(state);

    // Locate the Bundle card by its title text
    const bundleCardHeading = page.locator('h2', { hasText: /Paquetes de proyecto|Project bundles/i }).first();
    await expect(bundleCardHeading).toBeVisible({ timeout: 8000 });
    state.steps.push('bundle-card-visible');
    save(state);

    // The card article
    const bundleCard = bundleCardHeading.locator('xpath=ancestor::article[contains(@class,"settings-card")]').first();
    const projectSelect = bundleCard.locator('select').first();
    await expect(projectSelect).toBeVisible();

    // Enumerate options in the select
    const options = await projectSelect.locator('option').evaluateAll(nodes =>
      nodes.map(n => ({ value: (n as HTMLOptionElement).value, label: (n as HTMLOptionElement).textContent?.trim() ?? '' }))
    );
    state.selectOptions = options;
    save(state);

    // Pick the first project whose value != "0"
    const firstProject = options.find(o => o.value !== '0');
    if (!firstProject) {
      state.error = 'no projects in dropdown';
      save(state);
      throw new Error('No projects to export');
    }
    state.pickedProject = firstProject;
    save(state);

    // Select it
    await projectSelect.selectOption({ value: firstProject.value });
    await page.waitForTimeout(500);

    // Enumerate every button in the card for diagnostics
    const cardButtons = await bundleCard.locator('button').evaluateAll(nodes =>
      nodes.map(n => ({
        text: (n.textContent ?? '').trim().slice(0, 60),
        disabled: (n as HTMLButtonElement).disabled,
        outerClasses: (n as HTMLElement).className,
        hasBlazorHandler: !!(n as any).__internalId || !!(n as any)._blazorInternalId,
        clickBinding: n.getAttribute('_bl_') ?? 'n/a',
      }))
    );
    state.cardButtons = cardButtons;
    save(state);

    state.bundleCardHtml = await bundleCard.evaluate(n => (n as HTMLElement).outerHTML.slice(0, 3000));
    save(state);

    // Find Export button by label text "Exportar" (full match)
    const exportButton = bundleCard.locator('button', { hasText: /Exportar/i }).first();
    await expect(exportButton).toBeEnabled({ timeout: 4000 });
    const exportLabel = await exportButton.innerText();
    state.exportButtonLabel = exportLabel;
    save(state);

    // Sanity: click a benign button to verify Blazor interactive circuit is
    // actually wired. Use the theme toggle in the top bar (kmila-btn-icon).
    const themeToggle = page.locator('button[aria-label*="theme" i], button[title*="Cambiar tema"], button[title*="Toggle theme" i]').first();
    if (await themeToggle.count()) {
      const beforeTheme = await page.evaluate(() => document.documentElement.getAttribute('data-bs-theme'));
      await themeToggle.click().catch(() => {});
      await page.waitForTimeout(1200);
      const afterTheme = await page.evaluate(() => document.documentElement.getAttribute('data-bs-theme'));
      state.themeToggleWorks = beforeTheme !== afterTheme;
      state.themeBefore = beforeTheme;
      state.themeAfter = afterTheme;
      save(state);
    } else {
      state.themeToggleWorks = 'button not found';
      save(state);
    }

    // Also probe if kmilaDownloadText is defined
    const kmilaDlDefined = await page.evaluate(() => typeof (window as any).kmilaDownloadText === 'function');
    state.kmilaDownloadTextDefined = kmilaDlDefined;
    save(state);

    // Intercept the JS interop to prove the button click actually reaches JS
    await page.evaluate(() => {
      const w = window as any;
      const orig = w.kmilaDownloadText;
      w.__kmilaDownloadCalls = [] as Array<{ fileName: string; mime: string; len: number }>;
      w.kmilaDownloadText = function (fileName: string, mime: string, text: string) {
        w.__kmilaDownloadCalls.push({ fileName, mime, len: (text ?? '').length });
        return orig(fileName, mime, text);
      };
    });

    // Re-select the project a moment before clicking, and let Blazor re-render.
    // The `_exportProjectId <= 0` disabled binding requires the change to have
    // committed server-side.
    await projectSelect.selectOption({ value: firstProject.value });
    await projectSelect.dispatchEvent('change');
    await page.waitForTimeout(1200);
    state.exportButtonDisabledBeforeClick = await exportButton.isDisabled();
    save(state);

    // ------------- 2) Click Export + catch download ---------------
    const downloadPromise = page.waitForEvent('download', { timeout: 30_000 }).catch(() => null);
    // Blazor Web attaches its handler via global listener on document. Fire a
    // real MouseEvent so bubbling reaches document-level Blazor handler.
    await exportButton.evaluate((btn: HTMLButtonElement) => {
      btn.focus();
      btn.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));
    });
    await page.waitForTimeout(4000); // give Blazor round-trip + JS blob time
    const download = await downloadPromise;

    const interopCalls = await page.evaluate(() => (window as any).__kmilaDownloadCalls ?? []);
    state.interopCalls = interopCalls;
    state.consoleTail = consoleMsgs.slice(-40);
    state.wsFramesCount = wsFrames.length;
    state.wsFramesTail = wsFrames.slice(-40);
    state.blazorHttpTail = blazorHttp.slice(-40);
    save(state);

    // Capture any toast that might have popped (error path)
    const toastsPostClick = await page.locator('.toast, .kmila-toast, [class*="toast"]').allInnerTexts();
    state.postClickToasts = toastsPostClick.filter(t => t && t.trim().length > 0);
    // Additional: dump toast-host inner HTML
    state.toastHostHtml = await page.evaluate(() =>
      Array.from(document.querySelectorAll('.kmila-toast-host, .kmila-toast, .toast'))
        .map(n => (n as HTMLElement).outerHTML.slice(0, 400))
    );
    save(state);

    if (!download) {
      state.exportResult = { captured: false, note: 'no download event in 15s' };
      save(state);
      throw new Error('bundle export did not fire download event');
    }
    const suggested = download.suggestedFilename();
    const bundlePath = path.join(EVIDENCE_DIR, `EVID_E_bundle_${suggested}`);
    await download.saveAs(bundlePath);
    const stat = fs.statSync(bundlePath);
    state.exportResult = { captured: true, filename: suggested, savedTo: bundlePath, sizeBytes: stat.size };
    save(state);

    // ------------- 3) Parse JSON + verify schema ---------------
    const raw = fs.readFileSync(bundlePath, 'utf-8');
    let parsed: any = null;
    let parseError: string | null = null;
    try { parsed = JSON.parse(raw); } catch (e: any) { parseError = e.message; }
    if (!parsed) {
      state.parse = { ok: false, error: parseError };
      save(state);
      throw new Error('bundle JSON parse failed: ' + parseError);
    }
    state.parse = {
      ok: true,
      keys: Object.keys(parsed),
      schemaVersion: parsed.schemaVersion ?? parsed.SchemaVersion ?? null,
      exportedAt: parsed.exportedAt ?? parsed.ExportedAt ?? null,
      projectName: parsed.project?.name ?? parsed.Project?.Name ?? null,
      fileCount: (parsed.files ?? parsed.Files ?? []).length,
      fileNames: (parsed.files ?? parsed.Files ?? []).map((f: any) => f.fileName ?? f.FileName),
      configCount: (parsed.configurations ?? parsed.Configurations ?? []).length,
      snapshotCount: (parsed.snapshots ?? parsed.Snapshots ?? []).length,
      sampleFilePreview: ((parsed.files ?? parsed.Files ?? [])[0]?.content ?? (parsed.files ?? parsed.Files ?? [])[0]?.Content ?? '').slice(0, 200),
    };
    save(state);

    // ------------- 4) Re-import via InputFile ---------------
    // Locate the hidden InputFile inside the bundle card
    const fileInput = bundleCard.locator('input[type="file"]').first();
    await fileInput.setInputFiles(bundlePath);
    await page.waitForTimeout(3500);

    // Look for the success toast text (SETTINGS_BUNDLE_IMPORTED "Paquete importado como \"{0}\".")
    // Toast may show localized text; capture toast container innerText.
    const toasts = await page.locator('.toast, .kmila-toast, [class*="toast"]').allInnerTexts();
    state.postImportToasts = toasts.filter(t => t && t.trim().length > 0);
    save(state);

    const importedRe = /Paquete importado|Imported bundle|La importación falló|Import failed/i;
    const importSuccess = state.postImportToasts.some((t: string) => /Paquete importado|Imported bundle/i.test(t));
    const importFailure = state.postImportToasts.some((t: string) => /La importación falló|Import failed/i.test(t));
    state.importResult = {
      matchedToast: state.postImportToasts.find((t: string) => importedRe.test(t)) ?? null,
      success: importSuccess,
      failure: importFailure,
    };
    save(state);

    // ------------- 5) Verify duplicate project in /projects ---------------
    await shortGoto(page, '/projects');
    await page.waitForTimeout(1500);
    const cardNames = await page.locator('[class*="project-card"], .project-card, article').evaluateAll(nodes =>
      nodes.map(n => (n.textContent ?? '').trim().slice(0, 120))
    );
    state.projectsAfterImport = cardNames.filter(Boolean).slice(0, 20);
    save(state);
    state.steps.push('done');
    save(state);
  });
});
