# Línea base 2026-11-22 (v3.1.9, bench verbatim: el testbench del autor)

Golden master vigente. Plan: `Documentacion/handoffs/planes/PLAN_2026-11-22_v319_gaps_verbatim.md`.
`../2026-11-20-v316/` queda como línea base histórica (la última sobre las reimplementaciones legacy).

## Bench (`bench-kmila-vcd.sha256`)

SHA-256 de `SimulatorBench/Cases/*/results/vhdl/kmila.vcd` sin el bloque `$date`, clave `caso/lenguaje`.
Desde v3.1.9 la bench simula **el testbench del autor tal cual** (`variant.top`) en Kmila y en GHDL 6.0.0
y compara en modo estricto (nueve valores, t=0 incluido) **todas las señales del scope del testbench**.
Ya no hay reimplementaciones `legacy/` ni estímulo derivado: los genéricos del `generic map` y el estímulo
son los del autor (G86).

Resultado: **147/149**. Los 2 restantes están en `SimulatorBench/known-gaps.json` (ratchet):
`014-mux-sel` (`WAIT FOR 500 ps`) y `113-edges-det` (`HALF_PERIODE = 5.26315789474 ns`) — **G103**, retardos
menores a 1 ns; el motor cuenta el tiempo en ns enteros. Diferido a la Fase 2 (tiempo absoluto en fs/ps),
con aprobación del usuario (2026-11-22). La corrida por HTTP (`run --http`) da los mismos 149 hashes.

Cada VCD cambió respecto de v3.1.6, porque cambió lo que se simula (el testbench completo en lugar de la
DUT con estímulo) y porque el instante final ahora se procesa (como `--stop-time` de GHDL). La referencia de
cada caso es su propio `golden.vcd` de GHDL: la comparación estricta de la corrida es la verificación.
Correcciones del motor que hicieron falta (repros en `Documentacion/handoffs/repros_gaps_2026-11-16/v319/`):
instante final inclusivo, booleanos en el VCD, G84–G102 (ver `EXPECTED.md` de v308, v309 y v319).

## CLI (`cli/`)

`cli_golden.sh` corre `kmila check/sim/synth/flow/fit` sobre `../cli-designs/` (los cinco diseños de un
archivo que antes vivían en `SimulatorBench/Cases/*/legacy/`, sin cambios). Las 35 salidas son
idénticas a v3.1.6 salvo la duración y la fecha del VCD, que el script normaliza. Cubren la ruta de
estímulo por puertos (entrada por entidad), junto con el oráculo de prácticas y los fixtures.

## Escáner de estrictez (`strict_scan.txt`)

120 de 1341 archivos con error (v3.1.6: 127 de 1469). Sin rechazos nuevos; `027-add-sub/.../tb_adder_subtractor.vhd`
pasa a aceptarse (G95: `"0"` es un vector de 1 bit). El total baja porque las 149 fuentes `legacy/` ya no existen.
