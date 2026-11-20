# Línea base 2026-11-20 (v3.1.6, drivers, números y tiempo)

Golden master vigente de la bench (`bench-kmila-vcd.sha256`, SHA-256 de `Cases/*/results/kmila.vcd`
sin las 3 primeras líneas del bloque `$date`), de la CLI (`cli/`, `cli_golden.sh`, 35 salidas) y del
escáner de estrictez (`strict_scan.txt`). Plan: `Documentacion/handoffs/planes/PLAN_2026-11-20_v316_drivers_numeros_tiempo.md`.
`../2026-11-19-v315/` queda como línea base histórica.

Bench estricta: **149/149 PASS, sin `knownGap`**. Las 13 `t0Divergence` de v3.1.5 se conservan (dependen
de que la bench simule la fuente verbatim, Fase 0 5b).

## VCD cambiados respecto de v3.1.5: 1 de 149

| Caso | Categoría | Antes (v3.1.5) | Ahora (v3.1.6) |
|---|---|---|---|
| `136-fn-multi-caller-scope` | sensibilidad implícita (G62) y alcance de subprogramas por instancia (G67) | `o1`/`o2` = 5/10 (la función se re-evaluaba en cada delta y leía `k_sig` del primer llamador); `knownGap` | `XXXXXXXX`, igual que GHDL: la sensibilidad viene solo del texto del enunciado, nunca del cuerpo de la función |

Los demás cambios del motor (drivers con resolución IEEE 1164, `transport`/`inertial`/`reject`,
`inout`, `time` en fs y `now`, aritmética `BigInteger` con ancho y signo de numeric_std, rangos de
subtipo) no mueven ningún VCD de la bench. Los casos 012 y 014 recuperaron el `transport` de su fuente de
referencia en `kmila.vhdl` (local, SimulatorBench no tiene git), y su VCD no cambia. Cada construcción nueva
se verifica contra GHDL 6.0.0 en `Documentacion/handoffs/repros_gaps_2026-11-16/v316/` (`EXPECTED.md`)
y en `Tests/Unit/Kmila.Shared.Tests/Engine/V316Tests.cs`.

## CLI (`cli/`)

Las 35 salidas son idénticas a v3.1.5 salvo la duración (`… ms`) y la fecha del VCD, que
`cli_golden.sh` normaliza.

## Escáner de estrictez (`strict_scan.txt`)

127 de 1882 archivos con error (v3.1.5: 154 de 1828).
- **ERR → OK (29):** se aceptan diseños válidos que antes se rechazaban. Son las referencias y los testbenches de 005, 006, 012, 014, 020, 034, 063, 066, 067, 068, 071, 100 y 103 (formas de onda con varios elementos, `transport`, `now`, `time`, `std_logic_unsigned`) y sus copias en la evidencia TT2.
- **Archivos nuevos (54):** los repros de v3.1.6 y sus fixtures. `constructs.vhdl` pasa gracias a que `check` ya acepta `case … generate`. Solo se rechaza `in_write.vhdl` (×2, línea 11), y es intencional: GHDL da el mismo error, «cannot assign to input port».
- **OK → ERR:** ninguno.
