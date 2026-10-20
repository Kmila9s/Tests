// Phase 0 Visual Runtime — fixture catalogue.
//
// Each fixture pairs a stable project name with the VHDL source that
// lives at Tests/Playwright/data/vhdl/visual/<id>.vhdl. The seed.spec.ts
// test materialises them through the UI; the §9 walk tests look them up
// by name and skip the create step.
//
// Keep names short, stable across sessions, and free of timestamps —
// these are persistent fixtures we want to find across multiple runs.

import * as fs from 'fs';
import * as path from 'path';

export interface VisualFixture {
  /** Stable project name to look up across runs. */
  readonly projectName: string;
  /** Sanitized file slug, becomes `<id>.vhdl` after createFile. */
  readonly fileSlug: string;
  /** Entity ports as the wiring modal will see them. */
  readonly ports: ReadonlyArray<{ name: string; dir: 'in' | 'out' }>;
  /** Inline VHDL source. */
  readonly source: string;
}

function load(slug: string): string {
  return fs.readFileSync(
    path.resolve(__dirname, '../../data/vhdl/visual', `${slug}.vhdl`),
    'utf8',
  );
}

export const FIXTURES: ReadonlyArray<VisualFixture> = [
  {
    projectName: 'Visual-Phase0-Passthrough',
    fileSlug: 'passthrough',
    ports: [
      { name: 'btn', dir: 'in' },
      { name: 'led', dir: 'out' },
    ],
    source: load('passthrough'),
  },
  {
    projectName: 'Visual-Phase0-Inverter',
    fileSlug: 'inverter',
    ports: [
      { name: 'btn', dir: 'in' },
      { name: 'led', dir: 'out' },
    ],
    source: load('inverter'),
  },
  {
    projectName: 'Visual-Phase0-AndGate',
    fileSlug: 'and_gate',
    ports: [
      { name: 'btn_a', dir: 'in' },
      { name: 'btn_b', dir: 'in' },
      { name: 'led', dir: 'out' },
    ],
    source: load('and_gate'),
  },
  {
    projectName: 'Visual-Phase0-SrLatch',
    fileSlug: 'sr_latch',
    ports: [
      { name: 'btn_s', dir: 'in' },
      { name: 'btn_r', dir: 'in' },
      { name: 'led_q', dir: 'out' },
      { name: 'led_n', dir: 'out' },
    ],
    source: load('sr_latch'),
  },
  {
    projectName: 'Visual-Phase1-ToggleLed',
    fileSlug: 'toggle_led',
    ports: [
      { name: 'sw', dir: 'in' },
      { name: 'lamp', dir: 'out' },
    ],
    source: load('toggle_led'),
  },
  {
    projectName: 'Visual-Phase1-Counter4',
    fileSlug: 'counter4',
    ports: [
      { name: 'clk', dir: 'in' },
      { name: 'rst', dir: 'in' },
      { name: 'count', dir: 'out' },
    ],
    source: load('counter4'),
  },
  {
    projectName: 'Visual-Phase2-Stepper',
    fileSlug: 'stepper_phases',
    ports: [
      { name: 'clk',     dir: 'in' },
      { name: 'a_out',   dir: 'out' },
      { name: 'b_out',   dir: 'out' },
      { name: 'an_out',  dir: 'out' },
      { name: 'bn_out',  dir: 'out' },
    ],
    source: load('stepper_phases'),
  },
];

export function fixtureByName(name: string): VisualFixture {
  const f = FIXTURES.find((x) => x.projectName === name);
  if (!f) throw new Error(`Fixture not found: ${name}`);
  return f;
}
