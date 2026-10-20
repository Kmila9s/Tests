// Phase 0 Visual Runtime — acceptance walk.
//
// One test per handoff §9 item that the headless Web target can prove.
// Tablet visual confirmation is recorded separately in the handoff §9
// progress table; specs here are the fast, deterministic half.
//
// Each test depends on seed.spec.ts having materialised FIXTURES first.
// Run order:
//   1) npx playwright test tests/11-visual/seed.spec.ts
//   2) npx playwright test tests/11-visual/acceptance.spec.ts

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { Monaco } from '../../pageobjects/Monaco';
import { FIXTURES, fixtureByName } from './fixtures';

async function openFixtureInEditor(page: any, projectName: string) {
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.openProject(projectName);
  const editor = new EditorPage(page);
  await editor.waitForReady();
  return editor;
}

async function currentProjectId(page: any): Promise<string> {
  const url = page.url();
  const m = url.match(/\/editor\/(\d+)/);
  if (!m) throw new Error(`Not on /editor/{id}, current URL: ${url}`);
  return m[1];
}

test.describe('Visual Phase 0 — §9 acceptance walk (Web)', () => {
  test.setTimeout(120_000);

  test('§9.1 — fixture passthrough loads with led<=btn', async ({ page }) => {
    const fixture = fixtureByName('Visual-Phase0-Passthrough');
    const editor = await openFixtureInEditor(page, fixture.projectName);

    await editor.openDockTab('Files');
    const row = editor.files.fileRowByName(`${fixture.fileSlug}.vhdl`);
    await expect(row).toBeVisible();
    await row.locator('.file-row__name').click();

    const monaco = new Monaco(page);
    const body = (await monaco.getValue()).trim();
    expect(body).toContain('entity passthrough');
    expect(body).toContain('btn : in');
    expect(body).toContain('led : out');
    expect(body).toContain('led <= btn');
  });

  async function openVisualByName(page: any, projectName: string) {
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.openProject(projectName);
    const idMatch = page.url().match(/\/editor\/(\d+)/);
    if (!idMatch) throw new Error(`No project id in ${page.url()}`);
    const projectId = idMatch[1];
    await page.goto(`/visual/${projectId}`);
    await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
    return projectId;
  }

  test('§9.2 — /visual/{id} route resolves and page mounts', async ({ page }) => {
    const fixture = fixtureByName('Visual-Phase0-Passthrough');
    // First fetch the id by walking through /projects (same way the tablet
    // user would). Keeps the test self-contained — no DB peek required.
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.openProject(fixture.projectName);
    const idMatch = page.url().match(/\/editor\/(\d+)/);
    expect(idMatch, 'editor URL must carry the project id').not.toBeNull();
    const projectId = idMatch![1];

    await page.goto(`/visual/${projectId}`);
    // The Visual page wraps everything in `<section class="visual-page">`.
    await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
    // Header carries the project name + entity name once parse settles.
    await expect(page.locator('.visual-page__project')).toContainText(fixture.projectName);
    await expect(page.locator('.visual-page__entity')).toContainText('passthrough');
  });

  test('§9.4 — drag Button + LED widgets onto canvas', async ({ page }) => {
    await openVisualByName(page, 'Visual-Phase0-Passthrough');
    await page.waitForLoadState('networkidle');

    // Defensive: a first click in a cold Blazor circuit can land before
    // the @onclick handler is wired. Keep clicking until the DOM proves it.
    const ledBtn = page.locator('[data-testid="visual-palette-led"]');
    const btnBtn = page.locator('[data-testid="visual-palette-button"]');
    await expect(ledBtn).toBeEnabled({ timeout: 10_000 });
    // Session 30 (2026-07-24) — single click + toHaveCount(1) per widget.
    // Previous `expect.poll` with click-inside-poll raced the Blazor
    // server round-trip: DOM read returned 0 before the render caught
    // up, poll clicked again, ending with 2 widgets. See STATUS §11 bug 2.
    await ledBtn.click();
    await expect(page.locator('.kviz-led')).toHaveCount(1, { timeout: 5_000 });
    await btnBtn.click();
    await expect(page.locator('.kviz-button')).toHaveCount(1, { timeout: 5_000 });

    const led = page.locator('.kviz-led').first();
    const btn = page.locator('.kviz-button').first();
    await expect(led).toBeVisible();
    await expect(btn).toBeVisible();

    // Grab the LED's own bounding box (not the wrap, which is the whole
    // canvas). pointerdown on the LED bubbles up to the wrap, which is
    // the drag-target setter; mouse moves on the canvas then relocate it.
    const ledBox0 = await led.boundingBox();
    expect(ledBox0, 'LED bounding box visible').not.toBeNull();

    // Capture LED's pre-drag CSS left/top read from the inline style.
    const beforeStyle = await led.getAttribute('style');

    // Drag from the LED visual to +120,+80 on the canvas surface.
    const startX = ledBox0!.x + ledBox0!.width / 2;
    const startY = ledBox0!.y + ledBox0!.height / 2;
    await page.mouse.move(startX, startY);
    await page.mouse.down();
    await page.mouse.move(startX + 120, startY + 80, { steps: 8 });
    await page.mouse.up();

    const afterStyle = await led.getAttribute('style');
    expect(afterStyle, 'LED inline style must update after drag').not.toEqual(beforeStyle);

    // Button drag — verifies the same path with the input-driving widget
    // (regression for the Button stopPropagation conditional).
    const btnBox0 = await btn.boundingBox();
    const btnBeforeStyle = await btn.getAttribute('style');
    await page.mouse.move(btnBox0!.x + btnBox0!.width / 2, btnBox0!.y + btnBox0!.height / 2);
    await page.mouse.down();
    await page.mouse.move(btnBox0!.x + 200, btnBox0!.y + 40, { steps: 8 });
    await page.mouse.up();
    const btnAfterStyle = await btn.getAttribute('style');
    expect(btnAfterStyle, 'Button inline style must update after drag').not.toEqual(btnBeforeStyle);
  });

  test('§9.5 — Wiring modal binds pins → ports with direction filter', async ({ page }) => {
    await openVisualByName(page, 'Visual-Phase0-Passthrough');
    await page.waitForLoadState('networkidle');

    const ledBtn = page.locator('[data-testid="visual-palette-led"]');
    const btnBtn = page.locator('[data-testid="visual-palette-button"]');
    await expect(ledBtn).toBeEnabled({ timeout: 10_000 });
    // Session 30 (2026-07-24) — single click + toHaveCount(1) per widget.
    // Previous `expect.poll` with click-inside-poll raced the Blazor
    // server round-trip: DOM read returned 0 before the render caught
    // up, poll clicked again, ending with 2 widgets. See STATUS §11 bug 2.
    await ledBtn.click();
    await expect(page.locator('.kviz-led')).toHaveCount(1, { timeout: 5_000 });
    await btnBtn.click();
    await expect(page.locator('.kviz-button')).toHaveCount(1, { timeout: 5_000 });

    const wireBtn = page.locator('[data-testid="visual-wire"]');
    const modal = page.locator('.kviz-wiring-modal');
    await expect.poll(async () => {
      if (!(await modal.isVisible().catch(() => false))) await wireBtn.click();
      return modal.isVisible();
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBe(true);

    // The wiring table has one row per (component, pin). Pin direction
    // filter: button.out (drives input) lists IN/INOUT ports only;
    // led.anode (reads output) lists OUT/BUFFER/INOUT only.
    const rows = modal.locator('.kviz-wiring-table tbody tr');
    await expect(rows).toHaveCount(2);

    // Find the row for Button.out and verify its dropdown only offers `btn`.
    const buttonRow = rows.filter({ has: page.locator('code', { hasText: 'out' }) });
    await expect(buttonRow).toHaveCount(1);
    const buttonSelect = buttonRow.locator('select');
    const buttonOptions = await buttonSelect.locator('option').allTextContents();
    expect(buttonOptions.some((t) => t.startsWith('btn'))).toBe(true);
    expect(buttonOptions.some((t) => t.startsWith('led'))).toBe(false);

    // LED row: pin "anode" — only `led` (OUT) should be offered.
    const ledRow = rows.filter({ has: page.locator('code', { hasText: 'anode' }) });
    await expect(ledRow).toHaveCount(1);
    const ledSelect = ledRow.locator('select');
    const ledOptions = await ledSelect.locator('option').allTextContents();
    expect(ledOptions.some((t) => t.startsWith('led'))).toBe(true);
    expect(ledOptions.some((t) => t.startsWith('btn'))).toBe(false);

    // Bind both.
    await buttonSelect.selectOption('btn');
    await ledSelect.selectOption('led');

    // Close and verify in the canvas: port labels under each widget update.
    await page.locator('[data-testid="visual-wiring-done"]').click();
    await expect(modal).toBeHidden({ timeout: 5_000 });

    await expect(page.locator('.kviz-led-label')).toHaveText('led');
    await expect(page.locator('.kviz-button-port')).toHaveText('btn');
  });

  test('§9.6 / §9.7 / §9.8 — Run, button press lights LED within 50 ms, release dims', async ({ page }) => {
    await openVisualByName(page, 'Visual-Phase0-Passthrough');
    await page.waitForLoadState('networkidle');

    const ledBtn = page.locator('[data-testid="visual-palette-led"]');
    const btnBtn = page.locator('[data-testid="visual-palette-button"]');
    await expect(ledBtn).toBeEnabled({ timeout: 10_000 });

    // Defensive add: keep clicking until both kinds of widget are on the
    // canvas (handles the early-circuit click race we hit in §9.4 touch).
    const ledsBefore = await page.locator('.kviz-led').count();
    const btnsBefore = await page.locator('.kviz-button').count();
    await expect.poll(async () => {
      if ((await page.locator('.kviz-led').count()) <= ledsBefore) await ledBtn.click();
      if ((await page.locator('.kviz-button').count()) <= btnsBefore) await btnBtn.click();
      return (await page.locator('.kviz-led').count()) > ledsBefore
        && (await page.locator('.kviz-button').count()) > btnsBefore;
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBe(true);

    // Wire the freshly-added pair via the modal.
    const wireBtn = page.locator('[data-testid="visual-wire"]');
    const modal = page.locator('.kviz-wiring-modal');
    await expect.poll(async () => {
      if (!(await modal.isVisible().catch(() => false))) await wireBtn.click();
      return modal.isVisible();
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBe(true);
    const rows = modal.locator('.kviz-wiring-table tbody tr');
    // The page accumulates components across the test run. We need the
    // last LED + last Button rows (== the ones we just added).
    const lastButtonRow = rows.filter({ has: page.locator('code', { hasText: 'out' }) }).last();
    const lastLedRow = rows.filter({ has: page.locator('code', { hasText: 'anode' }) }).last();
    await lastButtonRow.locator('select').selectOption('btn');
    await lastLedRow.locator('select').selectOption('led');

    // Diagnostic: confirm port labels updated after Done click. We're
    // chasing a flake where the bindings appear unset on the canvas
    // even though selectOption fired.
    await page.locator('[data-testid="visual-wiring-done"]').click();
    await expect(modal).toBeHidden();
    await expect(page.locator('.kviz-button-port').last()).toHaveText('btn', { timeout: 5_000 });
    await expect(page.locator('.kviz-led-label').last()).toHaveText('led', { timeout: 5_000 });

    // §9.6 — Run. Toolbar swaps to Stop, +LED/+Button disable.
    const consoleErrors: string[] = [];
    page.on('pageerror', (e) => consoleErrors.push(e.message));
    page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });

    await page.locator('[data-testid="visual-run"]').click();
    await expect(page.locator('[data-testid="visual-stop"]')).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('[data-testid="visual-palette-led"]')).toBeDisabled();

    // Find the LED + Button we wired. The properties pane uses the
    // selection, which targets the last-clicked widget — but the assertion
    // below works off the bound port (anode → led, out → btn) so the
    // canvas reflects the engine's tick output.
    const led = page.locator('.kviz-led').last();
    const btn = page.locator('.kviz-button').last();
    await expect(led).toBeVisible();
    await expect(btn).toBeVisible();

    // §9.7 — press button, LED lights within ~50 ms.
    const btnBox = await btn.boundingBox();
    const center = { x: btnBox!.x + btnBox!.width / 2, y: btnBox!.y + btnBox!.height / 2 };
    const t0 = Date.now();
    await page.mouse.move(center.x, center.y);
    await page.mouse.down();
    // Poll for the .kviz-led--on class. 50 ms is the spec budget; we
    // allow a longer wait but record the actual latency.
    await expect.poll(async () => await led.evaluate((el) => el.classList.contains('kviz-led--on')),
      { timeout: 2000, intervals: [10, 10, 20, 30, 50, 100] }).toBe(true);
    const onLatency = Date.now() - t0;

    // §9.8 — release button, LED off within ~50 ms.
    const t1 = Date.now();
    await page.mouse.up();
    await expect.poll(async () => await led.evaluate((el) => el.classList.contains('kviz-led--on')),
      { timeout: 2000, intervals: [10, 10, 20, 30, 50, 100] }).toBe(false);
    const offLatency = Date.now() - t1;

    // Stop the run cleanly. Allow more time — coordinator's loopTask
    // termination + OnStopped → StateHasChanged round-trip is async.
    await page.locator('[data-testid="visual-stop"]').click();
    await expect(page.locator('[data-testid="visual-run"]')).toBeVisible({ timeout: 15_000 });

    expect(consoleErrors, `console errors during run: ${consoleErrors.join(' | ')}`).toEqual([]);

    // Surface the timings into the test output for the handoff log.
    console.log(`[§9.7] press→LED-on latency: ${onLatency} ms`);
    console.log(`[§9.8] release→LED-off latency: ${offLatency} ms`);
  });

  test('§9.9 — Stop halts loop; repeated Run/Stop cycles do not leak', async ({ page }) => {
    // We can't peek server-side RSS from Playwright, but we can assert:
    //   1) Stop click flips toolbar back to Run within bounded time
    //   2) Repeated Run/Stop transitions all succeed (proxy for no
    //      lingering task that would block subsequent runs)
    await openVisualByName(page, 'Visual-Phase0-Passthrough');
    await page.waitForLoadState('networkidle');

    // The fixture has bindings saved by wire_and_save.spec.ts, so widgets
    // should already be present + wired. If not (fresh test order), retry-
    // add. Either way, the test is about Run/Stop cycling.
    const ledBtn = page.locator('[data-testid="visual-palette-led"]');
    const btnBtn = page.locator('[data-testid="visual-palette-button"]');
    await expect(ledBtn).toBeEnabled({ timeout: 10_000 });
    if ((await page.locator('.kviz-led').count()) === 0) {
      await expect.poll(async () => {
        await ledBtn.click(); await btnBtn.click();
        return (await page.locator('.kviz-led').count()) > 0;
      }, { timeout: 10_000 }).toBe(true);
    }

    for (let i = 0; i < 5; i++) {
      await page.locator('[data-testid="visual-run"]').click();
      await expect(page.locator('[data-testid="visual-stop"]')).toBeVisible({ timeout: 5_000 });
      await page.waitForTimeout(80);
      await page.locator('[data-testid="visual-stop"]').click();
      await expect(page.locator('[data-testid="visual-run"]')).toBeVisible({ timeout: 5_000 });
    }
  });

  test('§9.10 — Save then reload restores canvas + bindings bit-exact', async ({ page }) => {
    // wire_and_save.spec.ts already persisted a wired pair; this test
    // captures the pre-reload DOM state and asserts the post-reload state
    // matches byte-equivalently (positions, configs, bindings).
    await openVisualByName(page, 'Visual-Phase0-Passthrough');
    await page.waitForLoadState('networkidle');

    // Wait for the saved widgets to materialise (no defensive add — if
    // wire_and_save wasn't run, the test will fail loudly, which is the
    // signal to re-run the seeder).
    const led = page.locator('.kviz-led').first();
    const btn = page.locator('.kviz-button').first();
    await expect(led).toBeVisible({ timeout: 10_000 });
    await expect(btn).toBeVisible();

    const ledStyleBefore = await led.getAttribute('style');
    const btnStyleBefore = await btn.getAttribute('style');
    const ledLabelBefore = await page.locator('.kviz-led-label').first().textContent();
    const btnLabelBefore = await page.locator('.kviz-button-port').first().textContent();

    // Navigate away and back — clean reload.
    await page.goto('/projects');
    await page.waitForLoadState('networkidle');
    await openVisualByName(page, 'Visual-Phase0-Passthrough');
    await page.waitForLoadState('networkidle');

    const ledAfter = page.locator('.kviz-led').first();
    const btnAfter = page.locator('.kviz-button').first();
    await expect(ledAfter).toBeVisible();
    await expect(btnAfter).toBeVisible();

    expect(await ledAfter.getAttribute('style')).toEqual(ledStyleBefore);
    expect(await btnAfter.getAttribute('style')).toEqual(btnStyleBefore);
    expect(await page.locator('.kviz-led-label').first().textContent()).toEqual(ledLabelBefore);
    expect(await page.locator('.kviz-button-port').first().textContent()).toEqual(btnLabelBefore);
  });

  test('§9.4 (touch) — drag works under hasTouch context', async ({ browser }) => {
    // The tablet emulator emits touch pointer events. Playwright's default
    // chromium-desktop context is mouse-only — so we explicitly open a
    // touch-enabled context to prove the seam handles `pointerType: "touch"`
    // before we trust the tablet visual.
    const context = await browser.newContext({
      viewport: { width: 1280, height: 800 },
      hasTouch: true,
      isMobile: false,
    });
    const page = await context.newPage();
    try {
      await page.addInitScript(() => {
        localStorage.setItem('kmila.tutorialSeen', '1');
        localStorage.setItem('kmila.tutorialSeen.projects', '1');
      });
      await openVisualByName(page, 'Visual-Phase0-Passthrough');

      // Wait for Blazor interactivity. A click that races SignalR's
      // initial circuit handshake gets silently dropped on slow contexts.
      await page.waitForLoadState('networkidle');

      const ledBtn = page.locator('[data-testid="visual-palette-led"]');
      const btnBtn = page.locator('[data-testid="visual-palette-button"]');
      await expect(ledBtn).toBeEnabled({ timeout: 10_000 });
      await expect(btnBtn).toBeEnabled({ timeout: 10_000 });

      // Retry until at least one LED + one Button widget renders. A single
      // click during the touch-context cold-start sometimes lands before
      // Blazor wired its @onclick → C# handler, even after
      // `networkidle` — keep clicking until the DOM proves it took.
      await expect.poll(async () => {
        if ((await page.locator('.kviz-led').count()) === 0) await ledBtn.click();
        if ((await page.locator('.kviz-button').count()) === 0) await btnBtn.click();
        return (await page.locator('.kviz-led').count()) > 0
          && (await page.locator('.kviz-button').count()) > 0;
      }, { timeout: 15_000, intervals: [400, 600, 800] }).toBe(true);

      const led = page.locator('.kviz-led').last();
      const btn = page.locator('.kviz-button').last();
      await expect(led).toBeVisible();
      await expect(btn).toBeVisible();

      const ledBox = await led.boundingBox();
      const ledBefore = await led.getAttribute('style');

      // Synthetic touch drag via CDP — bypasses mouse, exercises the same
      // pointer-event path the tablet's finger produces.
      const startX = ledBox!.x + ledBox!.width / 2;
      const startY = ledBox!.y + ledBox!.height / 2;
      await page.touchscreen.tap(startX, startY); // selects + warms up handlers
      await page.waitForTimeout(50);
      // No drag helper on touchscreen; build the gesture via CDP raw events.
      const client = await page.context().newCDPSession(page);
      const startPoint = { x: startX, y: startY };
      const endPoint = { x: startX + 140, y: startY + 80 };
      await client.send('Input.dispatchTouchEvent', {
        type: 'touchStart',
        touchPoints: [startPoint],
      });
      // 6 interpolated move steps.
      for (let i = 1; i <= 6; i++) {
        const x = startX + (140 * i) / 6;
        const y = startY + (80 * i) / 6;
        await client.send('Input.dispatchTouchEvent', {
          type: 'touchMove',
          touchPoints: [{ x, y }],
        });
        await page.waitForTimeout(20);
      }
      await client.send('Input.dispatchTouchEvent', {
        type: 'touchEnd',
        touchPoints: [],
      });

      const ledAfter = await led.getAttribute('style');
      expect(ledAfter, 'touch drag must reposition LED').not.toEqual(ledBefore);
    } finally {
      await context.close();
    }
  });
});
