# Línea base 2026-11-19 (v3.1.5, 9 valores y valores iniciales LRM)

Golden master vigente de la bench (`bench-kmila-vcd.sha256`, SHA-256 de `Cases/*/results/kmila.vcd`
sin las 3 primeras líneas del bloque `$date`), de la CLI (`cli/`, `cli_golden.sh`, 35 salidas) y del
escáner de estrictez (`strict_scan.txt`). Plan: `Documentacion/handoffs/planes/PLAN_2026-11-19_v315_valores_lrm.md`.

Es el cambio intencional de la Fase 1: **147 de los 149 VCD cambian** respecto de `../2026-11-16-v312/`
(que queda como línea base histórica de la semántica anterior). Categorías, todas verificadas contra
`results/ghdl/golden.vcd` con el comparador estricto:

| Categoría | Antes (v3.1.4) | Ahora (v3.1.5) |
|---|---|---|
| Reloj sintético | puerto falso `_clk` en 81 VCD | ya no existe; la bench declara `clocks` en los 26 casos con estímulo escrito a mano (los `autoStimulus` toman `clk` del golden) |
| Valores iniciales | todo objeto sin `:=` y todo puerto sembrado en `0` | `T'left`: `U` en std_logic/vectores, `integer'left`, primer literal del enum; las entradas toman el valor inicial de su señal en el testbench de referencia (`portInitials`) |
| 9 valores | `U`/`X` se volvían `0`/`1` en el evaluador y `x` en el VCD | tablas IEEE 1164; el VCD escribe `U X Z W L H -` en mayúscula |
| Flancos | `U→1` era flanco de subida | `rising_edge` exige `To_X01(last)=0` y `To_X01(now)=1` |
| Deltas | deltas de t=0 como `#1`, `#2` ps; el nivel inicial del reloj no se registraba | un valor asentado por instante; el reloj registra su nivel inicial |

Comparador de la bench (`SimulatorBench/Tools/SemanticDiffer.cs`): **estricto** — compara también t=0 y
distingue U, X y Z (antes `x` era comodín en ambos lados y t=0 se saltaba). Resultado: **148/149 PASS + 1 GAP**:

- `136-fn-multi-caller-scope`: `knownGap` G62 (la sensibilidad implícita de una asignación concurrente
  incluye señales leídas dentro de la función llamada; según el LRM no, y GHDL deja `o1/o2` en `X`) → v3.1.6.
- 13 casos con `t0Divergence` (004, 023, 024, 026, 074, 075, 076, 077, 080, 081, 097, 098, 101): el
  `kmila.vhdl` reescrito inicializa el registro (o asigna la salida) distinto de la fuente de referencia; se
  verificó simulando la **referencia** con Kmila que en t=0 da lo mismo que GHDL. Su muestra de t=0 no se
  compara hasta que la bench simule la fuente verbatim (Fase 0, 5b); el resto del trazo sí.

CLI (`cli/`): cambian los 5 `.vcd` (`sim` sin estímulo: las entradas nacen en `U`, sin `_clk`, sin reloj
salvo `--clock`) y los `.sim.err` de 001, 002 y 166 (`signals=` sin `_clk`). `check`, `synth`, `flow` y `fit`
no cambian.
