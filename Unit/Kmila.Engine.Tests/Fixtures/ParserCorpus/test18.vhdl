-- Test 18: Advanced Signal Attributes, Guarded Signals, Blocks, and Bus Resolution
-- Tests: 'event, 'stable, 'last_value, 'delayed, guarded signals, block statements, bus resolution

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

-- Resolution function for multi-driver bus
package BusPackage is
    type std_logic_vector_array is array (natural range <>) of std_logic;

    function resolve_bus(drivers : std_logic_vector_array) return std_logic;

    subtype resolved_logic is resolve_bus std_logic;
    type resolved_bus is array (natural range <>) of resolved_logic;
end BusPackage;

package body BusPackage is
    function resolve_bus(drivers : std_logic_vector_array) return std_logic is
        variable result : std_logic := 'Z';
    begin
        for i in drivers'range loop
            case drivers(i) is
                when '0' =>
                    if result = '1' then
                        result := 'X';
                    elsif result /= 'X' then
                        result := '0';
                    end if;
                when '1' =>
                    if result = '0' then
                        result := 'X';
                    elsif result /= 'X' then
                        result := '1';
                    end if;
                when 'Z' =>
                    null;
                when others =>
                    result := 'X';
            end case;
        end loop;
        return result;
    end resolve_bus;
end BusPackage;

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use work.BusPackage.all;

entity SignalAttributeDemo is
    generic (
        WIDTH : integer := 8;
        SETUP_TIME : time := 2 ns;
        HOLD_TIME : time := 1 ns;
        PULSE_WIDTH : time := 5 ns
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        -- Data inputs
        data_in : in std_logic_vector(WIDTH - 1 downto 0);
        enable : in std_logic;
        -- Control signals for testing attributes
        test_mode : in std_logic_vector(3 downto 0);
        -- Outputs
        data_out : out std_logic_vector(WIDTH - 1 downto 0);
        edge_detected : out std_logic;
        signal_stable : out std_logic;
        value_changed : out std_logic;
        timing_violation : out std_logic;
        -- Bus interface
        bus_data : inOut std_logic_vector(WIDTH - 1 downto 0);
        bus_en : in std_logic
    );
end SignalAttributeDemo;

architecture RTL of SignalAttributeDemo is

    -- Internal signals for attribute testing
    signal data_reg : std_logic_vector(WIDTH - 1 downto 0) := (others => '0');
    signal data_delayed : std_logic_vector(WIDTH - 1 downto 0);
    signal clk_delayed : std_logic;

    -- Guarded signals
    signal guard_signal : std_logic := '0';
    signal guarded_data : std_logic_vector(WIDTH - 1 downto 0) bus := (others => '0');

    -- Signals for timing checks
    signal setup_ok : std_logic := '1';
    signal hold_ok : std_logic := '1';
    signal last_clk_edge : time := 0 ns;
    signal last_data_change : time := 0 ns;

    -- Block guard signal
    signal block_enable : std_logic := '0';

begin

    -- Demonstrate 'event attribute (edge detection)
    edge_detection : process(clk)
    begin
        if clk'event and clk = '1' then
            edge_detected <= '1';
        else
            edge_detected <= '0';
        end if;
    end process edge_detection;

    -- Demonstrate 'stable attribute
    stability_check : process(clk, data_in)
    begin
        if rising_edge(clk) then
            if data_in'stable(SETUP_TIME) then
                signal_stable <= '1';
            else
                signal_stable <= '0';
            end if;
        end if;
    end process stability_check;

    -- Demonstrate 'last_value attribute
    change_detection : process(data_in)
    begin
        if data_in'event then
            if data_in /= data_in'last_value then
                value_changed <= '1';
            else
                value_changed <= '0';
            end if;
        end if;
    end process change_detection;

    -- Demonstrate 'delayed attribute
    data_delayed <= data_in'delayed(PULSE_WIDTH);
    clk_delayed <= clk'delayed(HOLD_TIME);

    -- Setup and hold time checking process
    timing_check : process(clk, data_in)
        variable data_stable_at_edge : boolean;
    begin
        if data_in'event then
            last_data_change <= now;
        end if;

        if rising_edge(clk) then
            last_clk_edge <= now;

            -- Setup time check
            if (now - last_data_change) < SETUP_TIME then
                setup_ok <= '0';
            else
                setup_ok <= '1';
            end if;
        end if;

        -- Hold time check using delayed clock
        if clk_delayed'event and clk_delayed = '1' then
            if data_in'event then
                hold_ok <= '0';
            else
                hold_ok <= '1';
            end if;
        end if;
    end process timing_check;

    timing_violation <= not (setup_ok and hold_ok);

    -- Main data register with attribute-based control
    main_register : process(clk, rst)
    begin
        if rst = '1' then
            data_reg <= (others => '0');
        elsif clk'event and clk = '1' then
            case test_mode is
                when "0000" =>
                    -- Normal operation
                    if enable = '1' then
                        data_reg <= data_in;
                    end if;

                when "0001" =>
                    -- Only update if data has been stable
                    if data_in'stable(SETUP_TIME) and enable = '1' then
                        data_reg <= data_in;
                    end if;

                when "0010" =>
                    -- Use delayed data
                    if enable = '1' then
                        data_reg <= data_delayed;
                    end if;

                when "0011" =>
                    -- Toggle on data change
                    if data_in'event then
                        data_reg <= not data_reg;
                    end if;

                when "0100" =>
                    -- Capture last value before change
                    if data_in'event then
                        data_reg <= data_in'last_value;
                    end if;

                when others =>
                    data_reg <= data_reg;
            end case;
        end if;
    end process main_register;

    data_out <= data_reg;

    -- Guarded block demonstration
    guard_signal <= enable and not rst;

    guarded_block : block (guard_signal = '1')
    begin
        -- Guarded signal assignment
        guarded_data <= guarded data_in when enable = '1' else (others => 'Z');
    end block guarded_block;

    -- Named block with local declarations
    processing_block : block
        signal local_counter : unsigned(7 downto 0) := (others => '0');
        signal local_flag : std_logic := '0';
    begin
        counter_proc : process(clk, rst)
        begin
            if rst = '1' then
                local_counter <= (others => '0');
                local_flag <= '0';
            elsif rising_edge(clk) then
                if enable = '1' then
                    local_counter <= local_counter + 1;
                    if local_counter = 255 then
                        local_flag <= '1';
                    else
                        local_flag <= '0';
                    end if;
                end if;
            end if;
        end process counter_proc;
    end block processing_block;

    -- Nested blocks
    outer_block : block
        signal outer_sig : std_logic := '0';
    begin
        outer_sig <= clk and enable;

        inner_block : block
            signal inner_sig : std_logic := '0';
        begin
            inner_sig <= outer_sig and data_in(0);

            inner_proc : process(clk)
            begin
                if rising_edge(clk) then
                    if inner_sig = '1' then
                        block_enable <= '1';
                    else
                        block_enable <= '0';
                    end if;
                end if;
            end process inner_proc;
        end block inner_block;
    end block outer_block;

    -- Bus tristate control
    bus_driver : process(bus_en, data_reg)
    begin
        if bus_en = '1' then
            bus_data <= data_reg;
        else
            bus_data <= (others => 'Z');
        end if;
    end process bus_driver;

    -- Process using multiple signal attributes in conditions
    complex_attribute_proc : process(clk, rst)
        variable edge_count : integer := 0;
    begin
        if rst = '1' then
            edge_count := 0;
        elsif clk'event then
            if clk = '1' then
                -- Rising edge
                if data_in'stable(SETUP_TIME) and not data_in'quiet(HOLD_TIME) then
                    edge_count := edge_count + 1;
                end if;
            else
                -- Falling edge
                if enable'last_value = '1' and enable = '0' then
                    edge_count := 0;
                end if;
            end if;
        end if;
    end process complex_attribute_proc;

    -- Delayed signal assignment with inertial delay
    delayed_assignment : process
    begin
        wait until rising_edge(clk);
        if enable = '1' then
            data_out <= data_in after PULSE_WIDTH;
        end if;
    end process delayed_assignment;

end RTL;

-- Configuration for the demo
configuration SignalAttributeDemo_Config of SignalAttributeDemo is
    for RTL
    end for;
end SignalAttributeDemo_Config;
