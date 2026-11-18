#!/bin/zsh
# Strictness scan (gate G, v3.1.3+): runs `kmila check` over every VHDL file in
# the repo, one invocation per directory (a directory ~ a project, so cross-file
# packages/entities resolve), and records which files report a parse error.
#
#   Tests/baseline/strict_scan.sh [baseline-file] [--update]
#
# Default baseline: the newest Tests/baseline/*/strict_scan.txt. The report lists
# files that are NEWLY rejected (must each be checked against GHDL 6.0.0: if GHDL
# accepts the file, the engine is wrong) and files that are newly accepted.
# --update writes the given file instead of diffing (only for a NEW dated folder).
# Requires a Release build of Kmila.CLI (net10.0).
set -u
ROOT=${0:A:h:h:h}
K=$ROOT/App/Kmila/Kmila.CLI/bin/Release/net10.0/kmila
BASE=${1:-$(ls $ROOT/Tests/baseline/*/strict_scan.txt 2>/dev/null | sort | tail -1)}
UPDATE=${2:-}
OUT=$(mktemp)
trap 'rm -f $OUT $OUT.*' EXIT
[[ -x $K ]] || { echo "missing $K — build Kmila.CLI -c Release first"; exit 2; }

cd $ROOT
find . \( -name bin -o -name obj -o -name node_modules -o -name .git \) -prune -o \
     -type f \( -name '*.vhd' -o -name '*.vhdl' \) -print | sed 's|^\./||' | sort > $OUT.files
for dir in $(sed 's|/[^/]*$||' $OUT.files | sort -u); do
  files=(${(f)"$(grep -E "^${dir}/[^/]+$" $OUT.files)"})
  # "<name>:<line>:<col>: error [CODE]: …" → "<dir>/<name>\tERR <line>"
  $K check ${files[@]} 2>&1 >/dev/null \
    | sed -nE "s|^([^:]+\.vhdl?):([0-9]+):[0-9]+: error.*|${dir}/\1	ERR \2|p" >> $OUT.err
done
# One line per file: OK, or ERR <first error line>.
sort -u -t$'\t' -k1,1 $OUT.err > $OUT.firsterr
awk -F'\t' 'NR==FNR { e[$1]=$2; next } { print $0 "\t" (($0 in e) ? e[$0] : "OK") }' $OUT.firsterr $OUT.files > $OUT

if [[ $UPDATE == --update ]]; then
  cp $OUT $BASE && echo "baseline written: $BASE ($(grep -c 'ERR' $BASE) of $(wc -l < $BASE | tr -d ' ') files with errors)"; exit 0
fi
[[ -f $BASE ]] || { echo "no baseline (pass a path with --update to create one)"; exit 2; }

newly_rej=$(join -t$'\t' <(sort $BASE) <(sort $OUT) | awk -F'\t' '$2=="OK" && $3!="OK" {print $1 "\t" $3}')
newly_ok=$(join -t$'\t' <(sort $BASE) <(sort $OUT) | awk -F'\t' '$2!="OK" && $3=="OK" {print $1}')
moved=$(join -t$'\t' <(sort $BASE) <(sort $OUT) | awk -F'\t' '$2!="OK" && $3!="OK" && $2!=$3 {print $1 "\t" $2 " -> " $3}')
added=$(join -v2 -t$'\t' <(sort $BASE) <(sort $OUT) | cut -f1)
echo "strict scan: $(grep -c ERR $OUT) of $(wc -l < $OUT | tr -d ' ') files with errors (baseline $(grep -c ERR $BASE))"
[[ -n $newly_rej ]] && { echo "NEWLY REJECTED (check each against GHDL):"; echo $newly_rej; }
[[ -n $newly_ok ]] && { echo "newly accepted:"; echo $newly_ok; }
[[ -n $moved ]] && { echo "error line moved:"; echo $moved; }
[[ -n $added ]] && { echo "files not in baseline:"; echo $added; }
[[ -z $newly_rej ]]
