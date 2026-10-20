# Tests

Cross-cutting test harness for the Kmila **App** (the .NET/MAUI + Blazor
application under [`../App`](../App)). It is organized into three independent
suites, each with its own toolchain:

| Suite | Type | Runner | Targets |
|-------|------|--------|---------|
| [`Playwright/`](#playwright--e2e-browser-tests) | End-to-end browser | `@playwright/test` (TypeScript) | `Kmila.Web` in a real browser |
| [`Performance/`](#performance--android-benchmark) | Performance benchmark | `bash` + `adb` | Kmila MAUI Android build on an emulator/device |
| [`Unit/`](#unit--net-unit-tests) | Unit | `dotnet test` (xUnit) | `Kmila.Shared` library |

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

An xUnit project, `Unit/Kmila.Shared.Tests/`, targeting `net9.0` and
referencing `App/Kmila/Kmila.Shared`. It exercises the VHDL block model and
round-trip logic in `Kmila.Shared`, e.g.:

- `VhdlBlockBuilderTests`, `VhdlBlockEmitterTests`, `BlockProgramTests`
- `RoundTripTests`, `RoundTripFidelityTests` (with `.vhd` fixtures under
  `Fixtures/RoundTripFidelity/`)
- `ProcedureFrameTests`, `WaitStatementTests`, `SelectedAssignmentTests`,
  `SignalAttributeTests`, `SliceHelperTests`

### Running

```bash
cd Tests/Unit/Kmila.Shared.Tests
dotnet test
```

Requires the .NET 9 SDK.
