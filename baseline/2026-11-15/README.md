# Línea base 2026-11-15 (antes de la Fase 0 multi-HDL)

Estado de referencia para el *golden master* del refactor. Generado con el código en: Parser v2.0.0, Interpreter v2.1.0,
Debugger v2.0.0, TimeMachine v2.0.0, LibraryCompiler v2.0.0, App/Kmila v2.5.0, Tests v2.5.0, PracticeValidator v2.0.0.

| Suite | Resultado |
|---|---|
| SimulatorBench `run` (fuente legada `kmila.vhdl`, GHDL 6.0.0) | **149/149 PASS** — `bench-report.json`, hashes VCD Kmila en `bench-kmila-vcd.sha256` (sin bloque `$date`) |
| Kmila.Shared.Tests (`dotnet test`) | **239/239 PASS** |
| PracticeValidator | **135/135 OK** — `practice-validator.txt` |
| Interpreter sweep (`dotnet run`) | **54/54 PASS** — `interpreter-sweep.txt` |
| Interpreter `--test-delta` | **ABORTA** en "Delta Swap Behavior": `Delta cycle limit (1000) exceeded at tick 0` — falla preexistente, no está en CI — `interpreter-test-delta.txt` |
| Parser sweep | 40 archivos OK, negativos 3/3 — `parser-sweep.txt` |
| Debugger | 1 línea con error/exception en su salida — `debugger-errors-count.txt` |
| CLI (net10.0) `check/sim/synth/flow/fit` sobre 5 casos | salidas en `cli/` |

Nota: `SimulatorBench verify` reporta *drift* del snapshot OpenAPI (`wwwroot/openapi/v1.json`) — preexistente.
