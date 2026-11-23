# Tests

Cross-cutting test harness for the Kmila **App** (the .NET/MAUI + Blazor
application under [`../App`](../../App)). It is organized into three independent
suites, each with its own toolchain:

| Suite | Type | Runner | Targets |
|-------|------|--------|---------|
| [`Playwright/`](#playwright--e2e-browser-tests) | End-to-end browser | `@playwright/test` (TypeScript) | `Kmila.Web` in a real browser |
| [`Performance/`](#performance--android-benchmark) | Performance benchmark | `bash` + `adb` | Kmila MAUI Android build on an emulator/device |
| [`Unit/`](#unit--net-unit-tests) | Unit | `dotnet test` (xUnit) | `Kmila.Shared` library, the engine and `Kmila.Frontend` |

Each suite is self-contained — there is no top-level "run all" command; run each
one with its own toolchain as described below.

---

## Playwright — E2E browser tests

The largest suite: **83 `*.spec.ts` files** (as of 2026-10-18) under
`Playwright/tests/**`, driving `Kmila.Web` in a real Chromium browser via
`@playwright/test`.

Tests are grouped into numbered scenario folders, for example:

- `00-smoke/` — basic smoke checks
- `01-chrome/` — topbar / burger / breadcrumbs chrome
- `02-projects/`, `03-editor/`, `04-diagrams/`, `05-compare/` — core editor and
  project flows
- `06-lessons/`, `07-practice/` — lessons and practice modality
- `08-settings/`, `09-bundles/`, `10-logs/`, `10-tooltips/` — settings and
  supporting UI
- `11-visual/` — visual / canvas rendering and firmware-view regressions (~20 specs)
- `99-audit/` — broad audit sweeps (~47 specs)

Supporting infrastructure lives alongside the specs:

- `pageobjects/` — page-object wrappers (`EditorPage`, `ProjectsPage`, `Monaco`,
  `RunBar`, `OutputPanel`, `TopBar`, `FilesCard`, `ConfirmDialog`).
- `data/` — VHDL fixtures used by tests (`data/vhdl/`, `data/lib/`), including
  practice and visual fixture sets.
- `fixtures/`, `artifacts/`, `test-results/`, `playwright-report/` — Playwright
  fixtures and generated output.
- `PLAN.md` — the canonical (partly aspirational) coverage plan. It notes that
  its coverage matrix under-inventories shipped work — grep the specs for the
  real inventory rather than trusting the matrix.

### Config

`playwright.config.ts`:

- **Auto-starts `Kmila.Web`** via `dotnet run --project ../../App/Kmila/Kmila.Web
  --framework net9.0 --launch-profile http` on port `5297` (override with
  `KMILA_WEB_PORT`). Cold build can take 60–90s, so the web-server timeout is
  generous (180s). An already-running server is reused unless in CI.
- Runs serially (`fullyParallel: false`, `workers: 1`).
- Projects: `chromium-desktop` (1440×900) and `chromium-mobile` (Pixel 7, only
  for `*.mobile.spec.ts`).
- On failure: retains trace, screenshot, and video.

### Running

```bash
cd Tests/Playwright
npm install          # first time only; also run `npx playwright install` for browsers
npm test             # playwright test (headless, auto-starts Kmila.Web)
npm run test:headed  # headed browser
npm run report       # open the last HTML report
```

Requires the .NET SDK on PATH (the config launches `Kmila.Web` itself) and
Node.js.

---

## Performance — Android benchmark

A single script, `Performance/perf-bench.sh`, that benchmarks the Kmila MAUI
Android build on an emulator or attached device. It measures four axes and exits
non-zero on threshold breach:

1. **Cold-start latency** — `am start -W` × 3, median `TotalTime` (fail > 3000 ms).
2. **Build + live-sim cycle** — tap Run, poll screencaps until frames stabilize
   (tracked for regression, no hard gate).
3. **Resource footprint (PSS)** — `dumpsys meminfo` TOTAL PSS (fail > 800 MB).
4. **Waveform-scroll smoothness** — `dumpsys gfxinfo` after scripted swipes
   (fail if jank > 25% or p90 > 80 ms).

All thresholds are overridable via `KMILA_PERF_*` env vars. See
`Performance/README.md` for full preconditions (booted emulator at 1080×2400,
`adb` on PATH, hard-coded tap coordinates), CI wiring, and known fragilities.

### Running

```bash
Tests/Performance/perf-bench.sh            # full benchmark, strict
Tests/Performance/perf-bench.sh --help     # all flags and env vars
Tests/Performance/perf-bench.sh --skip-sim # skip Axis 2 when no FPGA seeded
```

Requires a booted Android emulator (or USB device) reachable via `adb`, and a
buildable Kmila Android APK.

---

## Unit — .NET unit tests

Two xUnit projects (`net10.0`), both run in CI; `run-all.sh` runs locally exactly
what CI runs for the multi-HDL suites (unit tests, bench golden master + verbatim
ratchet, practice oracle — needs GHDL 6):

- **`Unit/Kmila.Engine.Tests/`** (since v3.0.1) — engine + front-end: tokenizer,
  user-type registry, variable ranges, the former Parser corpus
  (`Fixtures/ParserCorpus/test*.vhdl`, parsed through the production parser,
  plus must-reject files), and `Frontend/FrontendTests` (language registry,
  clean-state loads, in-memory elaboration, every entity closer form).
  `EngineFixture` is its single entry point for parsed designs. Since v3.0.10 it
  also holds the Interpreter's engine suites (`Interpreter/`, formerly
  `Interpreter --test-delta`; one fact per suite). The assembly runs serially.
- **`Unit/Kmila.Shared.Tests/`** — app services. `TestDesigns` is its single
  entry point for parsed designs (it goes through `Kmila.Frontend`, never a
  hand-copied pipeline). Multi-HDL (Fase 0): `TestKit/*` (VCD reader/comparer,
  case model, ratchet, harness, runners), `Fixtures/Cases/<group>/<case>` (v2
  cases with a committed GHDL `expected.vcd` per variant, regenerated by
  `SimulatorBench fixtures`; `FixtureCaseTests` has one row per case × supported
  language — a missing variant fails), `Content/ContentVariantTests` (every
  template and Learn sample simulates in each variant) and
  `Content/LanguageCoverageTests` (no required variant missing; a newly
  registered language is reported everywhere it is required).

Rules: sources under test are never edited to make Kmila pass — a known gap is
kept as a ratchet test that must still fail until the engine is fixed (see
`ParserCorpusTests.KnownGaps`). The current golden master is the newest dated
folder under `baseline/` with a `bench-kmila-vcd.sha256` — today
`baseline/2026-11-22-v319/` (bench verbatim: each author's testbench, 147/149
with G103 ratcheted; `cli/` outputs over `baseline/cli-designs/`;
`strict_scan.txt`; its README lists what changed and how it was checked
against GHDL). Older folders are kept as history; `baseline/2026-11-15/`
is the pre-refactor golden master.

`Kmila.Shared.Tests` exercises, e.g.:

- `VhdlBlockBuilderTests`, `VhdlBlockEmitterTests`, `BlockProgramTests`
- `RoundTripTests`, `RoundTripFidelityTests` (with `.vhd` fixtures under
  `Fixtures/RoundTripFidelity/`)
- `ProcedureFrameTests`, `WaitStatementTests`, `SelectedAssignmentTests`,
  `SignalAttributeTests`, `SliceHelperTests`

### Running

```bash
cd Tests
dotnet test Unit/Kmila.Engine.Tests -f net10.0 -p:TargetFrameworks=net10.0
dotnet test Unit/Kmila.Shared.Tests -f net10.0 -p:TargetFrameworks=net10.0
```

Requires the .NET 10 SDK.

---

## Unit — JavaScript (Concepts exercise interpreter)

`Unit/js/kmila-exec-sim.test.mjs` tests `App/Kmila/Kmila.Shared/wwwroot/js/kmila-exec-sim.js`,
the interpreter behind the Concepts › Execution models block exercises. It loads the script in a
`node:vm` sandbox (fake `window` / `document` / `localStorage`), builds workspaces in the same JSON
shape `kmila-blocks.js` serializes, and checks every world:

- `recipe` (e1) — valid order, wrong order explains the missing step, missing serve fails
- `kitchen` (e2) — four tasks in parallel, total = longest task
- `logic` (e3) — half-adder over all four input combinations, wrong gate and empty slot reported
- `clocked` (e4) — +1 per edge, two increments still +1 (last assignment wins), no-clock loop
- `restaurant` (e5) — sequential / polling / bells give the same totals as the `exec-restaurant`
  figure's model (≈ 70.6 / 37.4 / 22.7 s) and the matching stars
- `signals` (e6) — the variable reaches 16; a signal used as the intermediate reads its OLD value

### Running

```bash
node --test Tests/Unit/js/kmila-exec-sim.test.mjs
```

Requires Node 20+ (no npm dependencies).
