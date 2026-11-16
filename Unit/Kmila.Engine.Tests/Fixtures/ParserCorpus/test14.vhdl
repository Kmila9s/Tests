-- Test 14: Complex Package with Types, Subtypes, Constants, Functions, Procedures
-- Tests: Record types, array types, subtypes, deferred constants, overloaded functions, procedures

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

package DSP_Types is

    -- Basic type definitions
    constant MAX_TAPS : integer := 256;
    constant COEFF_WIDTH : integer := 18;
    constant DATA_WIDTH : integer := 16;
    constant ACC_WIDTH : integer := 48;

    -- Subtype definitions
    subtype coefficient_t is signed(COEFF_WIDTH - 1 downto 0);
    subtype sample_t is signed(DATA_WIDTH - 1 downto 0);
    subtype accumulator_t is signed(ACC_WIDTH - 1 downto 0);
    subtype tap_count_t is integer range 0 to MAX_TAPS - 1;

    -- Array types
    type coefficient_array_t is array (natural range <>) of coefficient_t;
    type sample_array_t is array (natural range <>) of sample_t;
    type sample_buffer_t is array (0 to MAX_TAPS - 1) of sample_t;

    -- Record type for filter configuration
    type filter_config_t is record
        num_taps : tap_count_t;
        symmetric : boolean;
        interpolation : integer;
        decimation : integer;
        bypass : std_logic;
    end record filter_config_t;

    -- Record type for filter state
    type filter_state_t is record
        buffer : sample_buffer_t;
        write_ptr : tap_count_t;
        phase : integer;
        output_valid : std_logic;
    end record filter_state_t;

    -- Record for complex numbers
    type complex_t is record
        re : sample_t;
        im : sample_t;
    end record complex_t;

    type complex_array_t is array (natural range <>) of complex_t;

    -- Enumerated type for filter types
    type filter_type_t is (FIR_LOWPASS, FIR_HIGHPASS, FIR_BANDPASS, FIR_BANDSTOP, IIR_BIQUAD);

    -- Constants
    constant ZERO_SAMPLE : sample_t := (others => '0');
    constant ZERO_COEFF : coefficient_t := (others => '0');
    constant ZERO_ACC : accumulator_t := (others => '0');
    constant ZERO_COMPLEX : complex_t := (re => ZERO_SAMPLE, im => ZERO_SAMPLE);

    constant DEFAULT_CONFIG : filter_config_t := (
        num_taps => 16,
        symmetric => true,
        interpolation => 1,
        decimation => 1,
        bypass => '0'
    );

    -- Deferred constants (defined in package body)
    constant KAISER_BETA : real;
    constant HAMMING_ALPHA : real;

    -- Function declarations
    function saturate(val : accumulator_t; width : integer) return signed;
    function round_convergent(val : accumulator_t; shift : integer) return sample_t;

    -- Overloaded multiplication functions
    function mult(a : sample_t; b : coefficient_t) return accumulator_t;
    function mult(a : complex_t; b : complex_t) return complex_t;
    function mult(a : complex_t; b : coefficient_t) return complex_t;

    -- Overloaded addition functions
    function add(a : sample_t; b : sample_t) return sample_t;
    function add(a : complex_t; b : complex_t) return complex_t;
    function add(a : accumulator_t; b : accumulator_t) return accumulator_t;

    -- Complex operations
    function conj(c : complex_t) return complex_t;
    function magnitude_squared(c : complex_t) return accumulator_t;

    -- Buffer operations
    function shift_buffer(buf : sample_buffer_t; new_sample : sample_t) return sample_buffer_t;

    -- Procedure declarations
    procedure mac(
        signal acc : inOut accumulator_t;
        sample : in sample_t;
        coeff : in coefficient_t
    );

    procedure reset_state(
        signal state : out filter_state_t
    );

    procedure update_buffer(
        signal state : inOut filter_state_t;
        new_sample : in sample_t
    );

end DSP_Types;

package body DSP_Types is

    -- Deferred constant definitions
    constant KAISER_BETA : real := 5.65326;
    constant HAMMING_ALPHA : real := 0.54;

    -- Saturation function
    function saturate(val : accumulator_t; width : integer) return signed is
        variable result : signed(width - 1 downto 0);
        variable max_val : accumulator_t;
        variable min_val : accumulator_t;
    begin
        max_val := to_signed((2 ** (width - 1)) - 1, ACC_WIDTH);
        min_val := to_signed(-(2 ** (width - 1)), ACC_WIDTH);

        if val > max_val then
            result := (width - 1 => '0', others => '1');
        elsif val < min_val then
            result := (width - 1 => '1', others => '0');
        else
            result := val(width - 1 downto 0);
        end if;
        return result;
    end saturate;

    -- Convergent rounding function
    function round_convergent(val : accumulator_t; shift : integer) return sample_t is
        variable shifted : accumulator_t;
        variable lsb : std_logic;
        variable guard : std_logic;
        variable round_bit : std_logic;
    begin
        if shift <= 0 then
            return val(DATA_WIDTH - 1 downto 0);
        end if;

        shifted := shift_right(val, shift - 1);
        guard := shifted(0);
        shifted := shift_right(shifted, 1);
        lsb := shifted(0);

        -- Round to nearest even
        if guard = '1' and (lsb = '1' or val(shift - 2 downto 0) /= 0) then
            shifted := shifted + 1;
        end if;

        return saturate(shifted, DATA_WIDTH);
    end round_convergent;

    -- Multiplication: sample * coefficient
    function mult(a : sample_t; b : coefficient_t) return accumulator_t is
        variable result : signed((DATA_WIDTH + COEFF_WIDTH) - 1 downto 0);
    begin
        result := a * b;
        return resize(result, ACC_WIDTH);
    end mult;

    -- Multiplication: complex * complex
    function mult(a : complex_t; b : complex_t) return complex_t is
        variable result : complex_t;
        variable re_re, im_im, re_im, im_re : accumulator_t;
    begin
        re_re := mult(a.re, resize(b.re, COEFF_WIDTH));
        im_im := mult(a.im, resize(b.im, COEFF_WIDTH));
        re_im := mult(a.re, resize(b.im, COEFF_WIDTH));
        im_re := mult(a.im, resize(b.re, COEFF_WIDTH));

        result.re := round_convergent(re_re - im_im, COEFF_WIDTH);
        result.im := round_convergent(re_im + im_re, COEFF_WIDTH);
        return result;
    end mult;

    -- Multiplication: complex * coefficient (scalar)
    function mult(a : complex_t; b : coefficient_t) return complex_t is
        variable result : complex_t;
    begin
        result.re := round_convergent(mult(a.re, b), COEFF_WIDTH);
        result.im := round_convergent(mult(a.im, b), COEFF_WIDTH);
        return result;
    end mult;

    -- Addition: sample + sample with saturation
    function add(a : sample_t; b : sample_t) return sample_t is
        variable sum : signed(DATA_WIDTH downto 0);
    begin
        sum := resize(a, DATA_WIDTH + 1) + resize(b, DATA_WIDTH + 1);
        return saturate(resize(sum, ACC_WIDTH), DATA_WIDTH);
    end add;

    -- Addition: complex + complex
    function add(a : complex_t; b : complex_t) return complex_t is
        variable result : complex_t;
    begin
        result.re := add(a.re, b.re);
        result.im := add(a.im, b.im);
        return result;
    end add;

    -- Addition: accumulator + accumulator
    function add(a : accumulator_t; b : accumulator_t) return accumulator_t is
    begin
        return a + b;
    end add;

    -- Complex conjugate
    function conj(c : complex_t) return complex_t is
        variable result : complex_t;
    begin
        result.re := c.re;
        result.im := -c.im;
        return result;
    end conj;

    -- Magnitude squared: |c|^2 = re^2 + im^2
    function magnitude_squared(c : complex_t) return accumulator_t is
        variable re_sq, im_sq : accumulator_t;
    begin
        re_sq := mult(c.re, resize(c.re, COEFF_WIDTH));
        im_sq := mult(c.im, resize(c.im, COEFF_WIDTH));
        return re_sq + im_sq;
    end magnitude_squared;

    -- Shift buffer and insert new sample
    function shift_buffer(buf : sample_buffer_t; new_sample : sample_t) return sample_buffer_t is
        variable result : sample_buffer_t;
    begin
        result(0) := new_sample;
        for i in 1 to MAX_TAPS - 1 loop
            result(i) := buf(i - 1);
        end loop;
        return result;
    end shift_buffer;

    -- Multiply-accumulate procedure
    procedure mac(
        signal acc : inOut accumulator_t;
        sample : in sample_t;
        coeff : in coefficient_t
    ) is
    begin
        acc <= acc + mult(sample, coeff);
    end mac;

    -- Reset filter state procedure
    procedure reset_state(
        signal state : out filter_state_t
    ) is
    begin
        state.write_ptr <= 0;
        state.phase <= 0;
        state.output_valid <= '0';
        for i in 0 to MAX_TAPS - 1 loop
            state.buffer(i) <= ZERO_SAMPLE;
        end loop;
    end reset_state;

    -- Update buffer procedure
    procedure update_buffer(
        signal state : inOut filter_state_t;
        new_sample : in sample_t
    ) is
    begin
        state.buffer(state.write_ptr) <= new_sample;
        if state.write_ptr = MAX_TAPS - 1 then
            state.write_ptr <= 0;
        else
            state.write_ptr <= state.write_ptr + 1;
        end if;
    end update_buffer;

end DSP_Types;

-- Entity using the package
library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use work.DSP_Types.all;

entity FIRFilter is
    generic (
        NUM_TAPS : integer := 16
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        config : in filter_config_t;
        data_in : in sample_t;
        data_valid : in std_logic;
        coeffs : in coefficient_array_t(0 to NUM_TAPS - 1);
        data_out : out sample_t;
        out_valid : out std_logic
    );
end FIRFilter;

architecture RTL of FIRFilter is
    signal state : filter_state_t;
    signal accumulator : accumulator_t := ZERO_ACC;
    signal tap_counter : tap_count_t := 0;
    signal computing : std_logic := '0';
begin
    process(clk, rst)
    begin
        if rst = '1' then
            reset_state(state);
            accumulator <= ZERO_ACC;
            tap_counter <= 0;
            computing <= '0';
            data_out <= ZERO_SAMPLE;
            out_valid <= '0';
        elsif rising_edge(clk) then
            out_valid <= '0';

            if config.bypass = '1' then
                data_out <= data_in;
                out_valid <= data_valid;
            elsif data_valid = '1' and computing = '0' then
                update_buffer(state, data_in);
                accumulator <= ZERO_ACC;
                tap_counter <= 0;
                computing <= '1';
            elsif computing = '1' then
                mac(accumulator, state.buffer(tap_counter), coeffs(tap_counter));

                if tap_counter = config.num_taps - 1 then
                    data_out <= round_convergent(accumulator, COEFF_WIDTH);
                    out_valid <= '1';
                    computing <= '0';
                else
                    tap_counter <= tap_counter + 1;
                end if;
            end if;
        end if;
    end process;
end RTL;
