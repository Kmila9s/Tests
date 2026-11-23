#!/bin/zsh
# CLI golden check (gate G.6): re-runs `kmila check/sim/synth/flow/fit` over the
# baseline designs and diffs the outputs against a baseline folder.
#
#   Tests/baseline/cli_golden.sh [baseline-cli-dir] [--update]
#
# Default baseline: the newest Tests/baseline/*/cli. --update rewrites the given
# folder instead of diffing (use only to create a NEW dated baseline).
# Requires a Release build of Kmila.CLI (net10.0).
set -u
ROOT=${0:A:h:h:h}
K=$ROOT/App/Kmila/Kmila.CLI/bin/Release/net10.0/kmila
BASE=${1:-$(ls -d $ROOT/Tests/baseline/*/cli | sort | tail -1)}
UPDATE=${2:-}
OUT=$(mktemp -d)
trap 'rm -rf $OUT' EXIT
[[ -x $K ]] || { echo "missing $K — build Kmila.CLI -c Release first"; exit 2; }

cd $ROOT
for c in 001-and-gate 002-decoder-3to8 003-counter-8bit 128-unsigned-slice-assign 166-case-expr-selector; do
  f=Tests/baseline/cli-designs/$c.vhdl   # single-file designs: entity entry + port stimulus
  $K check $f > $OUT/$c.check.txt 2>&1; echo "check=$?" > $OUT/$c.exit
  $K sim --top-file $f --duration 200ns --output-format vcd -o $OUT/$c.vcd > /dev/null 2>$OUT/$c.sim.err; echo "sim=$?" >> $OUT/$c.exit
  $K synth $f -o $OUT/$c.svg >/dev/null 2>&1; echo "synth=$?" >> $OUT/$c.exit
  $K flow $f -o $OUT/$c.puml >/dev/null 2>&1; echo "flow=$?" >> $OUT/$c.exit
  $K fit $f > $OUT/$c.fit.txt 2>&1; echo "fit=$?" >> $OUT/$c.exit
done
# The sim log carries the absolute output path; normalise it so baselines are portable.
sed -i '' -E "s| → .*/([^/]+\.vcd)$| → \1|" $OUT/*.sim.err

if [[ $UPDATE == --update ]]; then
  mkdir -p $BASE && cp $OUT/* $BASE/ && echo "baseline written: $BASE"; exit 0
fi

# Hash a file minus its run-dependent parts (sim timing/path, VCD $date block).
norm() {
  case $1 in
    *.sim.err) sed -E 's/ [0-9]+ms → .*//' $1 ;;
    *.vcd)     sed '/^\$date/,/\$end/d' $1 ;;
    *)         cat $1 ;;
  esac | shasum
}
cd $OUT
bad=0
for f in *; do
  [[ "$(norm $BASE/$f)" != "$(norm $f)" ]] && { echo "DIFF $f"; bad=1; }
done
[[ $bad == 0 ]] && echo "CLI: all $(ls | wc -l | tr -d ' ') outputs identical to $BASE"
exit $bad
