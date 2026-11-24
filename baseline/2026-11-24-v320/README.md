# Línea base 2026-11-24 (v3.2.0, tiempo absoluto en femtosegundos — G103)

Golden master vigente. Plan: `Documentacion/handoffs/planes/PLAN_2026-11-24_fase2_ir.md` (Fase 2, etapa v3.2.0).
`../2026-11-22-v319/` queda como línea base histórica.

## Bench (`bench-kmila-vcd.sha256`)

SHA-256 de `SimulatorBench/Cases/*/results/vhdl/kmila.vcd` sin el bloque `$date`, clave `caso/lenguaje`; la bench
simula el testbench de cada autor tal cual en Kmila y en GHDL 6.0.0 y compara en modo estricto.

Resultado: **149/149** con `SimulatorBench/known-gaps.json` **vacío**. El motor cuenta el tiempo en ticks de un número
entero de femtosegundos (`SimulationConfig.TickFs`), así que los retardos menores a 1 ns y los que no son un ns entero
caen exactos. La corrida por HTTP (`run --http`) da los mismos 149 hashes.

Cambian **13** hashes respecto de v3.1.9, todos por el mismo origen (un retardo que antes se redondeaba a ns enteros):
- `014-mux-sel` (`WAIT FOR 500 ps`) y `113-edges-det` (`HALF_PERIODE = 5.26315789474 ns`, VCD en `1fs`): antes en el ratchet.
- `018`…`024`, `026`…`029`: `tb_delay := 3*PERIOD/4` = 7.5 ns, que se redondeaba a 8 ns. Pasaban la comparación, pero sus
  instantes estaban corridos medio ns.

Verificación: en los 13, el conjunto de instantes de cambio del VCD de Kmila es idéntico al del `golden.vcd` de GHDL
(salvo el `#0` inicial de GHDL, como en v3.1.9). Los otros 136 hashes son idénticos a v3.1.9.
Repro: `Documentacion/handoffs/repros_gaps_2026-11-16/v320/`.

## CLI (`cli/`)

Las 35 salidas de `cli_golden.sh` son idénticas a v3.1.9 (copiadas sin cambios).

## Escáner de estrictez (`strict_scan.txt`)

120 de 1343 archivos con error, igual que v3.1.9 (120 de 1341). Sin rechazos nuevos; los dos archivos nuevos (el repro y el
fixture `gaps/g103-sub-ns`) se aceptan.
