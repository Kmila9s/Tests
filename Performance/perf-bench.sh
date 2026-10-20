#!/usr/bin/env bash
#
# perf-bench.sh — Kmila Android-emulator performance benchmark.
#
# Measures four axes and exits non-zero if any threshold is breached. See
# README.md beside this script for preconditions, threshold env vars, and the
# rationale for each axis.
#
#   Axis 1  Cold-start latency       — `am start -W` × 3 (median)
#   Axis 2  Build + live-sim cycle   — Run tap → screen-hash stabilization × 3
#   Axis 3  Resource footprint       — dumpsys meminfo (TOTAL PSS)
#   Axis 4  Waveform-scroll jank     — dumpsys gfxinfo after scripted swipes
#
# Hard-coded tap coordinates target a 1080×2400 emulator (the configuration
# documented for adb-driven Kmila exploration). Other resolutions print a
# warning and continue — most taps still land but flag any axis that returns
# nonsense numbers.

set -euo pipefail

# ---------- defaults ----------------------------------------------------------

PKG="kmila.kmila"
ACT="$PKG/crc6400a01a20fa44baf6.MainActivity"

THRESHOLD_COLDSTART_MS="${KMILA_PERF_COLDSTART_MS:-3000}"
THRESHOLD_PSS_MB="${KMILA_PERF_PSS_MB:-800}"
THRESHOLD_JANK_PCT="${KMILA_PERF_JANK_PCT:-25}"
THRESHOLD_P90_MS="${KMILA_PERF_P90_MS:-80}"
EXPECTED_RES="${KMILA_PERF_EXPECTED_RES:-1080x2400}"

DEVICE=""
INSTALL_APK=""
JSON_OUT=""
NO_STRICT=0
SKIP_SIM=0
SKIP_NAV=0

# ---------- arg parsing -------------------------------------------------------

usage() {
  cat <<EOF
Usage: $(basename "$0") [options]

Required environment:
  An Android device or emulator running Kmila, reachable via adb.

Options:
  --device <serial>     ADB serial (defaults to the only attached device).
  --install <apk>       'adb uninstall' then 'adb install -r <apk>' before
                        measuring. Required for a meaningful cold-start
                        baseline because subsequent runs reuse JIT cache.
  --json <path>         Write machine-readable report to <path>.
  --skip-sim            Skip Axis 2 (build+sim). Use when no FPGA is seeded.
  --skip-nav            Assume the editor is already open with sample project.
  --no-strict           Run every axis but always exit 0 (advisory mode).
  -h, --help            Show this help.

Thresholds (override with environment variables):
  KMILA_PERF_COLDSTART_MS   default $THRESHOLD_COLDSTART_MS
  KMILA_PERF_PSS_MB         default $THRESHOLD_PSS_MB
  KMILA_PERF_JANK_PCT       default $THRESHOLD_JANK_PCT
  KMILA_PERF_P90_MS         default $THRESHOLD_P90_MS
  KMILA_PERF_EXPECTED_RES   default $EXPECTED_RES
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --device)     DEVICE="$2"; shift 2 ;;
    --install)    INSTALL_APK="$2"; shift 2 ;;
    --json)       JSON_OUT="$2"; shift 2 ;;
    --no-strict)  NO_STRICT=1; shift ;;
    --skip-sim)   SKIP_SIM=1; shift ;;
    --skip-nav)   SKIP_NAV=1; shift ;;
    -h|--help)    usage; exit 0 ;;
    *)            echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

# ---------- helpers -----------------------------------------------------------

log()     { printf '[perf-bench] %s\n' "$*" >&2; }
adb_()    { adb ${DEVICE:+-s "$DEVICE"} "$@"; }
require() { command -v "$1" >/dev/null 2>&1 || { echo "Missing dependency: $1" >&2; exit 2; }; }

# Portable md5: macOS ships `md5 -q`, Linux ships `md5sum`. Pick whichever.
if command -v md5 >/dev/null 2>&1; then
  md5hash() { md5 -q "$1"; }
elif command -v md5sum >/dev/null 2>&1; then
  md5hash() { md5sum "$1" | awk '{print $1}'; }
else
  echo "Need md5 or md5sum" >&2; exit 2
fi

require adb

# Pick device when not supplied. (Stock macOS bash 3.2 has no mapfile, hence
# the read-loop instead of `mapfile -t`.)
if [[ -z "$DEVICE" ]]; then
  DEVS=()
  while IFS= read -r d; do
    [[ -n "$d" ]] && DEVS+=("$d")
  done < <(adb devices | awk 'NR>1 && $2=="device" {print $1}')
  case "${#DEVS[@]}" in
    0) echo "No adb device attached" >&2; exit 2 ;;
    1) DEVICE="${DEVS[0]}" ;;
    *) echo "Multiple devices attached; pass --device <serial>" >&2; exit 2 ;;
  esac
fi
log "device: $DEVICE"

# Resolution sanity check (taps are coord-based).
RES=$(adb_ shell wm size 2>/dev/null | awk '/Physical size:/ {print $3}' | tr -d '\r')
log "resolution: $RES"
if [[ "$RES" != "$EXPECTED_RES" ]]; then
  log "WARNING: expected $EXPECTED_RES, got $RES — tap coords may miss; numbers may be unreliable."
fi

# Optional reinstall — clears stored projects and JIT cache for a true cold start.
if [[ -n "$INSTALL_APK" ]]; then
  log "uninstalling + installing $INSTALL_APK"
  adb_ uninstall "$PKG" >/dev/null 2>&1 || true
  adb_ install -r "$INSTALL_APK" >/dev/null
fi

# Wake screen + dismiss keyguard so taps land.
adb_ shell input keyevent 82 >/dev/null 2>&1 || true
adb_ shell wm dismiss-keyguard >/dev/null 2>&1 || true

# ---------- axis 1: cold-start -----------------------------------------------

log "Axis 1/4: cold-start (3 runs)"
CS_TIMES=()
for i in 1 2 3; do
  adb_ shell am force-stop "$PKG" >/dev/null
  sleep 3
  T=$(adb_ shell am start -W -n "$ACT" 2>/dev/null \
        | awk '/TotalTime:/ {print $2; exit}' \
        | tr -d '\r')
  if [[ -z "$T" ]]; then
    log "WARNING: cold-start run $i returned no TotalTime; recording 0"
    T=0
  fi
  CS_TIMES+=("$T")
  log "  run $i: ${T} ms"
  sleep 4
done
COLDSTART_MEDIAN=$(printf '%s\n' "${CS_TIMES[@]}" | sort -n | awk 'NR==2')

# ---------- navigate to editor (default flow) --------------------------------

if [[ "$SKIP_NAV" -eq 0 ]]; then
  log "Navigating Home → Projects → sample project"
  # The app is in foreground after the last 'am start' above. Allow Blazor to
  # hydrate before tapping.
  sleep 5

  # Home page: "Go to Projects" button.
  adb_ shell input tap 540 1670
  sleep 3

  # Projects page. Two possible states:
  #   (a) No projects yet → tap "Try a sample" (creates the bundled project)
  #   (b) A project exists → tap the open-icon on the first card
  # Both buttons live on roughly the same column. We tap "Try a sample" first
  # (top button, y≈660). On state (b) it shows a toast but no project is
  # opened; we follow up by tapping the row's open-icon at (855, 970) which
  # works for state (a) once the new card materialises and for state (b)
  # directly.
  adb_ shell input tap 540 660
  sleep 4
  adb_ shell input tap 855 970
  sleep 6
fi

# ---------- axis 2: build + live-sim cycle -----------------------------------

SIM_TIMES=()
SIM_MEAN=0
if [[ "$SKIP_SIM" -eq 0 ]]; then
  log "Axis 2/4: build + sim cycle (3 runs)"
  for i in 1 2 3; do
    T0=$(adb_ shell 'date +%s%N' 2>/dev/null | tr -d '\r')
    adb_ shell input tap 780 1115   # Run button (anchored to the run-bar)
    prev_hash=""
    stable=0
    for _ in $(seq 1 80); do
      adb_ shell screencap -p > /tmp/perf_poll.png 2>/dev/null
      cur=$(md5hash /tmp/perf_poll.png)
      if [[ "$cur" == "$prev_hash" ]]; then
        stable=$((stable + 1))
        [[ $stable -ge 3 ]] && break
      else
        stable=0
      fi
      prev_hash=$cur
      sleep 0.25
    done
    T1=$(adb_ shell 'date +%s%N' 2>/dev/null | tr -d '\r')
    ELAPSED_MS=$(( (T1 - T0) / 1000000 - 750 ))   # subtract the 3 settle polls
    [[ $ELAPSED_MS -lt 0 ]] && ELAPSED_MS=0
    SIM_TIMES+=("$ELAPSED_MS")
    log "  run $i: ${ELAPSED_MS} ms"
    sleep 2
  done
  SIM_MEAN=$(printf '%s\n' "${SIM_TIMES[@]}" | awk '{s+=$1} END{print int(s/NR)}')
else
  log "Axis 2/4: SKIPPED (--skip-sim)"
fi

# ---------- axis 3: meminfo (PSS) --------------------------------------------

log "Axis 3/4: memory footprint"
PSS_KB=$(adb_ shell dumpsys meminfo "$PKG" 2>/dev/null \
            | awk '/TOTAL PSS:/ {print $3; exit}' \
            | tr -d '\r')
: "${PSS_KB:=0}"
PSS_MB=$(( PSS_KB / 1024 ))

# ---------- axis 4: gfxinfo (waveform scroll) --------------------------------

log "Axis 4/4: waveform-scroll frame timing"
# Make sure we're on the Waveforms dock tab so the swipes target the trace area.
adb_ shell input tap 870 1230 >/dev/null
sleep 2
adb_ shell dumpsys gfxinfo "$PKG" reset >/dev/null
for _ in 1 2 3 4 5 6; do
  adb_ shell input swipe 800 1700 200 1700 200 >/dev/null
done
sleep 1
GFX=$(adb_ shell dumpsys gfxinfo "$PKG" 2>/dev/null)
TOTAL_FRAMES=$(printf '%s\n' "$GFX" | awk '/Total frames rendered:/ {print $4; exit}' | tr -d '\r')
JANK=$(printf '%s\n' "$GFX" \
        | awk '/Janky frames:/ {gsub(/[()%]/,"",$4); print $4; exit}' \
        | tr -d '\r')
P50=$(printf '%s\n' "$GFX" | awk '/^50th percentile:/ {gsub("ms","",$3); print $3; exit}' | tr -d '\r')
P90=$(printf '%s\n' "$GFX" | awk '/^90th percentile:/ {gsub("ms","",$3); print $3; exit}' | tr -d '\r')
P99=$(printf '%s\n' "$GFX" | awk '/^99th percentile:/ {gsub("ms","",$3); print $3; exit}' | tr -d '\r')
: "${TOTAL_FRAMES:=0}"; : "${JANK:=0}"; : "${P50:=0}"; : "${P90:=0}"; : "${P99:=0}"

# ---------- report ------------------------------------------------------------

echo
echo "Kmila perf bench — $(date '+%Y-%m-%d %H:%M:%S')"
echo "Device $DEVICE  /  resolution $RES"
printf '  Cold-start  median   %5d ms        runs: %s\n' "$COLDSTART_MEDIAN" "${CS_TIMES[*]}"
if [[ "$SKIP_SIM" -eq 0 ]]; then
  printf '  Sim cycle   mean     %5d ms        runs: %s\n' "$SIM_MEAN" "${SIM_TIMES[*]}"
else
  printf '  Sim cycle   skipped\n'
fi
printf '  PSS                  %5d MB\n' "$PSS_MB"
printf '  Frames %d  jank %s%%  p50 %sms  p90 %sms  p99 %sms\n' \
  "$TOTAL_FRAMES" "$JANK" "$P50" "$P90" "$P99"

# Machine-readable JSON.
if [[ -n "$JSON_OUT" ]]; then
  # Build run arrays as JSON literals.
  csruns_json=$(printf '%s,' "${CS_TIMES[@]}" | sed 's/,$//')
  simruns_json=$(printf '%s,' "${SIM_TIMES[@]:-0}" | sed 's/,$//')
  cat > "$JSON_OUT" <<JSON
{
  "device": "$DEVICE",
  "resolution": "$RES",
  "timestamp": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')",
  "cold_start_ms":  { "median": $COLDSTART_MEDIAN, "runs": [$csruns_json] },
  "sim_cycle_ms":   { "mean":   $SIM_MEAN,        "runs": [$simruns_json], "skipped": $([[ $SKIP_SIM -eq 1 ]] && echo true || echo false) },
  "pss_mb":         $PSS_MB,
  "gfxinfo": {
    "total_frames": $TOTAL_FRAMES,
    "jank_percent": $JANK,
    "p50_ms":       $P50,
    "p90_ms":       $P90,
    "p99_ms":       $P99
  }
}
JSON
  log "wrote $JSON_OUT"
fi

# ---------- threshold gating --------------------------------------------------
# Uses awk for floating-point comparisons (jank% is "19.12" not "19").

awk_gt() { awk -v a="$1" -v b="$2" 'BEGIN { exit (a+0 > b+0) ? 0 : 1 }'; }

EXIT=0
fail() { echo "FAIL: $1" >&2; EXIT=1; }

awk_gt "$COLDSTART_MEDIAN" "$THRESHOLD_COLDSTART_MS" && fail "cold-start median $COLDSTART_MEDIAN ms > $THRESHOLD_COLDSTART_MS"
awk_gt "$PSS_MB"           "$THRESHOLD_PSS_MB"        && fail "PSS $PSS_MB MB > $THRESHOLD_PSS_MB"
awk_gt "$JANK"             "$THRESHOLD_JANK_PCT"      && fail "jank% $JANK > $THRESHOLD_JANK_PCT"
awk_gt "$P90"              "$THRESHOLD_P90_MS"        && fail "p90 $P90 ms > $THRESHOLD_P90_MS"

if [[ "$NO_STRICT" -eq 1 ]]; then
  [[ "$EXIT" -ne 0 ]] && log "advisory mode (--no-strict) — would have exited $EXIT"
  exit 0
fi
exit "$EXIT"
