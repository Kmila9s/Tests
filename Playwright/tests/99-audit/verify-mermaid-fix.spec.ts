import { test, expect } from '@playwright/test';

const lessonPaths = [
  '/learn/vhdl-basics/01-anatomy',
  '/learn/fsm/01-mealy-vs-moore',
  '/learn/cpu/01-cpu-overview',
  '/learn/cpu/02-cpu-project',
];

test.setTimeout(60000);

for (const p of lessonPaths) {
  test(`mermaid renders on ${p}`, async ({ page }) => {
    const consoleMsgs: string[] = [];
    page.on('console', msg => {
      const t = msg.type();
      if (t === 'error' || t === 'warning') consoleMsgs.push(`[${t}] ${msg.text()}`);
    });
    page.on('pageerror', err => consoleMsgs.push(`[pageerror] ${err.message}`));
    const failed: string[] = [];
    page.on('response', resp => {
      if (resp.status() >= 400) failed.push(`${resp.status()} ${resp.url()}`);
    });

    await page.goto(p, { waitUntil: 'load' });
    await page.waitForSelector('pre.mermaid', { state: 'attached', timeout: 20000 });

    const deadline = Date.now() + 20000;
    let stats: any = null;
    while (Date.now() < deadline) {
      stats = await page.evaluate(() => {
        const nodes = Array.from(document.querySelectorAll('pre.mermaid'));
        return {
          total: nodes.length,
          withSvg: nodes.filter(n => n.querySelector('svg') !== null).length,
          withErrorText: nodes.filter(n => {
            const html = (n.innerHTML || '').toLowerCase();
            return html.includes('syntax error') && html.includes('mermaid');
          }).length,
        };
      });
      if (stats.total > 0 && stats.withSvg === stats.total && stats.withErrorText === 0) break;
      await page.waitForTimeout(500);
    }

    console.log(`  ${p} → nodes=${stats.total} withSvg=${stats.withSvg} withErrorText=${stats.withErrorText}`);
    if (failed.length) { console.log('  failed responses:'); failed.slice(0, 8).forEach(f => console.log('   ', f)); }
    if (consoleMsgs.length) { console.log('  console:'); consoleMsgs.slice(0, 8).forEach(e => console.log('   ', e)); }

    expect(stats.total).toBeGreaterThan(0);
    expect(stats.withErrorText).toBe(0);
    expect(stats.withSvg).toBe(stats.total);
  });
}
