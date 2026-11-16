# Línea base CLI 2026-11-16 (v3.0.6, GAP-LINT-001)

Igual a `../2026-11-15/cli` salvo los `*.check.txt` de 001, 002 y 128: las reglas de lint reescritas
(`Debugger/Repositories/Lint`) ya no emiten los falsos positivos de `VHD-BLOCK-BALANCE` (contaba los `end`
de entidad/`if`/`process` contra los `begin`) ni de `VHD-MISSING-SEMICOLON` (sentencias que continúan en la
línea siguiente). Los `*.sim.err` guardan solo el nombre del VCD (ruta normalizada por `cli_golden.sh`).
Simulación, síntesis, flujo y recursos: idénticos. La línea base de la bench (`../2026-11-15/bench-kmila-vcd.sha256`)
sigue siendo el golden master.

Regenerar/verificar: `Tests/baseline/cli_golden.sh [dir] [--update]`.
