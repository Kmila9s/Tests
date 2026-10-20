import { expect, Locator, Page } from '@playwright/test';

/**
 * RunBar — Kmila.Shared/Components/RunBar.razor.
 *
 * Build is disabled when NoFpga; the test must ensure an FPGA is picked
 * (via pickFirstFpga) before invoking build/run.
 */
export class RunBar {
  readonly root: Locator;
  readonly fpgaChip: Locator;
  readonly saveBtn: Locator;
  readonly buildBtn: Locator;
  readonly runBtn: Locator;
  readonly stopBtn: Locator;
  readonly timeInput: Locator;
  readonly clockInput: Locator;

  // FPGA picker (lives in ProfileMenu, opened by clicking the chip)
  readonly profileMenu: Locator;
  readonly fpgaPickerDetails: Locator;
  readonly fpgaOptions: Locator;

  constructor(readonly page: Page) {
    this.root = page.locator('.run-bar');
    this.fpgaChip = this.root.locator('button.run-bar__fpga');
    this.saveBtn = this.root.locator('button.run-bar__file-btn--save');
    this.buildBtn = this.root.locator('button.run-bar__build');
    this.runBtn = this.root.locator('button.run-bar__run');
    this.stopBtn = this.root.locator('button.run-bar__stop');
    this.timeInput = this.root.locator('.run-bar__time input.run-bar__time-input');
    this.clockInput = this.root.locator('.run-bar__clock input.run-bar__time-input');

    this.profileMenu = page.locator('details.kmila-profile-menu');
    this.fpgaPickerDetails = page.locator('details.kmila-fpga-picker');
    this.fpgaOptions = page.locator('button.kmila-fpga-picker__option');
  }

  async hasFpga(): Promise<boolean> {
    return !(await this.fpgaChip.evaluate((el) => el.classList.contains('run-bar__fpga--missing')));
  }

  /**
   * Ensure an FPGA is selected. If one is already picked, no-op.
   * Otherwise opens the picker and selects the first option.
   */
  async pickFirstFpga() {
    if (await this.hasFpga()) return;

    // Force both <details> open via JS — pure DOM mutation, sidesteps any
    // Blazor click-handler race with the inner option click.
    await this.page.evaluate(() => {
      const profile = document.querySelector('details.kmila-profile-menu');
      if (profile) (profile as HTMLDetailsElement).open = true;
      const picker =
        document.querySelector('details.kmila-profile-menu details.kmila-fpga-picker') ??
        document.querySelector('details.kmila-fpga-picker');
      if (picker) (picker as HTMLDetailsElement).open = true;
    });

    await expect(this.fpgaOptions.first()).toBeVisible({ timeout: 10_000 });
    await this.fpgaOptions.first().click();

    // The chip should lose its `--missing` modifier once SelectedFPGA.SetFPGA fires.
    await expect.poll(async () => this.hasFpga(), { timeout: 10_000 }).toBe(true);

    // Picking doesn't auto-close the <details> overlay; leaving it open covers
    // the dock area and intercepts subsequent tab clicks.
    await this.page.evaluate(() => {
      document
        .querySelectorAll<HTMLDetailsElement>('details.kmila-profile-menu, details.kmila-fpga-picker')
        .forEach((d) => (d.open = false));
    });
  }

  async build() {
    await expect(this.buildBtn).toBeEnabled({ timeout: 15_000 });
    await this.buildBtn.click();
  }

  /**
   * Wait until the build button is no longer spinning. The button text shows
   * SIM_BUILDING (with a `.spinner-border`) while IsExplicitBuilding is true.
   */
  async waitForBuildIdle(timeoutMs = 60_000) {
    await expect(this.buildBtn.locator('.spinner-border')).toHaveCount(0, { timeout: timeoutMs });
  }

  async save() {
    await this.saveBtn.click();
  }
}
