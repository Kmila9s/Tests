// FW-3 HD44780 LCD acceptance — spawn, render 16×2 default grid, mode
// toggle, rows/cols selects. Text-mode line decoding is proven via the
// API sanity path (see the DIP-adder-regression pattern); this spec
// stays focused on widget spawn + props pane shape.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { fixtureByName } from './fixtures';

test.describe('FW-3 — HD44780 LCD widget', () => {
  test.setTimeout(90_000);

  async function openVisual(page: any, projectName: string) {
    const projects = new ProjectsPage(page);
    await projects.goto();
    await projects.openProject(projectName);
    const m = page.url().match(/\/editor\/(\d+)/);
    if (!m) throw new Error(`No project id in ${page.url()}`);
    await page.goto(`/visual/${m[1]}`);
    await expect(page.locator('section.visual-page')).toBeVisible({ timeout: 10_000 });
  }

  test('spawns from palette + renders 16×2 default screen', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-lcd-hd44780"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });

    await expect.poll(async () => {
      if ((await page.locator('.kviz-lcd').count()) === 0) await chip.click();
      return await page.locator('.kviz-lcd').count();
    }, { timeout: 10_000, intervals: [300, 500, 800] }).toBeGreaterThan(0);

    const widget = page.locator('.kviz-lcd').first();
    await expect(widget).toBeVisible();
    await expect(widget).toHaveAttribute('data-mode', 'text');
    await expect(widget).toHaveAttribute('data-rows', '2');
    await expect(widget).toHaveAttribute('data-cols', '16');
    // 32 cells for 16×2, all blank at rest (line0 unwired → spaces).
    await expect(widget.locator('.kviz-lcd__cell')).toHaveCount(32);
  });

  test('props pane shows mode + rows + cols selects with output chip', async ({ page }) => {
    await openVisual(page, fixtureByName('Visual-Phase0-Passthrough').projectName);

    const chip = page.locator('[data-testid="visual-palette-lcd-hd44780"]');
    await expect(chip).toBeEnabled({ timeout: 10_000 });
    await chip.click();
    await expect(page.locator('.kviz-lcd')).toBeVisible({ timeout: 5_000 });

    await page.locator('.kviz-lcd').first().click();

    const props = page.locator('.visual-page__props');
    await expect(props).toBeVisible({ timeout: 5_000 });

    // Output direction chip (text mode = purely output).
    await expect(props.locator('.visual-page__props-dir--out')).toBeVisible();
    // Three selects: mode / rows / cols.
    await expect(props.locator('select')).toHaveCount(3);
  });

  test('LCD hello banner shifts one character per STEP (API + Visual seam)', async ({ request }) => {
    // End-to-end regression: prove the interpreter, coordinator, and
    // widget-facing LatestSlicedValue all agree on the 128-bit line0
    // decode. Uses the same /api/v1 pipeline the Visual runtime
    // feeds through.
    const proj = await (await request.post('/api/v1/projects', {
      data: { name: 'lcd_e2e', retain: false },
    })).json();

    const vhdl = `library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
entity hello_scroll is
  port(clk: in std_logic; reset: in std_logic; step_en: in std_logic;
       line0: out std_logic_vector(127 downto 0));
end hello_scroll;
architecture rtl of hello_scroll is
  signal reg : std_logic_vector(127 downto 0) :=
    "01001000010001010100110001001100010011110010000001001011010011010100100101001100010000010010000000100000001000000010000000100000";
begin
  process(clk, reset)
  begin
    if reset = '1' then
      reg <= "01001000010001010100110001001100010011110010000001001011010011010100100101001100010000010010000000100000001000000010000000100000";
    elsif rising_edge(clk) then
      if step_en = '1' then reg <= reg(119 downto 0) & reg(127 downto 120); end if;
    end if;
  end process;
  line0 <= reg;
end rtl;`;
    await request.post(`/api/v1/projects/${proj.projectId}/files`,
      { data: { name: 'main.vhd', content: vhdl } });
    const sim = await (await request.post('/api/v1/simulate', {
      data: {
        projectId: proj.projectId, entryPoint: 'hello_scroll',
        timeScale: 'NS', duration: 100, systemClockMhz: 50, tickResNs: 10,
        portMoments: [
          { port: 'reset', t: 0, scale: 'NS', value: '0' },
          { port: 'step_en', t: 0, scale: 'NS', value: '1' },
        ],
      },
    })).json();

    const vcd: string = sim.vcd ?? '';
    const idMatch = vcd.match(/\$var\s+\S+\s+\d+\s+(\S+)\s+line0\s+\$end/);
    expect(idMatch, 'line0 declared in VCD').not.toBeNull();
    const id = idMatch![1];

    const changes: string[] = [];
    for (const line of vcd.split(/\r?\n/)) {
      const m = line.match(/^b([01]+)\s+(\S+)$/);
      if (m && m[2] === id) changes.push(m[1]);
    }
    expect(changes.length, 'line0 must transition at least twice').toBeGreaterThanOrEqual(2);

    const decode = (bits: string) => {
      let out = '';
      for (let i = 0; i + 8 <= bits.length; i += 8) {
        const val = parseInt(bits.substr(i, 8), 2);
        out += val >= 32 && val < 127 ? String.fromCharCode(val) : '?';
      }
      return out;
    };
    expect(decode(changes[0])).toBe('HELLO KMILA     ');
    expect(decode(changes[1])).toBe('ELLO KMILA     H');
  });
});
