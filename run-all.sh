#!/bin/zsh
# Runs locally exactly what CI runs for the multi-HDL suites (Fase 0, v3.0.12),
# from a checkout where the repos are siblings (Kmila-9s/{Parser,…,App/Kmila,
# Tests,SimulatorBench,PracticeValidator}). Needs GHDL 6 on the PATH.
#   zsh Tests/run-all.sh        (from the Kmila-9s root or anywhere inside it)
# Exits non-zero at the first failing step.
set -e
ROOT=${0:A:h:h}
step() { print -P "\n%B== $1%b"; }

step "unit tests (Kmila.Engine.Tests: front-end, parser, Interpreter suites)"
dotnet test $ROOT/Tests/Unit/Kmila.Engine.Tests -c Release -f net10.0 -p:TargetFrameworks=net10.0
step "unit tests (Kmila.Shared.Tests: engine, TestKit, fixture cases, content)"
dotnet test $ROOT/Tests/Unit/Kmila.Shared.Tests -c Release -f net10.0 -p:TargetFrameworks=net10.0

cd $ROOT/SimulatorBench
dotnet build -c Release -p:TargetFrameworks=net10.0
step "bench: golden master (authors' testbenches verbatim, ratcheted by known-gaps.json) + hashes"
dotnet run -c Release --no-build -- run
BASELINE=$(ls -d $ROOT/Tests/baseline/*/bench-kmila-vcd.sha256 | sort | tail -1)
dotnet run -c Release --no-build -- hash --check "$BASELINE"

step "practices: solutions, GHDL oracle (ratchet), starters"
cd $ROOT/PracticeValidator
dotnet run -c Release -p:TargetFrameworks=net10.0

step "all green"
