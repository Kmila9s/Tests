# Línea base 2026-11-16 (v3.1.1, destino único de asignación)

Golden master vigente de la bench (`bench-kmila-vcd.sha256`, SHA-256 de `Cases/*/results/kmila.vcd`
sin las 3 primeras líneas del bloque `$date`). Igual a `../2026-11-15/bench-kmila-vcd.sha256` salvo 4 casos,
cuyo VCD ahora coincide con GHDL 6.0.0 (`results/ghdl/golden.vcd`); antes divergían:

| Caso | Antes (2026-11-15) | Ahora |
|---|---|---|
| 108-demux-marq | señal espuria `Y ( 0 )`; `y` = `0000` en t=0 | sin señal espuria; `y` = `0001` (GHDL) |
| 131-indexed-lhs-write | señal espuria `ConditionResult`; `mem`/`dout` en `x` | `dout` = `00000000` desde t=0 (GHDL) |
| 158-process-local-ram | `dout` = `xxxxxxxx` tras la escritura | `dout` = `11011101` a los 15 ns (GHDL) |
| 165-fn-for-loop-count-ones | `y` = `xxxx`; faltaba `1001` a los 10 ns | `y` = `0000`, luego `1001` (GHDL) |

Causa: las escrituras a bit/rango/elemento (`AssignmentTarget`, v3.1.1) ya no se pierden ni crean señales espurias.
149/149 PASS. La CLI sigue verificándose contra `../2026-11-16-lint/cli` (sin cambios: 35/35).
