library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

entity scale_generator is
    port(
        note_sel : in  std_logic_vector(2 downto 0);
        enable   : in  std_logic;
        freq_hz  : out std_logic_vector(15 downto 0);
        gate     : out std_logic
    );
end scale_generator;

architecture rtl of scale_generator is
    signal note_freq : integer range 0 to 65535;
begin
    note_freq <= 262 when note_sel = "000" else   -- C4
                 294 when note_sel = "001" else   -- D4
                 330 when note_sel = "010" else   -- E4
                 349 when note_sel = "011" else   -- F4
                 392 when note_sel = "100" else   -- G4
                 440 when note_sel = "101" else   -- A4
                 494 when note_sel = "110" else   -- B4
                 523;                               -- C5

    freq_hz <= std_logic_vector(to_unsigned(note_freq, 16));
    gate    <= enable;
end rtl;
