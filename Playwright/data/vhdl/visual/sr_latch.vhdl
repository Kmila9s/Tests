library IEEE;
use IEEE.std_logic_1164.all;

-- Asynchronous SR latch via cross-coupled NORs. Bistable: set with btn_s,
-- reset with btn_r, holds state when both released. Hold case (s=0,r=0)
-- exercises the engine's combinational-loop convergence at the visual
-- layer.
entity sr_latch is
    port (
        btn_s : in  std_logic;
        btn_r : in  std_logic;
        led_q : out std_logic;
        led_n : out std_logic
    );
end entity;

architecture behavioral of sr_latch is
    signal q  : std_logic := '0';
    signal nq : std_logic := '1';
begin
    q     <= not (btn_r or nq);
    nq    <= not (btn_s or q);
    led_q <= q;
    led_n <= nq;
end architecture;
