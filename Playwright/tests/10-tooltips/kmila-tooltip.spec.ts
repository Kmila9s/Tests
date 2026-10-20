import { test, expect } from '@playwright/test';

/**
 * KmilaTooltip smoke — ships alongside the universal-tooltip Phase 1/2/3
 * migration (2026-07-19). Asserts three things:
 *
 *   1. The wrapper span is rendered around a canonical button.
 *   2. The rendered tooltip text tracks the active language flip.
 *   3. Every wrapped instance keeps its `role="tooltip"` accessible-name
 *      sibling so screen readers surface the translation.
 *
 * The test intentionally uses the Projects page — it doesn't require a
 * project to exist, and it hosts one of the KmilaTooltip-wrapped buttons
 * ("Create new project") that Phase 2 shipped.
 */
test.describe('KmilaTooltip', () => {
  test('renders wrapper + role=tooltip sibling on wrapped buttons', async ({ page }) => {
    await page.goto('/projects');

    // At least one KmilaTooltip wrapper must be present. Empty projects
    // page always shows the "Create new" empty-state CTA (wrapped in
    // <KmilaTooltip TextKey="PROJECTS_CREATE_NEW_TIP">). Non-empty state
    // also renders the header CTA. Either way, the wrapper class must exist.
    const wrappers = page.locator('span.kmila-tooltip');
    await expect(wrappers.first()).toBeVisible();
    expect(await wrappers.count()).toBeGreaterThan(0);

    // Each wrapper renders a data-kmila-tip attribute + an offscreen
    // role="tooltip" sibling with the same string, so aria-describedby
    // consumers pick it up.
    const first = wrappers.first();
    const dataTip = await first.getAttribute('data-kmila-tip');
    expect(dataTip).toBeTruthy();
    expect(dataTip!.length).toBeGreaterThan(0);

    const srSibling = first.locator('span.kmila-tooltip__sr[role="tooltip"]');
    await expect(srSibling).toHaveText(dataTip!);
  });

  test('tooltip text retranslates on language flip', async ({ page }) => {
    await page.goto('/projects');

    // Capture EN text of the first wrapped button on the page.
    const wrapper = page.locator('span.kmila-tooltip').first();
    const enText = await wrapper.getAttribute('data-kmila-tip');
    expect(enText).toBeTruthy();

    // Flip to Spanish through the profile menu → language grid. The chip
    // labels use the two-letter language code ("ES", "EN", …).
    await page.click('[data-tour="topbar-profile"] button');
    await page.click('button.kmila-profile-menu__lang-chip:has-text("ES")');

    // Give the Traductor reload event a moment to fire and StateHasChanged
    // to repaint the KmilaTooltip's `data-kmila-tip`.
    await page.waitForTimeout(400);

    const esText = await wrapper.getAttribute('data-kmila-tip');
    expect(esText).toBeTruthy();
    expect(esText).not.toEqual(enText);
  });

  test('does not double-render a browser tooltip alongside the styled bubble', async ({ page }) => {
    await page.goto('/projects');

    // Phase 3 stripped `title=` from every wrapped button. The wrapper's
    // child <button> should therefore never carry a title attribute —
    // otherwise the OS-native tooltip would race the styled bubble on
    // hover.
    const wrappedButtons = page.locator('span.kmila-tooltip > button');
    const count = await wrappedButtons.count();
    for (let i = 0; i < count; i++) {
      const button = wrappedButtons.nth(i);
      const title = await button.getAttribute('title');
      expect(title, `wrapped button ${i} still carries a title= attribute`).toBeNull();
    }
  });
});
