# Kmila Playwright Automation Plan

**Status:** Approved, in execution (foundation phase).
**Filed:** 2026-05-16
**Owner:** Daniel

This document is the canonical plan for the E2E suite. New scenarios get added here; specs are written against it. No code lands without a row that justifies it.

> **Real state (as of 2026-07-24):** 76 `*.spec.ts` files shipped under
> `Tests/Playwright/tests/**` (was `~38` in a prior memo — the memo
> under-inventoried). This document remains **aspirational for the
> coverage matrix** below; some bullets under-inventory shipped work
> (e.g. `11-visual/` has ~15 specs vs. the ~2 listed here). Do not
> treat the coverage matrix as an authoritative catalog — grep
> `tests/**/*.spec.ts` for the real inventory.

---

## Suite layout

```
Tests/Playwright/
├── playwright.config.ts          (webServer auto-starts Kmila.Web; chromium + firefox)
├── fixtures/
│   ├── app.ts                    (scratch app-data dir per worker; DB/UserAssets reset)
│   ├── language.ts               (force EN by default; helpers to switch)
│   ├── monaco.ts                 (setValue/getValue/waitForReady; multi-pane aware)
│   ├── downloads.ts              (capture + verify SVG/PNG/VCD/JSON/.kmila/.log)
│   ├── filetree.ts               (helpers: open/rename/move/delete via FilesCard)
│   └── snapshots.ts              (deterministic image baselines)
├── pageobjects/
│   ├── ProjectsPage.ts · EditorPage.ts · FilesCard.ts · RunBar.ts · OutputPanel.ts
│   ├── LearnPage.ts · LessonPage.ts · PracticePage.ts · PracticeViewPage.ts
│   ├── SettingsPage.ts · LogsPage.ts · TopBar.ts
├── data/
│   ├── vhdl/{exerciseId}/{right,half,wrong,cheat-*}.vhdl
│   ├── fpga/test-fpga.json
│   ├── lang/zz-test.json         (custom-language fixture; includes __LANG_NAME__)
│   ├── lib/test-lib.vhd
│   └── bundles/sample.kmila      (a known-good exported project for import test)
└── tests/
    ├── 00-smoke/        (already shipped: projects.spec.ts)
    ├── 01-chrome/       (topbar, burger visibility regression, breadcrumbs)
    ├── 02-projects/     (CRUD: create, list, search, remove)
    ├── 03-editor/       (file ops, monaco, save, multi-tab, preview, build, run)
    ├── 04-diagrams/     (schematic / waveform / flow render + export)
    ├── 05-compare/      (multi-run port/clock/FPGA variations + cross-run diff)
    ├── 06-lessons/      (catalog parametrized + per-lesson loads + progress)
    ├── 07-practice/     (3-case + anti-cheat per modality)
    ├── 08-settings/     (theme, lang, text-scale, custom FPGA, custom lang, libs)
    ├── 09-bundles/      (project export → import round-trip; library import + view)
    ├── 10-logs/         (search, filter, clear, download)
    └── 11-cross/        (console-error budget, network-error budget, i18n leak detection)
```

---

## Coverage matrix

### 01-chrome: top-bar / shell

| Spec | What it asserts |
|---|---|
| 01.1 | **Burger hidden on desktop** (≥ 641 px). Regression for the fix at `pages.css:519` — chain `.kmila-btn-icon.kmila-topbar__menu-btn { display: none }` so `.kmila-btn-icon`'s `inline-flex` cannot override it. |
| 01.2 | Burger visible on mobile (320–640 px); clicking opens the sidebar drawer. |
| 01.3 | Profile menu opens; theme/lang/text-size pickers reachable from it. |
| 01.4 | Breadcrumbs reflect the route across `/projects → /editor/:id → back to /projects`. |

### 02-projects: project CRUD

| Spec | What it asserts |
|---|---|
| 02.1 | Empty state shows; "Create new project" CTA visible. |
| 02.2 | Create project (covered by smoke) — card appears with name + description. |
| 02.3 | Sample project (`.projects-sample` CTA) creates a known-content project; verify file count + names. |
| 02.4 | Search bar (`.projects-search`) live-filters by name and description; clear (`.projects-search__clear`) resets. |
| 02.5 | **Remove project**: click `.project-card__action--danger` → ConfirmDialog appears (title `PROJECTS_REMOVING:{name}`, body uses `PROJECTS_REMOVE_MSG1/2`, primary button text `PROJECTS_REMOVE`, secondary `COMMON_GO_BACK`). Confirm → toast `PROJECTS_REMOVED_SUCCESS`, card disappears. Cancel branch → toast `PROJECTS_NOT_REMOVED`, card stays. |
| 02.6 | Remove all projects → empty state returns. |

### 03-editor: file ops, monaco, build, run

#### 03.A File operations (via `FilesCard`)

| Spec | What it asserts |
|---|---|
| 03.A.1 | **Create file** via `.files-list__toolbar` "+" → `CreateFileModal` (`#InputProjectFileName`, extension `#extentionType`). Default ext is `txt`; pick `vhdl`. New file appears in tree. |
| 03.A.2 | **Create folder** via folder-plus button → inline rename input (`.file-row__rename`). Enter commits; folder shows in tree. |
| 03.A.3 | **Open file** — click `.file-row__name` → Monaco loads content; row gains `.is-active`. |
| 03.A.4 | **Rename file** — kebab → `FILES_RENAME` → inline edit → Enter commits; new name in tree, Monaco's title updates. Escape cancels. |
| 03.A.5 | **Change extension** — same rename UI; type new ext (e.g. `.txt` → `.vhdl`); syntax highlight switches. |
| 03.A.6 | **Move file** — kebab → `FILES_MOVE_TO {folder}` for each target; file now under new parent. |
| 03.A.7 | **Move via drag-and-drop** — `draggable="true"` row dropped onto folder; same result as 03.A.6. |
| 03.A.8 | **Set as entry-point** — kebab → `FILES_SET_AS_ENTRY` (star). `.file-row` gains `.is-entry`; star marker `.file-row__entry-marker` visible. Pick another → marker moves. Clear via `FILES_CLEAR_ENTRY`. RunBar reflects the chosen entity. |
| 03.A.9 | **Delete file** — kebab → `FILES_DELETE` → ConfirmDialog (title `FILES_REMOVE_TITLE`, body `FILES_REMOVE_MSG1 + MSG2`). Confirm → tree row gone, Monaco tab closes if open. Cancel keeps it. |
| 03.A.10 | **Delete folder** — same flow with `FILES_FOLDER_REMOVE_CONFIRM`; deleting non-empty folder removes children. |

#### 03.B Editor & save

| Spec | What it asserts |
|---|---|
| 03.B.1 | Type code in Monaco; `.editor-unsaved` dot appears in header. |
| 03.B.2 | **Save via Ctrl+S** while Monaco is focused → dot disappears; toast `EDITOR_FILE_SAVED_BODY`. Browser default save dialog suppressed (per `kmila-shared-head.js`). |
| 03.B.3 | **Save via Cmd+S** on macOS (Playwright `Modifiers.Meta`). |
| 03.B.4 | **Save via RunBar button** — `.run-bar__save` (or callback) → same result. |
| 03.B.5 | After save, reload page → content persists. |
| 03.B.6 | Restart server (fixture) → content still persists (SQLite + on-disk). |

#### 03.C Multi-tab / split panes

| Spec | What it asserts |
|---|---|
| 03.C.1 | Open second file → `.editor-tab` count = 2. Active tab gains `.is-active`. |
| 03.C.2 | **Ctrl+Tab / Ctrl+Shift+Tab** cycles tabs (BlazorMonaco AddCommand bindings). |
| 03.C.3 | **Ctrl+W** closes active tab. |
| 03.C.4 | Tab close button (`.editor-tab__close`) closes that tab. |
| 03.C.5 | **Split toggle** (`.editor-split-toggle`) → workbench body switches to `.editor-workbench__body--panes` with two `.editor-pane`s. Each pane is independently focusable (`.is-focused`). |
| 03.C.6 | Open different files in left vs right pane; edit/save each independently. |
| 03.C.7 | **Markdown preview**: open a `.md` file → `.editor-split__pane` with `.editor-markdown` rendered on left, source on right. Header chip `EDITOR_MD_PREVIEW`. |

#### 03.D Build & run with error highlighting

| Spec | What it asserts |
|---|---|
| 03.D.1 | Write correct half-adder VHDL → click `.run-bar__build` → OutputPanel shows `SIM_LOG_ENDED_OK` row (`.output-row--success`). |
| 03.D.2 | Run again with stimulus → waveform tab shows trace; success row appears. |
| 03.D.3 | **Introduce a syntax error** (e.g. delete `end entity;`) → save → build → OutputPanel `.output-row--danger` row appears with line:column reference. |
| 03.D.4 | **Monaco diagnostics** — `window.kmilaSetMonacoMarkers` populates squiggles. Test asserts via `monaco.editor.getModelMarkers({ owner: 'kmila-vhdl' })` length > 0, severity == 8 (error). |
| 03.D.5 | Hover the squiggle range → tooltip shows the diagnostic message. (Playwright `page.mouse.move` + wait for Monaco's hover widget.) |
| 03.D.6 | Click error row in OutputPanel jumps cursor to the offending line (Monaco's `revealLineInCenter`). |
| 03.D.7 | **Fix the error** → build → markers clear; success row appears. |
| 03.D.8 | **Runtime error** (e.g. infinite loop) → state goes to `RuntimeError` / `Timeout` per SimState. |

#### 03.E Repeat the build/run cycle on a second file

| Spec | What it asserts |
|---|---|
| 03.E.1 | Set as entry: file A → build → success. Switch entry to file B (different entity) → build → success. RunBar shows the right entity each time. |
| 03.E.2 | Resource utilization (`EntityResourceUsage`) updates between the two entries. |

### 04-diagrams: render + export

| Spec | What it asserts |
|---|---|
| 04.1 | After a successful run, dock tab `#dock-tab-Schematic` shows `.schematic-viewer svg` with gate nodes. |
| 04.2 | `#dock-tab-Waveforms` shows `.waveform-viewer svg` with one trace per port. |
| 04.3 | `#dock-tab-Flow` shows `.flow-diagram svg` (Mermaid). Wait helper polls until SVG exists. |
| 04.4 | **Export schematic** → download captured, file ends in `.svg`, body starts with `<svg`. |
| 04.5 | **Export flow PNG** → download captured, magic bytes `\x89PNG`. |
| 04.6 | **Export flow SVG** → same as 04.4 but flow content. |
| 04.7 | **Export waveform VCD** → file matches `kmila-waveform-*.vcd`, content begins with `$version`. |

### 05-compare: vary inputs / clock / FPGA

| Spec | What it asserts |
|---|---|
| 05.1 | Run half-adder with `a=0,b=0` → waveform Y = 0,0. Re-run with `a=1,b=0` → Y = 1,0. Re-run with `a=1,b=1` → Y = 0,1. Snapshot per run. |
| 05.2 | Modify clock frequency in `.run-bar__clock` → re-run → period scales proportionally. |
| 05.3 | Change FPGA via `.run-bar__fpga` (e.g. `EP4CE10` → `iCE40HX1K`) → re-run → `EntityResourceUsage` numbers change. |
| 05.4 | Modify port moments (timing of stimulus) → re-run → waveform reflects new edges. |
| 05.5 | Save run A as a `SimulationSnapshot`, modify code, save run B; open compare → `.waveform-viewer--compare` shows both. |

### 06-lessons

| Spec | What it asserts |
|---|---|
| 06.1 | `/learn` lists all 15 modules; counts match manifest. |
| 06.2 | Level filter chips correctly gate the grid. |
| 06.3 | Search `.learn-hero__search` live-filters by title + summary; clear (`.projects-search__clear`-style) restores. |
| 06.4 | **Parametrized: open every lesson from manifest.** For each `{moduleId}/{lessonId}`: page loads, `article.lesson-body` present, no `.lesson-missing`, no console errors. |
| 06.5 | Markdown renders headings, code, tables, Mermaid. |
| 06.6 | Translation fallback banner appears on a lang missing a body. |
| 06.7 | Mark complete: button text/icon flip; chip `.lesson-chip--done` appears; back on catalog, progress chip increments. |
| 06.8 | Continue banner on catalog points at last-opened lesson. |
| 06.9 | Prev/Next nav crosses module boundaries. |
| 06.10 | Sample CTA spawns a project + jumps to editor. |
| 06.11 | Invalid slug shows `LEARN_LESSON_NOT_FOUND`. |

### 07-practice (3-case + anti-cheat)

**Baseline matrix:** 9 exercises (3 modalities × 3 difficulties).

| Modality | Easy | Medium | Hard |
|---|---|---|---|
| m1-from-zero | 01-half-adder | 04-counter-4bit | 03-alu-8bit |
| m2-fix-bug | (1 per difficulty, TBD) | … | … |
| m3-fill-gap | (1 per difficulty, TBD) | … | … |

For each row, run the **probe matrix**:

| Probe | Code | Expected verdict | Note |
|---|---|---|---|
| WRONG | gross logic error | `WrongAnswer` + `.t-detail` populated | validator catches obvious wrong |
| HALF | passes some tests, fails others | `WrongAnswer` + mixed `.ok` / `.ko` list | full coverage, not single-input |
| RIGHT | canonical solution | `Solved` + history entry persisted | correctness |
| CHEAT-EMPTY | empty file | `BuildError` | parser smoke |
| CHEAT-HARDCODE | outputs hardcoded to memorized sequence | should be `WrongAnswer` | **flag if it slips through — this is the anti-shortcut audit deliverable** |
| CHEAT-CANONICAL | exact copy of `solution.vhdl` | `Solved` | **documented limitation of Phase 0 (waveform-diff only)** |
| CHEAT-LOOP | infinite synthesis | `Timeout` or `RuntimeError` | |

Additionally per modality:

| Spec | What it asserts |
|---|---|
| 07.fix.1 | FixBug: load starter unchanged → `WrongAnswer` (proves bug is real); minimal patch → `Solved`. |
| 07.fill.1 | FillGap: right answer inside `-- BEGIN_EDIT / -- END_EDIT` → `Solved`. Same answer outside markers → `WrongAnswer`. |
| 07.persist | Solved status persists across reload; `.practice-history__list` accumulates attempts; reset starter button restores the starter. |
| 07.search | `/practice` search bar (`.learn-hero__search`) live-filters exercises. |

### 08-settings + customization

| Spec | What it asserts |
|---|---|
| 08.1 | Theme light/dark/auto: `data-bs-theme`, `kmila-theme` LS, `KMILA_THEME` AppSetting, Monaco theme. |
| 08.2 | Text scale xs..xl: `--user-text-scale` var, `kmila-text-scale` LS, `KMILA_TEXT_SCALE` AppSetting. |
| 08.3 | Each built-in language (en/es/fr/de/ch/jp/ar): canary UI string flips; `KMILA_LANGUAGE` AppSetting. |
| 08.4 | **Custom language import** (`data/lang/zz-test.json` with `__LANG_NAME__` + 5 canary keys): upload via `<InputFile accept=".json">` (max 512 KB). Appears in language picker. Switch to it → canary keys visible. Delete → picker no longer lists it. |
| 08.5 | **Custom FPGA import** (`data/fpga/test-fpga.json`): upload via `.settings-import-btn` (max 2 MB). Appears in `.run-bar__fpga` picker inside an editor session. Selecting it persists. Delete → no longer offered. |
| 08.6 | Tick resolution mode (`default`/`high`/`adaptive`) persists. |
| 08.7 | Library build mode persists; Clear library cache button removes cache dir contents. |
| 08.8 | Bundle export: pick project → download captured → file is JSON, parseable, contains expected keys (`name`, `files[]`, `runs[]`). Filename uses underscores. |
| 08.9 | Bundle import: upload `data/bundles/sample.kmila` → toast `SETTINGS_BUNDLE_IMPORTED`; new project listed in `/projects`. |
| 08.10 | Learn progress reset (confirm dialog) → counter returns to 0. |
| 08.11 | Diagnostics → navigates to `/logs`. |
| 08.12 | GitHub: with no token, sign-in CTA visible. Enter Client ID, save → `KMILA_GITHUB_CLIENT_ID` persisted. Auto-report toggle persisted. (Live device-flow login skipped — out of scope without mock IdP.) |
| 08.13 | Feedback Tier 1: copy diagnostics → clipboard has expected JSON-ish blob. |
| 08.14 | Feedback Tier 2: click → new tab matches `github.com/.../issues/new?...body=`. |

### 09-bundles & libraries

| Spec | What it asserts |
|---|---|
| 09.1 | Export project A → file captured. Import → project A' appears with same files. Open A', files open in Monaco identically. |
| 09.2 | **Library import** (`.vhd` ≤ 4 MB): upload via `.settings-import-btn` under "User VHDL libraries". Appears in `.settings-list` with the parsed package list (`.settings-list__code`). |
| 09.3 | **View library**: click eye icon → modal `.library-source-view` shows raw source. |
| 09.4 | Delete library → row disappears. |
| 09.5 | **Library export** — *NOT IMPLEMENTED in app*. Add a row to PLAN.md backlog asking whether to ship an export button; for now, the test is a skipped `test.skip()` placeholder so the gap is visible in the report. |

### 10-logs

| Spec | What it asserts |
|---|---|
| 10.1 | `/logs` renders title + list. |
| 10.2 | Live: trigger an action in another tab that emits a log → row appears (depends on store push). |
| 10.3 | Level filter (`.logs-page__level`): switch to `error` → only `.output-row--danger` rows visible. |
| 10.4 | Search `.logs-page__search` live-filters by message + category. Clear button resets. |
| 10.5 | Pause toggle → no new rows arrive; Resume → backlog catches up. |
| 10.6 | Auto-scroll toggle behaves. |
| 10.7 | **Clear logs** (trash icon, key `LOGS_CLEAR`): list becomes empty. |
| 10.8 | **Download logs** — *NOT IMPLEMENTED in app*. Backlog row added; placeholder `test.skip()` for now. |

### 11-cross-cutting

| Spec | What it asserts |
|---|---|
| 11.1 | **Console-error budget:** each spec attaches `page.on('pageerror')` + `page.on('console', 'error')`; any non-allow-listed error fails the spec. |
| 11.2 | **Network-error budget:** non-200/204/304 responses fail unless on allow-list (lang fallback 404s allowed). |
| 11.3 | **i18n leak detector:** load Settings/Projects/Editor in `es`, `ar`, `ch`. Walk visible text. Flag any ASCII-only word ≥4 letters that lacks the ⚠ pending marker. |
| 11.4 | Visual baselines via `toHaveScreenshot` for: empty Projects, Editor with half-adder, Schematic, Waveform, each settings card. |

---

## Test isolation

Each Playwright worker:
1. Gets a unique scratch dir `/tmp/kmila-pw-${workerIdx}/`.
2. Launches Kmila.Web with env `KMILA_APP_DATA_PATH=$dir` (small app-side ask: a single env-read in storage init).
3. Before each spec, fixture deletes `$dir/kmila.db` and `$dir/UserAssets/*` to guarantee a clean slate.
4. Optionally, if the app gains a `--testing` mode, that flag also disables analytics/telemetry calls.

Until the env-var override lands in the app, fall back to deleting the well-known data dir between specs (slower; serialized on worker 0 only).

---

## App-side asks (small)

1. **`KMILA_APP_DATA_PATH` env override** for SQLite + UserAssets root — one line at storage init.
2. **`data-testid` on ~15 critical controls** — RunBar build/clock/fpga, dock tabs, port-moment grid, CreateFileModal name input — to outlive CSS-class renames during UI churn.
3. Optional: a `/__test__/reset` endpoint gated by env, to make resets sub-second rather than process-restart.

Suite ships fine without these; they just make it faster and more robust.

---

## Phasing

| Phase | Scope | Est. days |
|---|---|---|
| 1 | Foundation (webServer, fixtures, page objects, smoke refactor, 01-chrome) | 2 |
| 2 | 02-projects + 03-editor file ops + 03-editor save/multi-tab | 3 |
| 3 | 03-editor build/run + error highlight + 04-diagrams + 05-compare | 3 |
| 4 | 06-lessons parametrized | 1 |
| 5 | 07-practice 3-case + anti-cheat audit | 3 |
| 6 | 08-settings + 09-bundles + 10-logs | 2 |
| 7 | 11-cross + visual baselines + CI wire-up | 2 |

**~16 dev-days total.** Each phase is independently green-able.

---

## Backlog (gaps the suite surfaced)

- **Library export** — Settings has import but no export. Decide whether to ship.
- **Logs download** — Logs page has Clear but no Download button. Decide format (`.log` plain text? `.json` for machine ingest?).
- **Editor file-tree search** — no search input in `FilesCard`. Low priority for small projects.
- **Practice anti-cheat hardening (Phase 1 of validator)** — current waveform-diff allows the "memorize the canonical solution" probe to pass. Decide whether structural rubric / code-quality heuristic should land.

---

*This plan is the source of truth. Updates land here first, then propagate to specs.*
