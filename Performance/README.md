# Tests/Performance

`perf-bench.sh` — Android-emulator (or device) performance benchmark for the
Kmila MAUI build. One script, four axes, machine-readable output.

## Why this is here

A 2026-06-16 manual benchmark (`v1.23.0` branch) on the standard 1080×2400
emulator surfaced one real regression risk: **19 % jank during waveform-scroll**
with p90 = 61 ms. Cold-start (1.8 s), build+sim cycle (18 s, live-paced), and
post-sim PSS (533 MB) were all within acceptable bands. The script codifies
those four measurements so the same numbers can be tracked build-to-build.

## What it measures

| Axis | How | Default fail threshold |
|------|-----|------------------------|
| Cold-start latency | `am start -W` × 3, take the median `TotalTime` | > 3000 ms |
| Build + live-sim cycle | Tap Run, poll screencap MD5 until 3 consecutive identical frames (sim done = waveform animation stopped) | *(no gate — paced UX, tracked for σ/regression only)* |
| PSS (resource footprint) | `dumpsys meminfo … TOTAL PSS` | > 800 MB |
| Waveform-scroll smoothness | `dumpsys gfxinfo reset` → 6 horizontal swipes on the waveform body → parse `Janky frames` / `90th percentile` | jank > 25 %, or p90 > 80 ms |

All thresholds are overridable per env var — see `--help`.

## Preconditions

The script is intentionally low-magic and assumes a known starting state.

1. **A booted Android emulator (or USB device) reachable via `adb`.** Either a
   single one attached, or pass `--device <serial>`.
2. **1080×2400 resolution.** Tap coordinates are hard-coded; off-spec
   resolutions print a warning and continue, but the navigation taps will
   likely miss and downstream numbers become noise. Override the warning's
   expected value with `KMILA_PERF_EXPECTED_RES`.
3. **An FPGA picked in Settings** (if you want Axis 2 to work). The sim
   `Run` button is disabled until a board is selected. Without one, pass
   `--skip-sim` to get the other three axes; the script will still print
   and gate them.
4. **macOS or Linux host** with `bash`, `adb`, and either `md5` or
   `md5sum` on PATH.

If you pass `--install <apk>`, the script `adb uninstall`s the package first,
giving a true cold-cache baseline at the cost of erasing local projects.
Without `--install`, subsequent runs benefit from the JIT cache and cold-start
numbers run 400-500 ms lower than the first-after-install value.

## Usage

```bash
# Full benchmark, dies on threshold breach:
Tests/Performance/perf-bench.sh

# Reinstall first for a true cold start, write JSON for trend tracking:
Tests/Performance/perf-bench.sh \
  --install App/Kmila/Kmila/bin/Debug/net9.0-android35.0/kmila.kmila-Signed.apk \
  --json /tmp/perf.json

# No FPGA seeded — skip Axis 2 (sim), still report + gate the other three:
Tests/Performance/perf-bench.sh --skip-sim

# Editor already open with sample project — skip the home → projects nav:
Tests/Performance/perf-bench.sh --skip-nav

# Advisory mode: print every number, never fail. Use when bringing up a
# new device profile and you don't yet trust the thresholds.
Tests/Performance/perf-bench.sh --no-strict
```

## Plugging into CI

The script is stateless: it expects an emulator already up (or a device
attached) and the APK already buildable. Glue layer:

```bash
# pseudo-CI job
- name: Build APK
  run: dotnet build App/Kmila/Kmila/Kmila.csproj \
         -c Debug -f net9.0-android35.0 \
         -p:EmbedAssembliesIntoApk=true \
         -p:AndroidFastDeploymentType=None

- name: Boot emulator
  uses: reactivecircus/android-emulator-runner@v2
  with:
    api-level: 35
    arch: arm64-v8a            # or x86_64 if your runner is intel
    target: google_apis
    profile: pixel_5           # 1080×2400, matches the bake-in coords
    script: |
      Tests/Performance/perf-bench.sh \
        --install App/Kmila/Kmila/bin/Debug/net9.0-android35.0/kmila.kmila-Signed.apk \
        --skip-sim \
        --json $GITHUB_STEP_SUMMARY/perf.json
```

Critical bits worth surfacing to whoever wires this in:

- `EmbedAssembliesIntoApk=true` + `AndroidFastDeploymentType=None` are
  **required**. Without them MAUI Fast Deployment caches stale DLLs under
  `/data/data/kmila.kmila/files/.__override__/` between runs and the
  benchmark measures the old build (this bit us on 2026-06-16 — see the
  manual perf walk).
- `--skip-sim` is the safe default for unattended CI until you wire FPGA
  seeding. The waveform-scroll axis (the one with the actual finding) does
  not need a live sim — historical sample data is sufficient — but in the
  current script the gfxinfo run happens after the sim cycle and uses
  whatever waveform is on screen. If you skip sim, also script in a
  hand-picked snapshot load before invoking the script.
- The thresholds are calibrated against a macOS-host HVF emulator. A Linux
  GitHub runner with x86_64 KVM-accelerated emulation typically runs ~30 %
  slower on cold-start and ~10 % worse on jank — re-baseline before
  promoting strict mode to a hard gate.

## Known fragility

- **Tap coordinates.** The navigation flow (Home → Projects → sample row →
  Run) uses hard-coded `(x, y)` pairs. UI changes to those screens will
  break the script silently — Axis 2 will time out and return ~20 s with
  no useful signal. When you re-skin the topbar or the projects list,
  re-derive the coords and update the comments in `perf-bench.sh`.
- **WebView opacity to UI automator.** Blazor's rendered DOM lives inside
  a WebView, so `uiautomator dump` returns empty text fields for every
  in-app control. That's why we tap coordinates instead of querying by
  text — `uiautomator` simply cannot see the buttons.
- **Screen-hash sim detection.** The "sim done" signal is "3 consecutive
  byte-identical screencaps". Live cursor / hover overlays could in theory
  keep the frame perpetually different, but in practice the editor
  stabilises within ~250 ms of sim end. If a future feature animates the
  toolbar permanently (e.g., a running shimmer), this heuristic will need
  a more specific signal — pixel-region OCR or a logcat marker emitted on
  `Sim.State == Complete`.
