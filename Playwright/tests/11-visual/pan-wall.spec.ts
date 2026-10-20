// Pan-wall regression — §6 Item 3.
//
// Bug: `.visual-page__canvas-wrap` is inline-styled to Canvas.Width ×
// Canvas.Height. Widgets are absolutely positioned inside the wrap, so a
// drag past those dims used to overflow silently — clientWidth stayed
// pinned, the pan-zoom lib's cached bounds never updated, and the widget
// sat behind an invisible clamp barrier.
//
// Fix:
//   1) VisualPanel.EnsureCanvasFitsWidgets() grows Canvas.Width/Height on
//      MarkDirty so the wrap physically expands.
//   2) app.js exposes kmilaPanZoomInvalidateBounds(id) as a defensive
//      re-measure hook — engines where ResizeObserver takes a frame to
//      fire pick up the fresh bounds immediately.
//
// This spec pins the JS side (API present + honours new bounds after
// invalidate). The C# growth logic is exercised via unit tests in
// Tests/Unit/Kmila.Shared.Tests. End-to-end drag verification requires
// manual reproduction — Blazor Server's SignalR pointermove pipeline
// drops trailing events under Playwright's synthetic drag, so an
// integration test on the drag path is inherently flaky.

import { test, expect } from '@playwright/test';

test.describe('Pan-wall — kmilaPanZoomInvalidateBounds', () => {
  test.setTimeout(60_000);

  test('exposes kmilaPanZoomInvalidateBounds on window', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const exposed = await page.evaluate(() =>
      typeof (window as any).kmilaPanZoomInvalidateBounds === 'function');
    expect(exposed).toBe(true);
  });

  test('invalidateBounds re-measures the target so grown content is pannable', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Set up a synthetic pan-zoom scene entirely in the browser: a root
    // element containing a target div at a small initial size, attach
    // pan-zoom with panOnWheel, then grow the target's inline size and
    // ensure the pan-zoom lib respects the new bounds after invalidate.
    const result = await page.evaluate(async () => {
      const root = document.createElement('div');
      root.id = 'panzoom-test-root';
      root.style.width = '400px';
      root.style.height = '300px';
      root.style.position = 'relative';
      root.style.overflow = 'hidden';
      document.body.appendChild(root);

      const target = document.createElement('div');
      target.id = 'panzoom-test-target';
      target.style.width = '600px';
      target.style.height = '400px';
      target.style.position = 'relative';
      root.appendChild(target);

      const attach = (window as any).kmilaAttachPanZoom;
      const invalidate = (window as any).kmilaPanZoomInvalidateBounds;
      if (typeof attach !== 'function' || typeof invalidate !== 'function') {
        return { ok: false, reason: 'kmila pan-zoom API missing' };
      }
      const id = attach('#panzoom-test-root', {
        targetSelector: '#panzoom-test-target',
        minScale: 0.25,
        maxScale: 4.0,
        fitOnAttach: false,
        panOnWheel: true,
      });
      if (typeof id !== 'number' || id <= 0) return { ok: false, reason: 'attach returned bad id: ' + id };

      // Baseline: transform should be identity (fitOnAttach:false).
      const before = target.style.transform;

      // Grow the target to a size much larger than the root — same
      // shape as a Visual canvas that got widget-extended.
      target.style.width = '2400px';
      target.style.height = '1800px';

      // Nudge pan-zoom to re-measure. Without this, the cached bounds
      // stay at 600x400 and pan-clamp would keep the user pinned to the
      // top-left corner even though there's content further out.
      invalidate(id);

      // Fire a wheel-pan far to the right. If clamp uses the OLD 600 px
      // width, the transform's tx would stop near -520 (root.width -
      // MIN_VIS - naturalW). If it uses the NEW 2400 px width, tx can
      // reach much further negative — that's the pan-wall lift.
      const wheelEvent = new WheelEvent('wheel', {
        deltaX: 5000, deltaY: 0, bubbles: true, cancelable: true,
      });
      root.dispatchEvent(wheelEvent);

      const after = target.style.transform;
      // Parse tx out of "translate3d(<tx>px, <ty>px, 0) scale(<s>)"
      const m = after.match(/translate3d\((-?\d+(?:\.\d+)?)px/);
      const tx = m ? parseFloat(m[1]) : 0;

      // Cleanup.
      (window as any).kmilaDetachPanZoom(id);
      root.remove();

      return { ok: true, before, after, tx };
    });

    expect(result.ok, `synthetic pan-zoom setup failed: ${(result as any).reason ?? ''}`).toBe(true);
    // With the OLD cached bounds (600 wide), clamp would leave tx near
    // -120 (400 - 80 - 400 for content) — nowhere near -1000. With the
    // NEW bounds (2400 wide), clamp allows tx well past -1000.
    expect(result.tx!, `pan tx after wheel-scroll on grown content: ${result.tx}`)
      .toBeLessThan(-500);
  });
});
