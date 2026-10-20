import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';
import { EditorPage } from '../../pageobjects/EditorPage';
import { Monaco } from '../../pageobjects/Monaco';
import { RunBar } from '../../pageobjects/RunBar';

const COUNTER_VHDL = `library ieee;
use ieee.std_logic_1164.all;

entity sim_lab is
  port (
    clk    : in  std_logic;
    reset  : in  std_logic;
    q      : out std_logic
  );
end entity sim_lab;

architecture behavioral of sim_lab is
  signal q_int : std_logic := '0';
begin
  process(clk, reset)
  begin
    if reset = '1' then
      q_int <= '0';
    elsif rising_edge(clk) then
      q_int <= not q_int;
    end if;
  end process;
  q <= q_int;
end architecture behavioral;
`;

test.use({
  storageState: {
    cookies: [],
    origins: [{
      origin: 'http://localhost:5297',
      localStorage: [
        { name: 'kmila.tutorialSeen',          value: '1' },
        { name: 'kmila.tutorialSeen.projects', value: '1' },
        { name: 'kmila.tutorialSeen.editor',   value: '1' },
      ],
    }],
  },
});

test('breakpoint pause produces highlight', async ({ page }) => {
  test.setTimeout(180_000);

  const errs: string[] = [];
  page.on('console', m => {
    if (m.type() === 'error' || m.type() === 'warning' || m.type() === 'log') {
      errs.push(`[${m.type()}] ${m.text()}`);
    }
  });
  page.on('pageerror', e => errs.push(`[pageerror] ${e.message}`));

  const projectName = `hl-${Date.now()}`;
  const projects = new ProjectsPage(page);
  await projects.goto();
  await projects.createProject(projectName, 'highlight check');
  await projects.openProject(projectName);

  const editor = new EditorPage(page);
  await editor.waitForReady();

  const runbar = new RunBar(page);
  await runbar.pickFirstFpga();

  await editor.openDockTab('Files');
  await editor.files.createFile('counter', 'vhdl');
  await editor.files.fileRowByName('counter.vhdl').locator('.file-row__name').click();

  const monaco = new Monaco(page);
  await monaco.setValue(COUNTER_VHDL);

  await runbar.save();
  await editor.files.toggleEntryPoint('counter.vhdl');

  // Set breakpoint by dispatching mousedown on Monaco's gutter for line 20.
  await page.waitForTimeout(1500);
  const bpSet = await page.evaluate(() => {
    const editors = (window as any).monaco?.editor?.getEditors?.() ?? [];
    const ed = editors.find((e: any) => {
      const d = e.getDomNode && e.getDomNode();
      return d && d.isConnected && d.closest && d.closest('.editor-monaco');
    });
    if (!ed) return 'no editor';
    // Use Monaco's API to fake a mousedown via revealLine then dispatch event on line-numbers DOM.
    const targetLine = 20;
    ed.revealLine(targetLine);
    const dom = ed.getDomNode();
    const lineNoEls = Array.from(dom.querySelectorAll('.margin-view-overlays .line-numbers'));
    const seen: string[] = [];
    for (const el of lineNoEls) {
      const txt = el.textContent ? el.textContent.trim() : '';
      seen.push(txt);
      if (txt === String(targetLine)) {
        const rect = (el as HTMLElement).getBoundingClientRect();
        const e = new MouseEvent('mousedown', { bubbles: true, clientX: rect.left + 2, clientY: rect.top + 2, button: 0 });
        el.dispatchEvent(e);
        return 'ok';
      }
    }
    return 'no line 20; seen=' + seen.join(',');
  });
  console.log('bp set result:', bpSet);
  await page.waitForTimeout(500);
  const bpInfo = await page.evaluate(() => {
    const editors = (window as any).monaco?.editor?.getEditors?.() ?? [];
    const ed = editors.find((e: any) => {
      const d = e.getDomNode && e.getDomNode();
      return d && d.isConnected && d.closest && d.closest('.editor-monaco');
    });
    if (!ed) return { error: 'no editor' };
    const decos = (ed.getModel().getAllDecorations() || []).map((d: any) => ({
      line: d.range?.startLineNumber,
      glyph: d.options?.glyphMarginClassName,
      cls: d.options?.className,
    })).filter((x: any) => x.glyph || x.cls);
    return { decos };
  });
  console.log('BEFORE RUN decos:', JSON.stringify(bpInfo));

  await runbar.runBtn.click();

  // Wait for breakpoint pause
  await page.waitForTimeout(12_000);

  const after = await page.evaluate(() => {
    const editors = (window as any).monaco?.editor?.getEditors?.() ?? [];
    const ed = editors.find((e: any) => {
      const d = e.getDomNode && e.getDomNode();
      return d && d.isConnected && d.closest && d.closest('.editor-monaco');
    });
    if (!ed) return { error: 'no editor' };
    const model = ed.getModel();
    const decos = (model.getAllDecorations() || []).map((d: any) => ({
      line: d.range?.startLineNumber,
      cls:  d.options?.className,
      glyph: d.options?.glyphMarginClassName,
      whole: d.options?.isWholeLine,
    }));
    return { lineCount: model.getLineCount(), decos };
  });
  console.log('AFTER RUN editor decos:', JSON.stringify(after, null, 2));

  const schem = await page.evaluate(() => {
    const cells = Array.from(document.querySelectorAll('.schematic-cell.is-active-step')).length;
    const wires = Array.from(document.querySelectorAll('.schematic-wire.is-active-step')).length;
    const allCells = document.querySelectorAll('.schematic-cell').length;
    const allWires = document.querySelectorAll('.schematic-wire').length;
    return { activeCells: cells, activeWires: wires, allCells, allWires };
  });
  console.log('Schematic state:', JSON.stringify(schem));

  console.log('Console errs:', JSON.stringify(errs.slice(-30)));

  await page.screenshot({ path: '/tmp/highlight-after-pause.png', fullPage: true });
});
