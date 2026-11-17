#!/bin/zsh
# Duplication gate (gate G.7): jscpd over the engine + app + tools sources.
#
#   Tests/baseline/jscpd.sh [root]      # root defaults to the repo checkout
#
# Prints "clones lines percentage". The percentage may never go up from one
# stage to the next (AUDIT_duplicacion_SOLID_2026-11.md, method §1).
set -u
ROOT=${1:-${0:A:h:h:h}}
OUT=$(mktemp -d)
trap 'rm -rf $OUT' EXIT
cd $ROOT
dirs=()
for d in App/Kmila Interpreter Parser Debugger TimeMachine LibraryCompiler PracticeValidator SimulatorBench Tests/Unit; do
  [[ -d $d ]] && dirs+=$d
done
npx --yes jscpd@4.0.5 --silent --min-lines 12 --min-tokens 80 \
  --formats-exts "csharp:cs,razor;javascript:js,mjs" \
  --ignore "**/bin/**,**/obj/**,**/wwwroot/lib/**,**/node_modules/**,**/wwwroot/Learn/**,**/wwwroot/Practice/**,**/wwwroot/Concepts/**,**/Cases/**,**/*.min.js" \
  --reporters json --output $OUT $dirs >/dev/null 2>&1
python3 -c "
import json,sys
t=json.load(open('$OUT/jscpd-report.json'))['statistics']['total']
print(t['clones'], t['duplicatedLines'], f\"{t['percentage']:.2f}%\", t['sources'], 'sources', t['lines'], 'lines')"
