# Línea base 2026-11-16 (v3.1.2, records completos)

Golden master vigente de la bench (`bench-kmila-vcd.sha256`, SHA-256 de `Cases/*/results/kmila.vcd`
sin las 3 primeras líneas del bloque `$date`) y de la CLI (`cli/`, `cli_golden.sh`, 35 salidas).
Igual a `../2026-11-16-v311/` salvo 12 casos de máquinas de estados con tipos enumerados:

055-seqdet-lemur, 056-code-det, 061-compar-fsm, 086-sec-moore, 092-sec-mealy, 093-sec-mooress,
099-sec-mealyss, 100-traffic, 112-ser-fsm, 121-multi-hop-synced-rom, 128-unsigned-slice-assign,
129-enum-eq-conditional.

| Antes (v3.1.1) | Ahora (v3.1.2) |
|---|---|
| cada literal del enum (`sA`, `S_IDLE`, …) se volcaba como `$var wire 1` con valor `x` | los literales son constantes: no se vuelcan (en VHDL no son objetos) |
| la señal enum (`state`) se volcaba como `bxx…` constante | se vuelca el índice del literal en binario (`b00`, `b01`, …) |

GHDL 6.0.0 no vuelca enums en VCD, así que el resto de las señales es lo que se compara: 149/149 PASS semántico
contra `results/ghdl/golden.vcd`. En la CLI solo cambia `128-unsigned-slice-assign.vcd` por la misma razón.
