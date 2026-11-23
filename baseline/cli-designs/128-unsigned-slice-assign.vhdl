library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity unsigned_slice_assign is
    port (
        clk  : in  std_logic;
        word : in  std_logic_vector(23 downto 0);
        go   : in  std_logic;
        data : out std_logic_vector(7 downto 0)
    );
end unsigned_slice_assign;

architecture rtl of unsigned_slice_assign is
    type state_t is (S_IDLE, S_FETCH, S_WAIT, S_WRITE, S_DONE);
    signal state    : state_t := S_IDLE;
    signal mar_reg  : unsigned(7 downto 0) := (others => '0');
    signal data_reg : std_logic_vector(7 downto 0) := (others => '0');
    signal rom_addr : std_logic_vector(7 downto 0);
    signal rom_data : std_logic_vector(7 downto 0) := (others => '0');

    type rom_t is array (0 to 7) of std_logic_vector(7 downto 0);
    constant rom_init : rom_t := (
        "10101010", "10111011", "11001100", "11011101",
        "00010001", "00100010", "00110011", "01000100"
    );
begin
    -- Sync ROM read.
    process(clk)
    begin
        if rising_edge(clk) then
            rom_data <= rom_init(to_integer(unsigned(rom_addr)));
        end if;
    end process;

    rom_addr <= std_logic_vector(mar_reg);

    -- CPU-shaped FSM mirroring P2A's S_EXEC_LOAD_* pipeline.
    process(clk)
    begin
        if rising_edge(clk) then
            case state is
                when S_IDLE =>
                    if go = '1' then
                        mar_reg <= unsigned(word(7 downto 0));   -- THE assignment under test
                        state   <= S_FETCH;
                    end if;
                when S_FETCH =>
                    state <= S_WAIT;          -- wait one cycle for sync ROM
                when S_WAIT =>
                    state <= S_WRITE;
                when S_WRITE =>
                    data_reg <= rom_data;
                    state    <= S_DONE;
                when S_DONE =>
                    state <= S_DONE;
            end case;
        end if;
    end process;

    data <= data_reg;
end rtl;
