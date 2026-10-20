// DIP adder regression — proves the 2026-07-05 interpreter fix
// (function-arg sub-expressions now live-fold via DeferredExpression)
// works end-to-end through the Visual runtime, not just the /api/v1
// simulate endpoint.
//
// The VHDL is `sum <= std_logic_vector(resize(unsigned(a), 5) +
// resize(unsigned(b), 5));` — the outer std_logic_vector cast wraps an
// arithmetic sub-expression, which was silently returning empty before
// the fix.

import { test, expect } from '@playwright/test';
import { ProjectsPage } from '../../pageobjects/ProjectsPage';

const DIP_VHD = `library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

entity dip_adder is
    port(
        a   : in  std_logic_vector(3 downto 0);
        b   : in  std_logic_vector(3 downto 0);
        sum : out std_logic_vector(4 downto 0)
    );
end dip_adder;

architecture rtl of dip_adder is
begin
    sum <= std_logic_vector(resize(unsigned(a), 5) + resize(unsigned(b), 5));
end rtl;
`;

test.describe('DIP adder — nested-arithmetic regression', () => {
  test.setTimeout(120_000);

  test('sum computes live when DIP switches change', async ({ request }) => {
    // Use the /api/v1 pipeline — same interpreter path the Visual
    // runtime feeds through. Stimulus mirrors two DIP flips: (3+5)=8
    // then (15+1)=16.
    const proj = await (await request.post('/api/v1/projects', {
      data: { name: 'dip_e2e', retain: false },
    })).json();

    await request.post(`/api/v1/projects/${proj.projectId}/files`, {
      data: { name: 'main.vhd', content: DIP_VHD },
    });

    const sim = await (await request.post('/api/v1/simulate', {
      data: {
        projectId: proj.projectId,
        entryPoint: 'dip_adder',
        timeScale: 'NS',
        duration: 200,
        systemClockMhz: 50,
        tickResNs: 10,
        portMoments: [
          { port: 'a', t: 0,   scale: 'NS', value: '0011' },
          { port: 'b', t: 0,   scale: 'NS', value: '0101' },
          { port: 'a', t: 100, scale: 'NS', value: '1111' },
          { port: 'b', t: 100, scale: 'NS', value: '0001' },
        ],
      },
    })).json();

    // Extract sum's VCD id + value transitions.
    const vcd: string = sim.vcd ?? '';
    const sumIdMatch = vcd.match(/\$var\s+\S+\s+\d+\s+(\S+)\s+sum\s+\$end/);
    expect(sumIdMatch, 'sum should be declared in the VCD').not.toBeNull();
    const sumId = sumIdMatch![1];

    // Collect every value-change line for sum.
    const changes: string[] = [];
    for (const line of vcd.split(/\r?\n/)) {
      const m = line.match(/^b([01UXZ]+)\s+(\S+)$/);
      if (m && m[2] === sumId) changes.push(m[1]);
    }
    expect(changes.length, 'sum must receive at least two values (initial + change)').toBeGreaterThanOrEqual(2);

    // Initial: 3 + 5 = 8 → 01000
    expect(changes[0]).toBe('01000');
    // After the a=1111,b=0001 stimulus: 15 + 1 = 16 → 10000 (5-bit)
    expect(changes[changes.length - 1]).toBe('10000');
  });
});
